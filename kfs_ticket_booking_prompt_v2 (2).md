# KFS School Event — Ticket Booking & QR Verification Platform (v2)

> **Prompt for Claude Code.** Act as a senior solution architect *and* full-stack developer. Build this end-to-end. Make pragmatic decisions where unspecified, document them in `DECISIONS.md`, and ship a runnable system with seed data.
>
> ⚠️ **This is v2 — a major revision.** Earlier drafts assumed students booked 5 tickets (2 parent + 3 public). That is **wrong**. Students only book the 2 parent seats. All other zones are admin-managed printable QR cards.

---

## 1. Project Overview

A school event ticketing platform for KFS (Saudi school, student email domain `stu.kfs.sch.sa`). The system has two completely separate flows:

1. **Student-driven parent seat booking** — each student logs in and books a paired set of seats for their mother and father in the VIP zones. Seats are gender-segregated but auto-paired (same row, same seat number, opposite gender side). Student receives QR-coded tickets by email.

2. **Admin-driven printable QR generation** — for VVIP, Guests, Staff, and Media. No login, no email. Admin clicks "Generate" → system creates batches of QR codes → admin prints them onto pre-designed cards. The QR codes are valid in the database for gate scanning.

A public-facing scanner web page (works on iPad camera) decodes the QR at the gate, validates against the database, and displays a welcome message with the zone the holder belongs to.

**The system's job ends at QR generation and validation.** Card graphic design is handled externally by a designer.

---

## 2. Tech Stack

Use this stack unless you hit a specific blocker (justify any deviation in `DECISIONS.md`):

- **Backend:** .NET 8 Web API, C#, Entity Framework Core
- **Frontend:** React 18 + TypeScript + Vite, TailwindCSS, shadcn/ui
- **Database:** **PostgreSQL 16** with `Npgsql.EntityFrameworkCore.PostgreSQL` provider
- **Real-time:** SignalR for live seat-map updates
- **Auth:** JWT (access + refresh), BCrypt password hashing
- **Email:** SendGrid (abstract behind `IEmailService` so a different provider can swap in)
- **QR generation:** `QRCoder` NuGet package
- **PDF generation (printable QR sheets):** `QuestPDF`
- **Excel parsing:** `ClosedXML` for student roster uploads
- **Scanner camera:** `html5-qrcode` (must support iPad rear camera with mirror correction)
- **Hosting target:** **Microsoft Azure**. Primary deployment region: **UAE North (Dubai)** — chosen as the nearest GA Azure region to Riyadh, since **Azure Saudi Arabia East does not reach general availability until Q4 2026**. Plan a migration path to Saudi East when it opens. All resources provisioned together:
  - `api` → **Azure App Service for Linux** (.NET 8 stack), deployed from `/src/KFS.Api` Dockerfile (or built-in stack runtime — pick in `DECISIONS.md`).
  - `postgres` → **Azure Database for PostgreSQL — Flexible Server** (Burstable B2s tier is sufficient for a single-event load; scale up if running multiple events). Enable HA standby in same region for event day.
  - `portal` / `admin` / `scanner` → **Azure Static Web Apps** (one per frontend), with route-based API proxy back to the App Service.
- **Object storage:** **Azure Blob Storage** account in UAE North for generated QR PNGs and printable PDF/ZIP outputs. Containers: `qr-codes`, `printable-batches`. Use Managed Identity from the App Service to access (no connection string in app config). Abstract behind `IBlobStorage`.
- **Secrets:** **Azure Key Vault** in UAE North. App Service reads via Managed Identity. Never put secrets in App Settings except for Key Vault references (`@Microsoft.KeyVault(...)`).
- **Containerisation:** `docker-compose.yml` for local dev (API + PostgreSQL 16 + **Azurite** as Azure Blob stand-in + frontend apps).
- **Time zone:** application default is **`Asia/Riyadh`** (UTC+3, no DST — Saudi Arabia does not observe DST). All scheduled jobs (day-before reminder, cart sweeper) compute against this zone. Store all datetimes as `timestamptz` and convert to `Asia/Riyadh` for UI, emails, and reports.
- **Data residency & compliance:** the school is in Riyadh, KSA — **Saudi Personal Data Protection Law (PDPL)** applies. Since hosting is in UAE North until Saudi East is GA, document a cross-border data transfer mechanism in `DECISIONS.md` (Saudi PDPL allows transfers to countries with adequate protection — UAE has reciprocal arrangements through GCC frameworks; confirm with the school's legal contact). Sign a Data Processing Addendum (DPA) with Microsoft. Disable cross-region replication beyond UAE/Saudi geography. Plan to migrate the Postgres + Blob + Key Vault to Saudi East once GA.

**PostgreSQL-specific guidance for Claude Code:**
- Use `snake_case` table and column naming via `UseSnakeCaseNamingConvention()` (EFCore.NamingConventions package), OR keep EF defaults — pick one and document in `DECISIONS.md`.
- Enable the `citext` extension for case-insensitive email columns.
- Enable `pgcrypto` for `gen_random_uuid()` if using UUID primary keys (recommended over `int` identity for this project — easier to expose IDs in QR payloads without leaking row counts).
- Use `timestamptz` (timestamp with time zone) for all datetime columns; never `timestamp` without timezone.
- Use `jsonb` (not `json`) for `AuditLogs.MetadataJson`.

**Azure-specific guidance for Claude Code:**
- Provision via **Bicep templates** in `/infra/` — one main template + parameter files for `dev` and `prod` environments. Use the AVM (Azure Verified Modules) where possible.
- App Service: enable Managed Identity, configure Key Vault references for connection strings and secrets, set `WEBSITE_TIME_ZONE=Arabian Standard Time`, enable Application Insights for telemetry, set the health check path to `/healthz`.
- Postgres Flexible Server: enable the `pg_trgm`, `citext`, and `pgcrypto` extensions via the `azure.extensions` server parameter. Connection from App Service uses VNet integration + private endpoint (no public access). Use AAD authentication for the App Service Managed Identity rather than password auth.
- Storage account: container access set to `Private`. Generate short-lived SAS tokens server-side when serving QR images to authenticated students; for the public scanner page, embed QR validation in the API response rather than exposing blob URLs directly.
- SignalR: for production scale, use **Azure SignalR Service** in `Default` mode rather than self-hosted; for the expected ~600 concurrent users this is optional but cheap insurance for event day.
- CDN / WAF: front the App Service with **Azure Front Door** if expecting traffic spikes; otherwise direct App Service exposure with rate limiting in code is fine for school-scale traffic.
- Deploy via **GitHub Actions** with OIDC federated credentials (no service principal secrets). One workflow file per environment.
- Local dev: use the Azure CLI + `az login` for developer access to dev Key Vault and Storage. Use **Azurite** Docker image for local blob emulation so devs don't hit cloud storage during development.

---

## 3. Venue & Zones — UPDATED LAYOUT

The venue has a stage at the front. Behind the stage are two parent groups (Group A and Group B), each split down the middle into **Female (left/pink)** and **Male (right/blue)** halves. Surrounding zones are general/admin-issued.

### 3.1 Parent Zones (student-bookable)

| Zone | Group | Side | Layout | Seats |
|---|---|---|---|---|
| **VIP A — Female** | Group A | Female (pink) | 4 rows × 19 seats | 76 |
| **VIP A — Male** | Group A | Male (blue) | 4 rows × 19 seats | 76 |
| **VIP B — Female** | Group B | Female (pink) | 4 rows × 19 seats | 76 |
| **VIP B — Male** | Group B | Male (blue) | 4 rows × 19 seats | 76 |

**Total parent seating: 304** (152 in Group A, 152 in Group B).

Naming convention on tickets (matches WhatsApp clarification):
- Mother's ticket: `Block: VIP AF`, `Row: A`, `Seat: 1`
- Father's ticket: `Block: VIP AM`, `Row: A`, `Seat: 1`
- (Or `VIP BF` / `VIP BM` for Group B)

### 3.2 Admin-Issued QR Zones (no booking, just generation)

| Zone | Total Seats | QR Cards | Seats per QR |
|---|---|---|---|
| **Guest Zone** | 600 | 200 printable QR codes | 3 seats per code |
| **Staff Zone** | 100 | 100 printable QR codes | 1 seat per code |
| **Media Zone** | 100 | 100 printable QR codes | 1 seat per code |
| **VVIP Zone** | 100 | 100 printable QR codes | 1 seat per code |

VVIP is NOT on the physical seat map — it's a logical zone for QR generation only. Other zones appear on the map for visitor orientation but have no per-seat picking — the gate staff just direct holders to the right area.

**Total event capacity: 1,204** (304 parents + 600 guests + 100 staff + 100 media + 100 VVIP).

A seed migration must populate every reserved seat row in the database at startup (304 seats for VIP A/B). Admin-issued QR zones are populated when admin clicks "Generate".

---

## 4. Critical Booking Rule — Paired Seats

When a student picks ONE seat for their mother (or father), the system **automatically books the mirror seat on the opposite gender side, in the same group, same row, same seat number**.

- Student picks: `VIP A Female, Row A, Seat 5` (for mother)
- System auto-assigns: `VIP A Male, Row A, Seat 5` (for father)
- Both seats must be available — if the mirror seat is taken, the student cannot pick the original.
- Both bookings happen in the same atomic transaction.
- Mother and father receive **separate emails** with separate QR tickets, but the bookings are linked by a single `BookingId`.

A student picks the **side** (start with mother OR father) and the **group** (A or B), then picks a row+seat. The mirror is calculated and reserved server-side.

Both seats must always be in the **same group** — never one in Group A and one in Group B.

---

## 5. User Roles

1. **Student** — only role with login credentials. Books the parent pair only. Email format: `{name}@stu.kfs.sch.sa`.
2. **Admin** — manages roster upload, generates printable QR batches for VVIP/Guest/Staff/Media, runs reports, sends reminders, monitors live seat map.
3. **Gate Scanner Operator** — opens a public scanner URL on an iPad or phone, scans QR codes, sees zone/seat info. No login required (URL contains a time-limited event token).

**No accounts** for VVIP, Guest, Staff, or Media holders. They receive pre-printed cards.

---

## 6. Authentication & Account Provisioning

- **No public signup.** Student accounts are created exclusively by admin Excel upload.
- **Excel upload format:**
  - Columns: `StudentEmail` (must end `@stu.kfs.sch.sa`), `FirstName`, `LastName`, `DateOfBirth` (DD-MM-YYYY), `GradeOrClass` (optional)
  - Validation: reject duplicate emails, malformed emails, missing DOB. Show per-row error report in the UI before final import.
- **Auto-generated initial password:** `{First3LettersOfFirstName}{DDMMYYYY}` — e.g. student `Safa Albuhairan` born `15-03-2010` → password `Saf15032010`. Force password change on first successful login.
- **JWT auth:** 15-min access token, 7-day refresh token. Refresh tokens stored hashed in DB.
- **Admin account:** seeded super-admin in initial migration (`admin@kfs.sch.sa` / env-var password). Admin login is a separate endpoint.

---

## 7. Booking Rules (student flow)

1. Each student is allocated exactly **2 tickets** — one mother seat, one father seat. They are auto-paired (see Section 4).
2. **Cart hold:** when a student selects a female-side seat (which auto-pairs the male mirror), both seats are held for **10 minutes**. A background job releases expired holds. Live countdown displayed in UI.
3. **Cancellation window:** if a student cancels their confirmed booking, both seats return to the pool, and the student gets a **10-minute re-booking window** to pick replacements. After 10 mins, allocation is forfeited (configurable in admin settings).
4. **Concurrency:** wrap the paired-seat reserve in a transaction with `IsolationLevel.Serializable` and use `SELECT ... FOR UPDATE` (via `EF.Property` + raw SQL or `dbContext.Database.ExecuteSqlInterpolatedAsync`) to row-lock both the chosen seat and its mirror before insert. Both seats either reserve atomically or both fail. Handle PostgreSQL serialization failures (SQLState `40001`) with a single retry.
5. **No cross-group booking** — both seats must be in the same group (A or B).
6. **One active booking per student** — a student cannot have two simultaneous confirmed bookings.

---

## 8. Admin-Issued QR Generation (no booking, just printing)

For VVIP, Guest, Staff, Media — each has a "Generate Batch" button in the admin console:

- **Guest:** generates 200 QR codes, each represents 3 seats (the QR holder brings 3 people).
- **Staff:** generates 100 single-seat QR codes.
- **Media:** generates 100 single-seat QR codes.
- **VVIP:** generates 100 single-seat QR codes.

Each generated batch:
- Stored in DB with `Type`, `BatchId`, `SequenceNumber`, `QrCodePayload`, `SeatsCount`, `CreatedAt`, `IssuedToName` (nullable — admin can fill in later).
- Downloadable as a **printable PDF** (A4 layout, multiple QR codes per page) so the designer can lift just the QR images, OR as a ZIP of individual PNG files for the designer to drop into their card design.
- A "Print Specification" toggle on the generation modal lets admin choose: PDF sheet vs ZIP of PNGs.
- All generated QRs are immediately valid for scanning at the gate.

**No emails sent for these zones** — the only output is downloadable files.

---

## 9. QR Code Strategy

- **Payload:** signed JWT containing `{ ticketId, eventId, type, zone, seatLabel?, seatsCount, iat, exp }` signed with `QR_SIGNING_KEY` (separate from auth JWT secret). Expires 24 hours after event end.
- **Render:** PNG, 300×300, error correction level Q. Stored in **Azure Blob Storage** (UAE North) at `qr-codes/{eventId}/{ticketNumber}.png` (or local **Azurite** emulator in dev). Behind `IBlobStorage` abstraction. Served to authenticated clients via short-lived SAS URLs (5-minute expiry); never expose container as public.
- **Validation:** scanner posts payload → API verifies signature, expiry, looks up the corresponding `BookingItem` or `AdminPass`, writes a `ScanLog` entry, returns rich response (zone, name, seat label, already-scanned flag).

---

## 10. Email Workflow (students only)

Replicate the ticket sample exactly (the screenshot you showed):

```
#****{last6OfTicketNumber}
CATEGORY: A   (or B)
GATE: Gate A
BLOCK: VIP AF (or VIP AM, VIP BF, VIP BM)
SEAT: 12
ROW: A
المقاعد المحجوزة: A12 (Arabic line — single seat per ticket; mother's ticket shows mother's seat only, father's shows father's)
QR Code: [PNG]
Footer: "Ticket is sent to {studentEmail} and pending approval by receiver"
```

- One email per ticket → student receives **2 emails** at checkout (one for mother's ticket, one for father's), both arriving at the student's `@stu.kfs.sch.sa` address.
- HTML email with inline CSS, embedded base64 QR, RTL Arabic snippet correctly rendered.
- Admin can resend either ticket from the admin console.
- "Resend my tickets" button in the student portal.
- All sends logged to `AuditLogs`.

---

## 11. Reminders

The system must send two types of reminders:

1. **Unbooked-student reminder.** Admin clicks "Send Reminder to Unbooked" → email sent to every student whose `Bookings.Status != Confirmed`. Customisable subject + body template stored in admin settings. Can be sent on-demand or scheduled.
2. **Day-before reminder.** Automatically sent 24 hours before `Event.EventDate` to **all confirmed bookers**, including:
   - Reminder of QR ticket — re-attach both QR images.
   - Event date, time, venue address, and a Google Maps link.
   - Any free-text note from admin (e.g. parking instructions).

Use a Hangfire / `IHostedService` schedule to fire the day-before reminder. Track sends in `ReminderLogs` so we don't double-send.

---

## 12. Reporting

Admin must be able to generate two key reports:

1. **Group A Booked Seats Report** — every confirmed seat in VIP A (Female + Male), with parent name (derived from the student's record + a label "Mother of {StudentName}" / "Father of {StudentName}"), seat label, row, side. Exportable as Excel and PDF.
2. **Group B Booked Seats Report** — same as above for VIP B.

Additional dashboards:
- Booking funnel: `Students total → logged in → in cart → confirmed → cancelled`.
- Per-zone totals: how many seats sold/issued in each zone.
- Live scan stats on event day.

All reports respect the format the school has already established (column order: Row, Seat, Side, Parent Name, Linked Student, Email, Booked At). Export buttons for CSV / Excel / PDF.

---

## 13. Database Schema (key tables)

```
Students        (Id, Email, FirstName, LastName, DateOfBirth, PasswordHash, MustChangePassword, IsActive, CreatedAt)

Admins          (Id, Email, PasswordHash, Role, CreatedAt)

Events          (Id, Name, EventDate, Venue, VenueAddress, MapLink,
                 IsActive, BookingOpensAt, BookingClosesAt,
                 CartHoldMinutes, CancellationWindowMinutes,
                 ReminderDayBeforeSent, ReminderNoteFromAdmin)

Zones           (Id, EventId, Code [VIPAF|VIPAM|VIPBF|VIPBM|GUEST|STAFF|MEDIA|VVIP],
                 DisplayName, Group [A|B|null], Side [Female|Male|null],
                 IsReservedSeating, Capacity)

Seats           (Id, ZoneId, RowLabel, SeatNumber, FullLabel)
                -- only populated for VIP zones (304 rows after seed)

Bookings        (Id, StudentId, EventId,
                 Status [Cart|Confirmed|Cancelled|Expired|RebookWindow],
                 GroupChosen [A|B], CreatedAt, ConfirmedAt, CancelledAt,
                 RebookWindowExpiresAt)

BookingItems    (Id, BookingId, ZoneId, SeatId, ParentRole [Mother|Father],
                 TicketNumber, QrCodePayload, QrCodeImageUrl,
                 EmailSent, EmailSentAt, HoldExpiresAt)
                -- 2 rows per booking, both linked by BookingId

AdminPasses     (Id, EventId, Type [VVIP|Guest|Staff|Media],
                 BatchId, SequenceNumber, TicketNumber,
                 QrCodePayload, QrCodeImageUrl, SeatsCount,
                 IssuedToName [nullable], IssuedByAdminId, IssuedAt)

ScanLogs        (Id, ScannedItemType [BookingItem|AdminPass], ItemId,
                 ScannedAt, ScannerIp, DeviceInfo,
                 Result [Valid|AlreadyUsed|Invalid|Expired])

ReminderLogs    (Id, EventId, StudentId, Type [Unbooked|DayBefore],
                 SentAt, EmailMessageId)

PasswordResets  (Id, StudentId, Token, ExpiresAt, Used)

AuditLogs       (Id, ActorType, ActorId, Action,
                 EntityType, EntityId, MetadataJson, Timestamp)
```

**Indexes:**
- `BookingItems.QrCodePayload` (unique)
- `AdminPasses.QrCodePayload` (unique)
- `BookingItems.HoldExpiresAt` (cleanup job)
- `Seats.ZoneId+RowLabel+SeatNumber` (unique)

---

## 14. API Endpoints (REST, all `/api/v1`)

**Auth**
- `POST /auth/login` — student or admin
- `POST /auth/refresh`
- `POST /auth/change-password`
- `POST /auth/forgot-password` / `POST /auth/reset-password`

**Student**
- `GET  /me` — profile + booking status
- `GET  /events/active` — current event
- `GET  /events/{id}/seatmap?group=A|B` — full seat map for chosen group, both sides, with availability
- `POST /cart/select` — `{ group, side, rowLabel, seatNumber }` → server reserves the chosen seat AND its mirror, returns hold expiry
- `DELETE /cart` — release current cart
- `GET  /cart` — current cart with countdown
- `POST /cart/checkout` — confirm booking, generate 2 QRs, send 2 emails
- `GET  /bookings` — my bookings (always 0 or 1 active)
- `POST /bookings/{id}/cancel` — triggers 10-min re-book window
- `POST /bookings/{id}/resend-emails`

**Admin — students**
- `POST /admin/students/upload` — Excel file, returns row-level result
- `GET  /admin/students?search=&status=` — paginated
- `PATCH /admin/students/{id}` — deactivate, reset password
- `POST /admin/students/{id}/reset-password`

**Admin — passes**
- `POST /admin/passes/generate` — `{ type: VVIP|Guest|Staff|Media, count, format: PDF|ZIP }` → returns download link
- `GET  /admin/passes` — list batches with download links
- `PATCH /admin/passes/{id}` — set `IssuedToName`

**Admin — bookings & seat map**
- `GET  /admin/bookings?group=&status=`
- `GET  /admin/seatmap?group=A|B` — live, both sides, with occupant info
- `POST /admin/bookings/{id}/force-cancel`

**Admin — reports & reminders**
- `GET  /admin/reports/group/{A|B}?format=pdf|xlsx|csv`
- `POST /admin/reminders/unbooked` — sends reminder, optionally with custom body
- `GET  /admin/reminders/logs`

**Admin — event config**
- `GET  /admin/event` / `PUT /admin/event` — event config

**Public scanner**
- `POST /scan/verify` — `{ qrPayload, eventToken }` → `{ valid, type, zone, seatLabel?, name?, alreadyScanned, scannedAt? }`

**Real-time (SignalR `/hubs/seatmap`)**
- Server pushes `seat-held`, `seat-released`, `seat-booked` to subscribed clients.

---

## 15. Frontend Architecture & Patterns

This applies to all three React apps (`portal`, `admin`, `scanner`). Build them in a single **pnpm workspaces monorepo** under `/web` so they share a common UI library, API client, and types.

### 15.1 Monorepo & Tooling

- **Workspace manager:** pnpm with workspaces.
- **Build tool:** Vite per app.
- **Task runner:** **Turborepo** for caching builds, lints, tests across the three apps.
- **Language:** TypeScript with `strict: true` and `noUncheckedIndexedAccess: true`.
- **Linting:** ESLint with `@typescript-eslint`, `eslint-plugin-react`, `eslint-plugin-react-hooks`, `eslint-plugin-jsx-a11y`. Prettier for formatting. Husky + lint-staged on commit.
- **Node:** pinned via `.nvmrc` (Node 20 LTS).

```
/web
  /apps
    /portal               Student-facing
    /admin                Admin console
    /scanner              Scanner PWA
  /packages
    /ui                   Shared shadcn/ui components + design tokens
    /api-client           Generated TS client from OpenAPI + auth interceptors
    /types                Shared DTO types (also generated from OpenAPI)
    /hooks                Shared React hooks (useAuth, useSeatMap, useSignalR)
    /i18n                 Translation files + i18next setup
    /utils                Date formatting (Asia/Riyadh), validation schemas
  pnpm-workspace.yaml
  turbo.json
  tsconfig.base.json
```

### 15.2 Type-Safe API Client

The .NET API exposes a Swagger / OpenAPI 3 spec at `/swagger/v1/swagger.json`. The `@packages/api-client` workspace runs **`openapi-typescript-codegen`** (or `orval`) at build time to generate:

- Strongly-typed TypeScript request/response types.
- A typed Axios-based client with auth interceptors (auto-attach JWT, auto-refresh on 401).

Apps never write HTTP calls by hand — always go through the generated client. CI fails if the OpenAPI spec drifts from the committed types.

### 15.3 State Management

- **Server state:** **TanStack Query (React Query) v5** for all API data — handles caching, background refetch, optimistic updates, and pagination. Use query keys derived from API paths.
- **Client / UI state:** **Zustand** for cross-component UI state (auth user, current cart hold, scanner offline queue). Avoid Redux — overkill for this scope.
- **Form state:** **React Hook Form** with **Zod** schema validation. Share Zod schemas between apps where applicable (e.g. login schema). The Zod schemas should mirror the .NET FluentValidation rules so error messages stay consistent.
- **Realtime:** **`@microsoft/signalr`** wrapped in a `useSeatMapHub` hook that exposes `useSyncExternalStore`-style subscriptions. The hook reconnects on visibility change and exposes `seatHeld`, `seatReleased`, `seatBooked` events. React Query `queryClient.setQueryData` is updated on each event so the UI never goes stale.

### 15.4 Routing

- **React Router v6** with code-split routes (`React.lazy` + Suspense).
- Route-level auth guards: `<RequireAuth role="student" />` and `<RequireAuth role="admin" />` HOCs that read from the Zustand auth store.
- Public routes: scanner page, login, password reset, password change.
- Use `loader` + `action` patterns where they keep code clean, otherwise stick with React Query inside components.

### 15.5 Authentication on the Client

- **Access token (JWT, 15 min):** stored in **memory only** (Zustand store). Lost on refresh — that's fine; refresh token recovers it.
- **Refresh token (7 days):** stored in an **HttpOnly, Secure, SameSite=Strict cookie** set by the API. Never accessible to JS.
- **Refresh flow:** Axios response interceptor catches 401, calls `/auth/refresh`, retries the original request once. If refresh also 401s, redirect to login.
- **Force password change:** if `MustChangePassword === true` in the JWT claims, redirect to a forced-change screen and block all other routes until done.
- **Logout:** call `/auth/logout` (clears cookie server-side), then clear in-memory state and redirect.

### 15.6 Internationalisation & RTL

- **Library:** **i18next** + **react-i18next**.
- **Languages:** English (`en`) and Arabic (`ar`). Default to Arabic for the student portal (school audience), English for admin (developer/staff audience), and Arabic for the scanner welcome message. Each app picks its default; user can switch via a header dropdown.
- **RTL handling:** when language is `ar`, set `<html dir="rtl" lang="ar">` and `document.body.classList.add('rtl')`. Tailwind v3.3+ supports logical properties (`ps-` / `pe-` instead of `pl-` / `pr-`) — use those throughout to avoid manual mirroring.
- **Numerals:** use Western Arabic numerals (1, 2, 3) for seat numbers — never Arabic-Indic — for clarity at the gate.
- **Dates:** format via `Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Riyadh' })` for English, `ar-SA` for Arabic. Wrap in a `formatEventDate(date, locale)` util in `@packages/utils`.
- **Email templates** (server-side) follow the same pattern: pick template by student's `PreferredLocale` field (default `ar`).

### 15.7 Design System — KFS Brand

The school's official brand guideline (`brand_guideline.pdf`) defines the palette and typography. Honour it in `@packages/ui` as Tailwind theme extensions.

**Color palette** (from the brand book):

| Role | Hex | Usage |
|---|---|---|
| **Primary — Forest** | `#0d3128` | Primary buttons, headers, navigation, ticket card text, "Block: VIP A" labels, brand-strong UI chrome |
| **Secondary — Sage** | `#548b7d` | Secondary buttons, hover states, info chips, subtle backgrounds, secondary headings |
| **Accent — Olive Gold** | `#a08b16` | Highlights, the "Category" badge on the ticket card, decorative accents, the school's heritage-feel |

Tailwind config (`@packages/ui/tailwind.config.ts`):

```ts
colors: {
  kfs: {
    forest: { DEFAULT: '#0d3128', 50: '#f0f5f3', 100: '#dce9e5', 500: '#1d4f43', 600: '#163e34', 700: '#0d3128', 800: '#0a2620', 900: '#061814' },
    sage:   { DEFAULT: '#548b7d', 50: '#f3f7f6', 100: '#e0eae7', 500: '#548b7d', 600: '#456f64', 700: '#365650' },
    gold:   { DEFAULT: '#a08b16', 50: '#fbf8e9', 100: '#f5edc4', 500: '#a08b16', 600: '#806f12', 700: '#60530d' },
  }
}
```

**Semantic / status colors** — kept distinct from brand palette so a "valid scan" green doesn't get confused with KFS forest-green chrome:

- **Success / valid scan:** `emerald-500` (`#10b981`) — bright, distinct from KFS forest.
- **Warning / already-scanned:** **KFS gold** (`#a08b16`) — brand-aligned warning.
- **Error / invalid:** `red-600` (`#dc2626`) — universal stop signal, kept neutral.

**Typography** (from the brand book):

- **English:** **Source Sans Variable** (Bold + Regular). Free, open source. Install via `@fontsource-variable/source-sans-3`. Apply as `font-sans` default in Tailwind.
- **Arabic:** **Janna LT** (Bold + Regular) per the brand book. ⚠️ **This is a commercial Linotype/Monotype font, not free.** Decision required (capture in `DECISIONS.md`):
  1. Acquire a Janna LT web font licence and self-host the WOFF2 files (preferred — preserves brand fidelity).
  2. Use a free near-equivalent: **IBM Plex Sans Arabic** (`@fontsource/ibm-plex-sans-arabic`) or **Tajawal** (`@fontsource/tajawal`) — note the deviation in `DECISIONS.md` and flag for the school's approval.
- Tailwind `fontFamily` config:
  ```ts
  fontFamily: {
    sans:   ['"Source Sans 3 Variable"', 'system-ui', 'sans-serif'],
    arabic: ['"Janna LT"', '"IBM Plex Sans Arabic"', 'Tahoma', 'sans-serif'], // graceful fallback
  }
  ```
- Apply `font-arabic` automatically when `<html lang="ar">` is set.

**Logo & marks:**

- The brand book provides three logo lock-ups: full lock-up (Arabic + English wordmark + portrait emblem), Arabic-only, English-only. Place SVG copies under `@packages/ui/assets/logos/` and expose as `<KfsLogo variant="full|arabic|english|emblem" />`.
- The "S" wave watermark is a recurring brand motif — use sparingly as a 5–10% opacity background on confirmation pages and email headers, not on every screen.

**Tone & feel:**

- Conservative, institutional, heritage-forward. Generous white space. Minimal ornamentation.
- Rounded corners: `rounded-md` default, never large pill radii on primary surfaces.
- Shadows: subtle (`shadow-sm` / `shadow-md`); avoid heavy modern drop-shadows.
- Match the seriousness of the printed school communications, not a consumer SaaS aesthetic.

**Domain components** (build once in `@packages/ui`, reuse everywhere):

- `<TicketCard />` — visual ticket replicating the email layout, themed in KFS forest+gold.
- `<SeatGrid />` — interactive SVG grid for VIP seat picking. Available seats in `kfs-sage-100`, held in `kfs-gold-100`, booked in `kfs-forest`, blocked in `neutral-300`.
- `<SeatPair />` — read-only display showing the linked mother+father seats.
- `<QrPreview />` — QR image with download/copy actions.
- `<ZoneBadge />` — color-coded chip for VIP AF / VIP AM / VIP BF / VIP BM / Guest / Staff / Media / VVIP. Each zone gets a distinct shade derived from the KFS palette.
- `<CountdownPill />` — for cart hold and re-book windows. Uses `kfs-gold` when over 1min remaining, `red-600` when under.
- `<EmptyState />`, `<ErrorBoundary />`, `<LoadingPanel />` — required everywhere a query runs.
- `<KfsLogo />` — props: `variant`, `monochrome`.
- `<BrandWatermark />` — the "S" wave decorative motif at low opacity.

### 15.8 Forms & Validation Patterns

- Every form: React Hook Form + zodResolver + visible inline errors + disabled submit while pending.
- File upload (Excel roster): drag-drop zone, preview first 10 parsed rows in a table, show server validation errors per row before final submit.
- Optimistic UI for cart actions (add seat, remove seat) with rollback on server error.
- Toast notifications via **sonner** for non-blocking feedback (success/error/info).

### 15.9 Accessibility

- All interactive elements keyboard-accessible.
- Seat grid has both pointer and arrow-key navigation; current focus seat is announced via `aria-live="polite"`.
- Colour is never the only signal — every status has an icon and label.
- Target WCAG 2.1 AA contrast.
- Test with `eslint-plugin-jsx-a11y` and a Lighthouse a11y check in CI.

### 15.10 Scanner PWA Specifics

- **Manifest** (`public/manifest.webmanifest`): standalone display, KFS branding, 192x192 + 512x512 icons.
- **Service worker** via **Vite PWA plugin** (`vite-plugin-pwa`):
  - Precache app shell.
  - Runtime cache: scan endpoint with `NetworkFirst` strategy.
- **Offline queue:** scans made while offline are stored in **IndexedDB via Dexie** in a `pending_scans` table. A background sync (or visibility-change handler) flushes the queue when online. UI shows a small "N scans pending sync" indicator.
- **Camera handling:** `html5-qrcode` configured for rear camera (`facingMode: { exact: "environment" }`) with fallback to default. CSS `transform: scaleX(-1)` toggle button for iPad mirror correction. Audio cue via `Audio` API on each scan.
- **Reduced motion:** respect `prefers-reduced-motion` for the success/fail flash animation.

### 15.11 Performance

- Route-based code splitting; each app's initial JS payload < 200 KB gzipped.
- Image lazy-loading via native `loading="lazy"`.
- React Query `staleTime` tuned per resource (seat map: 0 = always fresh; student profile: 5 min).
- Memoise expensive seat grid renders with `React.memo` + stable seat keys.
- Bundle analysis via `rollup-plugin-visualizer` checked into PR builds.

### 15.12 Testing

- **Unit / component:** **Vitest** + **React Testing Library**. Coverage target >=70% on shared packages.
- **Integration:** Mock Service Worker (MSW) for API stubbing in tests.
- **E2E:** **Playwright** in `/tests/e2e`. Critical flows:
  - Student logs in, force-change password, picks seat, mirror auto-paired, checks out, downloads ticket.
  - Two parallel students racing for the same seat (concurrency).
  - Admin uploads Excel and student record appears.
  - Admin generates Guest batch, scans one of the QRs, sees "Group of 3" message.
- Visual regression on the `<TicketCard />` to lock in the email-matching layout.

### 15.13 Build & Deploy

- Each app: `pnpm --filter portal build` produces a static `dist/`.
- **Azure Static Web Apps:** deploy each app independently via GitHub Actions; configure the SWA to proxy `/api/*` to the App Service.
- **CSP headers** set via `staticwebapp.config.json`: only allow API + SignalR + Azure Blob origin.
- **Environment variables:** `VITE_API_BASE_URL`, `VITE_SIGNALR_URL`, `VITE_SCANNER_TOKEN_KEY` — injected at build time per environment. No runtime env access in the browser bundle.

---

## 16. Admin Console — UI

Pages required:

1. **Dashboard** — capacity per zone, booked %, cancellations today, scans today (live).
2. **Students** — table, upload button, per-row actions (reset password, view booking, deactivate).
3. **Excel upload modal** — drag-drop, preview first 10 rows, validation report, confirm import.
4. **Live Seat Map** — toggle between Group A and Group B, both sides shown side-by-side (Female | Male), colour-coded (Available / Held / Booked / Cancelled). Click seat → see occupant, ticket #, scan status.
5. **Bookings list** — filterable, exportable.
6. **Generate Passes** — for each of VVIP / Guest / Staff / Media:
   - Show counter of how many already generated.
   - "Generate Batch" button → modal asking count + output format (PDF sheet / ZIP of PNGs) → downloads file.
   - Lists past batches with re-download links.
7. **Reports** — Group A / Group B reports with date filter, export buttons.
8. **Reminders** — buttons for "Send to unbooked now" and "Customise day-before message"; log of past sends.
9. **Event Settings** — booking window, cart hold minutes, cancellation window, day-before reminder note.

---

## 17. Student Portal — UI

- **Dashboard:** booking status (none / cart / confirmed / cancelled with re-book countdown), event details.
- **Group Chooser:** "Pick your group: A or B" — once chosen and confirmed, locked for this booking.
- **Side Chooser:** "Start by picking a seat on the Female (Mother) side or Male (Father) side." (Doesn't matter which — the mirror is auto-paired.)
- **Seat Picker:** SVG grid showing the chosen group's chosen side. Hover shows seat label. Click to add to cart. Real-time updates from SignalR. Mobile-friendly.
- **Cart:** shows both the chosen seat AND the auto-paired mirror seat with countdown timer. "Confirm Booking" CTA.
- **Confirmation:** success screen with both ticket previews + download links.
- **My Bookings:** download both QRs, resend emails, cancel button (with modal explaining the 10-min re-book rule).

Mobile-first. Arabic RTL support for any displayed Arabic text.

---

## 18. Scanner Page — UI

Public URL: `/scan?token={eventScanToken}` (token issued by admin, time-limited).

- Uses `getUserMedia` + `html5-qrcode` for camera access.
- **Critical:** handle iPad camera flip — when the rear camera feed is mirrored (front-camera mode), apply a CSS `transform: scaleX(-1)` correction. Provide a manual "Flip camera" button as fallback.
- **Scan response display:**
  - ✅ Big green panel: "Welcome! Zone: {zone}, Seat: {seatLabel or 'General Seating'}, Holder: {name or 'Guest'}". For Guests: "Group of 3 — please admit 3 people."
  - ⚠️ **Gold (KFS warning):** "Already scanned at {time}".
  - ❌ Red: "Invalid / expired QR — direct to help desk".
- Audio cue per state.
- Offline-tolerant: queue scans in IndexedDB if network drops, sync when reconnected.
- Big-finger tap targets, high contrast, designed for noisy event entry.

---

## 19. Background Jobs

Use Hangfire or `IHostedService`:

- **Cart expiry sweeper** — every 30 seconds. Marks expired holds, broadcasts SignalR `seat-released`.
- **Re-book window enforcer** — every 60 seconds. Closes expired re-book windows and forfeits allocation.
- **Day-before reminder** — runs hourly, fires when `Event.EventDate - 24h` window opens, sends emails, updates `Event.ReminderDayBeforeSent = true`.
- **Email retry** — exponential backoff for failed sends.

---

## 20. Security & Hardening

- Rate-limit all auth endpoints (`AspNetCoreRateLimit`).
- HTTPS-only, HSTS, secure cookies.
- CSRF protection on state-changing endpoints.
- Excel upload validation: max 5 MB, MIME sniff, content scan, row cap.
- Sanitise all user-supplied strings before email rendering (XSS).
- All admin actions written to `AuditLogs`.
- Secrets via **Azure Key Vault** (UAE North), accessed by App Service Managed Identity. Never committed.
- Input validation with FluentValidation.
- Scanner endpoint rate-limited per IP.
- Camera permission request flow with clear UX.

---

## 21. Project Structure

```
/src
  /KFS.Api                .NET 8 Web API
    /Controllers
    /Services
    /Hubs                 SignalR
    /Jobs                 Background workers
    /Email/Templates
    /Reports              QuestPDF / ClosedXML report generators
  /KFS.Domain             Entities, value objects
  /KFS.Infrastructure     EF Core DbContext, migrations, repositories
  /KFS.Application        Use cases, DTOs, validators
/web                      pnpm workspaces monorepo (see section 15.1)
  /apps/portal            Student portal (Vite + React)
  /apps/admin             Admin console (Vite + React)
  /apps/scanner           Scanner PWA (Vite + React + vite-plugin-pwa)
  /packages/ui            Shared shadcn/ui + design tokens
  /packages/api-client    OpenAPI-generated TS client + Axios interceptors
  /packages/types         Shared DTO types
  /packages/hooks         Shared React hooks (useAuth, useSeatMapHub, etc.)
  /packages/i18n          i18next setup + en/ar translation files
  /packages/utils         Date formatting (Asia/Riyadh), Zod schemas
  pnpm-workspace.yaml
  turbo.json
  tsconfig.base.json
/tests
  /KFS.Api.Tests
  /KFS.Application.Tests
  /e2e                    Playwright (incl. paired-booking concurrency)
docker-compose.yml
/infra                    Bicep templates + parameter files (dev, prod)
/.github/workflows        CI + Azure deploy (OIDC)
README.md
DECISIONS.md
```

---

## 22. Definition of Done

- [ ] `docker-compose up` brings up API, **PostgreSQL 16**, **Azurite** (Azure Blob emulator), all 3 frontends locally.
- [ ] Repository deploys cleanly to **Azure UAE North**: App Service (api), Postgres Flexible Server, Storage Account, Key Vault, three Static Web Apps (portal/admin/scanner). Healthchecks green and `/healthz` returns 200.
- [ ] Seed migration creates the venue, an active event, super-admin, all 304 VIP seats, 5 sample students.
- [ ] Admin uploads Excel → student accounts created with the correct password format.
- [ ] Student logs in, picks group + side + seat → mirror seat auto-paired → checkout → 2 emails arrive with valid QRs matching the screenshot format (incl. Arabic line).
- [ ] Two students cannot book the same paired seat (concurrency test).
- [ ] Cart hold expires after 10 minutes; both seats released atomically.
- [ ] Cancellation gives 10-min re-book window; expiry forfeits allocation.
- [ ] Admin "Generate Batch" produces working PDF and ZIP outputs for VVIP / Guest / Staff / Media. Each generated QR scans successfully.
- [ ] Guest QR shows "Group of 3" on scanner; staff/media show single-seat.
- [ ] Scanner page works on iPad with correct camera orientation.
- [ ] Group A / Group B reports export to PDF, Excel, CSV.
- [ ] "Reminder to unbooked students" sends to the right recipients only.
- [ ] Day-before reminder fires automatically and includes QRs + map link.
- [ ] Unit tests cover paired-seat concurrency, allocation enforcement, QR signing, mirror calculation.
- [ ] README explains setup; DECISIONS.md captures every assumption.
- [ ] GitHub Actions workflow runs `dotnet test` + frontend lint/typecheck on PRs and **deploys to Azure UAE North via OIDC** on push to `main`. Bicep templates are validated (`az deployment group what-if`) before apply.

---

## 23. Build Order

1. **Foundations:** Scaffold backend solution (.NET 8, EF Core, OpenAPI) AND frontend monorepo (pnpm workspaces, Turborepo, three Vite apps, shared packages, design tokens, i18n). Wire up the OpenAPI codegen pipeline so generated types flow into `@packages/api-client`. Set up `docker-compose.yml` with API + Postgres + Azurite.
2. **Domain & seed:** Domain models + EF migrations + venue seed (304 seats). Build Bicep templates in `/infra` for UAE North.
3. **Auth (full-stack):** Backend auth endpoints + student Excel upload + admin login. Frontend: login screens, force-password-change flow, auth Zustand store, Axios refresh interceptor in `@packages/api-client`. RequireAuth route guards.
4. **Seat map (full-stack):** Seat map API + SignalR hub + paired-seat reserve transaction. Frontend: `<SeatGrid />` component with realtime updates via `useSeatMapHub`, group/side chooser, mirror-pair preview.
5. **Booking flow (full-stack):** Cart + checkout + 2-ticket QR generation + email sending. Frontend: cart with countdown, checkout confirmation, "My Bookings" page with re-download/resend.
6. **Cancellation + re-book window** (backend logic + frontend modal flow with countdown).
7. **Admin "Generate Batch"** for VVIP / Guest / Staff / Media (PDF + ZIP outputs). Admin UI for batch list and re-download.
8. **Reports & dashboard:** Group A / Group B exports + admin dashboard with live seat map view.
9. **Reminders** (unbooked + day-before, with admin UI for triggering and templates).
10. **Scanner PWA:** vite-plugin-pwa setup, camera handling with iPad mirror correction, IndexedDB offline queue, big visual feedback states.
11. **Hardening:** Tests (Vitest, Playwright E2E covering paired-seat concurrency), accessibility audit, Lighthouse PWA check, Docker, GitHub Actions OIDC deploy to Azure UAE North.

Start by scaffolding the solution, writing `DECISIONS.md` with your initial architectural choices, then proceed through the build order. Pause and ask only if a requirement is genuinely ambiguous; otherwise make the call and document it.
