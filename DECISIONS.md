# DECISIONS — KFS Ticket Booking Platform

This document captures every non-obvious architectural choice made while implementing [kfs_ticket_booking_prompt_v2 (1).md](kfs_ticket_booking_prompt_v2%20%281%29.md). Each entry: **what**, **why**, and **alternatives considered**.

## Hosting target: Azure UAE North

The school's data must stay in-region. Every PII-bearing resource (Postgres, Storage, Key Vault, App Insights, App Service) is provisioned in `uaenorth`. Static Web Apps is the one exception — it's not GA in UAE North as of writing, so the three frontends pin to `centralus`. SWAs serve only static assets (no PII), so this is acceptable; documented here so a reviewer doesn't need to chase it.

The full deployment is captured in [infra/main.bicep](infra/main.bicep) and per-env parameter files. CI deploys via OIDC federated credentials — see [.github/workflows/deploy.yml](.github/workflows/deploy.yml). No long-lived service principal secrets.

## Stack

### PostgreSQL 16 (matches spec v2 (1))
EF Core 8 + `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.4. Earlier passes detoured through SQL Server (spec v2) and back to Postgres (user request) — v2 (1) settles the question for good and adds Azure Database for PostgreSQL — Flexible Server as the managed target.

### Naming convention: snake_case via EFCore.NamingConventions
Tables and columns are snake_case (`booking_items`, `qr_code_payload`). The spec offers either default-PascalCase or snake_case; snake_case matches PG ecosystem norms (psql, pgAdmin, dbt, etc.) and stops the constant `"BookingItems"` quoting in raw SQL.

### Postgres-specific column types
- **Email columns** are `citext` (case-insensitive comparison) — drops the `ToLowerInvariant()` calls scattered across services.
- **`AuditLogs.metadata_json`** is `jsonb` (queryable in pure SQL, indexable on JSON paths). Spec calls this out explicitly.
- **`students.date_of_birth`** is `date`, not `timestamp` — it's a calendar value with no time component.
- **All other datetimes** are `timestamp with time zone` (timestamptz). Default Npgsql 8 behavior; no legacy switch enabled. The codebase uses `DateTime.UtcNow` consistently so all writes have `Kind=Utc`, which is the only safe input for timestamptz columns.

### Postgres extensions
Declared in `OnModelCreating` and emitted into the initial migration: `citext`, `pgcrypto`, `pg_trgm`. The Bicep template enables them via the `azure.extensions` server parameter so the migration applies cleanly in Azure.

### Time zone: Asia/Dubai (UTC+4, no DST)
Stored everywhere as UTC; rendered as Asia/Dubai for emails, reports, UI. [`KfsTime`](api/src/KFS.Application/Common/KfsTime.cs) is a static helper with a `FormatLocal` extension on `DateTime`. Resolves the IANA id on Linux and the Windows id on Windows hosts; falls back to a custom +4 zone if neither is available (covers trimmed Alpine images).
**App Service**: `WEBSITE_TIME_ZONE=Arabian Standard Time` is set in App Settings so any direct `DateTime.Now` calls (none today, but defensive) also resolve to Dubai.

### Concurrency: Serializable + SELECT FOR UPDATE + 40001 retry
The paired-seat reservation runs inside `IsolationLevel.Serializable`, **and** explicitly takes a row lock on both target seat rows with `SELECT 1 FROM seats WHERE id = ANY(...) FOR UPDATE` before the conflict check. If Postgres aborts the transaction with SQLState `40001` (serialization_failure), the service catches it, clears the change tracker, and retries once. After two attempts a conflict surfaces as `409 seat_taken` so the portal refreshes the seat map via SignalR.

### Storage: `IBlobStorage` with two backends
- `LocalDiskBlobStorage` — writes to `wwwroot/`; serves at `/static/`. Default for bare-metal local runs.
- `AzureBlobStorage` — used in Docker (Azurite) and Azure (Managed Identity → Storage account). Generates **5-minute SAS URLs** for blobs when running against a real Storage account, mirroring the spec. Connection-string mode (Azurite) returns plain URLs since the local emulator doesn't enforce auth.
- Switch via `Storage:Provider` config: `LocalDisk` (default) or `AzureBlob`. The Bicep template wires `AzureBlob` + the storage URL via Managed Identity.
- Containers `qr-codes` and `printable-batches` are created by the Bicep template; the local `azurite` Docker service creates them on first write.

### IHostedService over Hangfire
Built-in, no extra dashboard infra. Three jobs: cart sweeper (30s), rebook expirer (60s), day-before reminder (hourly). All idempotent so a missed tick does not corrupt state.

### SignalR (built-in)
Single-instance scale today. For event day with ~600 concurrent users (per spec), Azure SignalR Service in `Default` mode is the swap-in. Wiring is one line: `AddSignalR().AddAzureSignalR(connectionString)`. Not enabled yet because we haven't committed to multi-instance.

### Application Insights
`AddApplicationInsightsTelemetry()` reads `APPLICATIONINSIGHTS_CONNECTION_STRING` from env. Set automatically by the Bicep template via `appInsights.outputs.connectionString`. No-op locally without the env var.

### IHostedService over Hangfire
**Why**: Spec said "Hangfire or IHostedService". IHostedService + `PeriodicTimer` is built-in, requires no extra dashboard infra, and the three jobs we need (cart sweeper, rebook expirer, day-before reminder) don't need durable retries beyond what we add inside the loops. A single Hangfire dependency is overkill.
**Trade-off**: If a host instance crashes mid-job, work isn't durably persisted. We mitigate by making each job idempotent (read DB → act → write log).

### SignalR (built-in) for live seat-map updates
**Why**: Native to ASP.NET Core, no extra service. Single-instance deployment can use in-memory backplane; Azure SignalR Service is the swap-in for scale-out.

### QRCoder + QuestPDF + ClosedXML — exactly as the spec
**License note**: QuestPDF requires `QuestPDF.Settings.License = LicenseType.Community` set early in `Program.cs` for non-commercial use. Captured in code.

### Email: `IEmailService` abstraction
**Implementations shipped**:
- `ConsoleEmailService` — writes the rendered HTML + attachments to `logs/email/<timestamp>-<to>.html`. Default in `Development`.
- `SendGridEmailService` — uses SendGrid REST API when `Email__Provider = "SendGrid"` and `SendGrid__ApiKey` is configured.
**Why two**: Lets local dev see email output without sending real mail; production can swap to SendGrid (or later Azure Communication Services) without touching call sites.

### Blob storage: `IBlobStorage` abstraction
**Implementations shipped**:
- `LocalDiskBlobStorage` — writes to `wwwroot/qr/...`, served as static files. Default everywhere unless overridden.
- `AzureBlobStorage` — placeholder; wired to a connection string but **not implemented** in this pass.
**Why**: QR images need a stable URL for emails. Local disk works for self-hosted / Railway / Azure App Service single-instance.

### Refresh tokens
**Decision**: Added a `RefreshTokens` table not in the schema in spec section 13 (the spec mentions hashed-DB storage in section 6, just doesn't list the table).
**Schema**: `(Id, UserId, UserType, TokenHash, ExpiresAt, RevokedAt, ReplacedByTokenId, CreatedAt)`.

### Frontends: separate from this pass
**Decision**: This commit ships the **complete backend** only. The three React apps (`portal`, `admin`, `scanner`) are scaffolded in subsequent passes — see [README.md](README.md) "Build status".
**Why**: One LLM turn cannot deliver all of section 13–17 with quality. Backend is the foundation; APIs are stable so frontends can plug in next.

## Domain modelling

### Mirror seat calculation
A seat in `VIP AF Row A Seat 5` mirrors `VIP AM Row A Seat 5`. Implementation in `BookingService.GetMirrorZoneCode()`:
- `VIPAF ↔ VIPAM`, `VIPBF ↔ VIPBM`.
- Both seats must already exist in the DB (they're seeded). If one is missing the booking is rejected — never silently allocate a phantom seat.

### Concurrency
`BookingService.ReserveCartAsync` uses `ExecutionStrategy` + an explicit `Serializable` transaction. Inside the transaction:
1. `SELECT` both target seats `WITH (UPDLOCK, ROWLOCK)` (raw SQL, since EF won't emit hints).
2. Verify neither seat has an active `BookingItem` whose `HoldExpiresAt > NOW()` or whose `Booking.Status IN (Cart, Confirmed, RebookWindow)`.
3. Insert the `Booking` + 2 `BookingItem` rows.
4. Commit.

If the seats are taken, throw `ConflictException("seat already held")`. Caller surfaces this to the SignalR hub so the picker can refresh.

### Cart expiry & rebook window enforcement
The hold lives on `BookingItem.HoldExpiresAt`. The `CartSweeperJob` (every 30s) runs:
```sql
UPDATE Bookings
   SET Status = 'Expired'
 WHERE Status = 'Cart'
   AND Id IN (SELECT BookingId FROM BookingItems WHERE HoldExpiresAt < NOW())
```
…then broadcasts SignalR `seat-released` for each affected seat.

`RebookWindowEnforcerJob` (every 60s) closes `RebookWindow` bookings whose `RebookWindowExpiresAt < NOW()` by setting them to `Cancelled` (already-cancelled: just expire the rebook offer).

### QR payload & signing
**Format**: signed JWT, payload `{ tid, eid, typ, zn, sl?, sc, iat, exp }` (short keys to keep QR dense).
- `tid` = ticket UUID (BookingItem.TicketNumber or AdminPass.TicketNumber).
- `typ` = `bk` (BookingItem) or `ap` (AdminPass).
- `sl` = seat label (omitted for AdminPass single-seat zones; present for guest-of-3 if useful).
- `sc` = seats count.
**Signing key**: `Qr__SigningKey` env var, **separate** from auth `Jwt__Secret`. Compromising the auth secret should not let an attacker mint event QRs and vice versa.
**Expiry**: 24h after `Event.EventDate + 12h` (gives gate staff a generous window before tickets expire mid-event).

## Auth specifics

### Initial password format
`{First3LettersOfFirstName}{DDMMYYYY}` from spec section 6. Implemented in `StudentImportService.ComputeInitialPassword`.
**Edge case**: First name shorter than 3 chars (rare). Pad with `X` characters to length 3.
**Edge case**: First name with non-ASCII (Arabic). We take the first 3 *characters* (Unicode-safe), not bytes. Documented in test.

### Role claim mapping
Single JWT scheme, two issuers (student vs admin) distinguished via the `typ` claim:
- `typ=stu` → `roles: [Student]`
- `typ=adm` → `roles: [Admin]`

### Seeded super-admin
`admin@kfs.sch.sa`, password from `Auth__SuperAdminPassword` env var (defaults to `Admin@123` in dev). `MustChangePassword` flag forces a password change on first login.

## Database details

### Indexes added beyond spec section 13
- `Students.Email` UNIQUE
- `Admins.Email` UNIQUE
- `BookingItems(BookingId, ParentRole)` UNIQUE — guarantees max 2 items per booking, one mother + one father
- `AdminPasses(BatchId, SequenceNumber)` UNIQUE — clean batch ordering
- `Events(EventDate)` non-unique — used by `DayBeforeReminderJob`
- `RefreshTokens.TokenHash` UNIQUE

### Seeding
`DbSeeder` runs at startup when `Database__RunMigrationsOnStartup=true`. It creates:
- 1 active `Event` with default `BookingOpensAt=now`, `EventDate=now+30d`, `CartHoldMinutes=10`, `CancellationWindowMinutes=10`.
- 8 zones (4 VIP + Guest + Staff + Media + VVIP).
- 304 VIP seats (4 × 19 × 4 zones).
- 1 super-admin.
- 5 sample students with `@stu.kfs.sch.sa` emails so the admin Excel-upload path can be exercised manually too.

## API surface

### Status codes
- Conflict on seat-already-taken: `409`. Body `{ code: "seat_taken", message, blocked: { zone, row, seat } }`. The portal handler refreshes the map on `code=seat_taken`.
- Validation: `400` with `errors: { field: [messages] }` (FluentValidation collected by `ValidationFilter`).
- Auth: `401` `Unauthorized`, `403` `Forbidden`. The portal redirects on `401`.

### Public scanner token
`/scan/verify` accepts a body `{ qrPayload, eventToken }`. The `eventToken` is a short random string set in `Event.ScannerToken` (rotated by admin). Without a matching token the endpoint returns `401`. This avoids embedding scanner credentials yet stops random internet traffic from probing.

## What is NOT in this commit

These are deferred but tracked:

- React `portal`, `admin`, `scanner` apps (next pass).
- Playwright e2e tests for the paired-booking concurrency case.
- iPad camera mirror-flip handling (lives in `scanner` app — next pass).
- GitHub Actions CI for Azure deploy. Railway-based deploy is documented in [README.md](README.md).
- SendGrid integration is a stub — the API path works but the actual SendGrid call returns `NotImplementedException` until a real account is wired.
- Azure Blob Storage — local-disk shipped; Azure path stubbed.

## Future considerations (not in scope, but flagged)

- **Multi-instance deployments** need the SignalR Azure backplane and the cart-sweeper changed to a single-elected-instance pattern (`IDistributedLock`). Today the system assumes single-API-instance.
- **Translation** — Arabic is hardcoded in email templates. A `IStringLocalizer` pass would let the school customise it without redeploys.
- **Audit logging** — `AuditLogs` table is created but only key admin actions write to it. Comprehensive audit interceptor on EF SaveChanges is a clean follow-up.
