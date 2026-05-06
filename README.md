# KFS School Event — Ticket Booking & QR Verification Platform

Reference implementation of [kfs_ticket_booking_prompt_v2 (1).md](kfs_ticket_booking_prompt_v2%20%281%29.md) (the spec that supersedes the original v2). Every architectural call is captured in [DECISIONS.md](DECISIONS.md).

> **Build status (this commit)**
> - ✅ Backend: complete (.NET 8 + EF Core + PostgreSQL via Npgsql, all v2 endpoints, paired-seat concurrency, QR generation, PDF/ZIP pass batches, scanner verify, SignalR live seat map, background jobs, console-output email, seeded database)
> - 🟡 Frontends (`portal`, `admin`, `scanner`): not in this commit — backend APIs are stable so they can plug in next pass
> - 🟡 Tests: structure scaffolded; concurrency tests next pass
> - 🟡 Azure CI: pending; Railway deploy is the documented path

## Architecture

```
KFS/
├── api/                              ASP.NET Core 8 Web API
│   ├── KFS.sln
│   ├── src/
│   │   ├── KFS.Domain/               Entities, enums (no dependencies)
│   │   ├── KFS.Application/          DTOs, service contracts + implementations, validators
│   │   ├── KFS.Infrastructure/       EF Core Npgsql, JWT, QR (QRCoder), PDF (QuestPDF),
│   │   │                             Excel (ClosedXML), email, blob storage, seeding
│   │   └── KFS.Api/                  Controllers, SignalR hubs, IHostedService jobs,
│   │                                 middleware, Program.cs, settings
│   └── tests/
│       └── KFS.Tests/                xUnit + FluentAssertions
│
├── docker-compose.yml                api + postgres
├── DECISIONS.md                      every non-obvious architectural choice
├── kfs_ticket_booking_prompt_v2.md   the source spec
└── .env.example                      copy → .env
```

## Stack

| Concern              | Choice                                                                                |
| -------------------- | ------------------------------------------------------------------------------------- |
| API                  | ASP.NET Core 8, C# 12                                                                 |
| Persistence          | PostgreSQL 16, EF Core 8 (`Npgsql.EntityFrameworkCore.PostgreSQL`), code-first migrations |
| Real-time            | SignalR (`/hubs/seatmap`)                                                             |
| Auth                 | JWT (15-min access, 7-day refresh, hashed in DB), BCrypt password hashing             |
| Validation           | FluentValidation                                                                      |
| QR                   | QRCoder (signed JWT payload, separate signing key from auth)                          |
| PDF / Excel          | QuestPDF, ClosedXML                                                                   |
| Emails               | `IEmailService` abstraction; default writes HTML + attachments to `logs/email/`       |
| Blob storage         | `IBlobStorage`; default writes to `wwwroot/` served at `/static/`                     |
| Background jobs      | `IHostedService` + `PeriodicTimer` (cart sweeper, rebook expirer, day-before reminder)|
| Logging              | Serilog (console + rolling file)                                                      |
| API docs             | Swashbuckle / OpenAPI                                                                 |

## Quick start (Docker)

```bash
cp .env.example .env
# Edit JWT_SECRET, QR_SIGNING_KEY, and SUPER_ADMIN_PASSWORD to strong randoms.
docker compose up --build
```

| Service       | URL / port                             |
| ------------- | -------------------------------------- |
| API           | http://localhost:5080/api/v1           |
| Swagger       | http://localhost:5080/swagger          |
| SignalR       | ws://localhost:5080/hubs/seatmap       |
| PostgreSQL    | localhost:5432 (kfs / `POSTGRES_PASSWORD`) |

The first start runs EF Core migrations and seeds: 1 active event, 8 zones, 304 VIP seats, 1 super-admin (`admin@kfs.sch.sa`), 5 sample students.

### Default credentials (change immediately)

| Role    | Email / Login           | Password                                 |
| ------- | ----------------------- | ---------------------------------------- |
| Admin   | `admin@kfs.sch.sa`      | `SUPER_ADMIN_PASSWORD` env (`Admin@123`) |
| Student | `safa.albuhairan@stu.kfs.sch.sa` | `Saf15032010` (3-letter capitalised first name + DDMMYYYY) |
| Student | `layan.alqahtani@stu.kfs.sch.sa` | `Lay22062009` |
| Student | `yousef.almutairi@stu.kfs.sch.sa` | `You04112010` |
| Student | `reem.alotaibi@stu.kfs.sch.sa` | `Ree30012009` |
| Student | `khalid.alharbi@stu.kfs.sch.sa` | `Kha12082010` |

All accounts are flagged `MustChangePassword = true`.

## Local development (no Docker)

```bash
# Start a local Postgres (any way you like). Easiest is Docker:
docker run --name kfs-pg -e POSTGRES_PASSWORD=kfs -e POSTGRES_USER=kfs \
  -e POSTGRES_DB=kfs -p 5432:5432 -d postgres:16-alpine

cd api
dotnet restore

# First-time setup: generate the initial migration. The seeder gracefully falls back to
# EnsureCreated if you skip this, but committing migrations is the supported flow.
dotnet ef migrations add InitialCreate \
    --project src/KFS.Infrastructure \
    --startup-project src/KFS.Api

dotnet run --project src/KFS.Api
```

Open http://localhost:5080/swagger. Subsequent migrations:

```bash
dotnet ef migrations add <Name> \
    --project src/KFS.Infrastructure \
    --startup-project src/KFS.Api

dotnet ef database update \
    --project src/KFS.Infrastructure \
    --startup-project src/KFS.Api
```

## REST endpoints (all under `/api/v1`)

### Auth (no role)
- `POST /auth/login` (student)
- `POST /auth/admin/login`
- `POST /auth/refresh`
- `POST /auth/forgot-password` / `POST /auth/reset-password`
- `POST /auth/change-password` (any authenticated user)

### Student (role `Student`)
- `GET  /me`
- `GET  /events/active`
- `GET  /events/{id}/seatmap?group=A|B`
- `GET  /cart` / `POST /cart/select` / `DELETE /cart` / `POST /cart/checkout`
- `GET  /bookings` / `POST /bookings/{id}/cancel` / `POST /bookings/{id}/resend-emails`

### Admin (role `Admin`)
- Students: `POST /admin/students/upload`, `GET/PATCH /admin/students/...`, `POST /admin/students/{id}/reset-password`
- Passes: `POST /admin/passes/generate`, `GET /admin/passes/batches`, `GET /admin/passes`, `PATCH /admin/passes/{id}`, `GET /admin/passes/batches/{id}/download?format=pdf|zip`
- Bookings: `GET /admin/bookings`, `GET /admin/seatmap?group=`, `POST /admin/bookings/{id}/force-cancel`
- Reports: `GET /admin/reports/dashboard`, `GET /admin/reports/group/{A|B}?format=csv|xlsx|pdf`
- Reminders: `POST /admin/reminders/unbooked`, `GET /admin/reminders/logs`
- Event: `GET /admin/event`, `PUT /admin/event/{id}`

### Scanner (no auth, requires `eventToken` in body)
- `POST /scan/verify`

### SignalR
- `/hubs/seatmap` — `JoinGroup(eventId, group)` / `LeaveGroup(eventId, group)`; receives `seat-changed` events.

## Required env vars (production)

| Variable                  | Notes                                                         |
| ------------------------- | ------------------------------------------------------------- |
| `ConnectionStrings__Default` | PostgreSQL key=value string (or set `DATABASE_URL` and the API translates it) |
| `Jwt__Secret`             | ≥32 chars, never commit                                       |
| `Qr__SigningKey`          | ≥32 chars, **separate** from `Jwt__Secret`                    |
| `Auth__SuperAdminPassword`| Initial super-admin password (only used on first boot)        |
| `Cors__AllowedOrigins__N` | Public URL of each frontend (portal, admin, scanner)          |
| `Database__RunMigrationsOnStartup` | `true` for self-managed deploys                      |
| `Swagger__Enabled`        | `false` in production                                         |

## Deploying to Azure UAE North

The full footprint (App Service, Postgres Flex, Storage, Key Vault, App Insights, three Static Web Apps) is described in Bicep at [infra/main.bicep](infra/main.bicep) — see [infra/README.md](infra/README.md) for the manual deploy commands.

### Automated deploy via GitHub Actions (OIDC)

[.github/workflows/deploy.yml](.github/workflows/deploy.yml) runs `what-if`, applies the Bicep, publishes the API, and smoke-tests `/healthz`. To enable:

1. **Create an Azure AD app registration** with federated credentials for this repo. The app needs Contributor on the resource group and User Access Administrator if you want it to grant the App Service Managed Identity its KV/Storage RBAC roles.
2. In GitHub repo settings, add these **environment secrets** (one set per `dev`, `prod`):
   - `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
   - `AZURE_RESOURCE_GROUP` (e.g. `rg-kfs-prod`)
   - `POSTGRES_ADMIN_PASSWORD`, `JWT_SECRET`, `QR_SIGNING_KEY`, `SUPER_ADMIN_PASSWORD`
3. Add a repo **variable** `AZURE_APP_NAME` (e.g. `app-kfs-prod-uaen`).
4. Push to `main` — the workflow runs `az deployment group what-if` first, then applies.

### Manual deploy (one shot)

```bash
az login
az group create --name rg-kfs-prod --location uaenorth

export POSTGRES_ADMIN_PASSWORD='...'
export JWT_SECRET='...'
export QR_SIGNING_KEY='...'
export SUPER_ADMIN_PASSWORD='...'

az deployment group create \
  --resource-group rg-kfs-prod \
  --template-file infra/main.bicep \
  --parameters infra/prod.bicepparam
```

After Bicep applies, publish the API:

```bash
cd api
dotnet publish src/KFS.Api -c Release -o ./publish
zip -r api.zip publish
az webapp deploy --resource-group rg-kfs-prod --name app-kfs-prod-uaen --src-path api.zip --type zip
```

Watch `/healthz` come green:

```bash
curl https://<api-public-host>/healthz
```

### What lands in Azure

| Resource                          | Region        | Purpose                                                  |
| --------------------------------- | ------------- | -------------------------------------------------------- |
| Resource group                    | UAE North     | All PII-bearing resources                                |
| App Service Plan + Web App        | UAE North     | API host, .NET 8 Linux                                   |
| Postgres Flexible Server (v16)    | UAE North     | citext / pgcrypto / pg_trgm enabled                      |
| Storage Account                   | UAE North     | Containers `qr-codes`, `printable-batches`               |
| Key Vault                         | UAE North     | JWT, QR, super-admin, Postgres connection string         |
| Application Insights              | UAE North     | Wired via `APPLICATIONINSIGHTS_CONNECTION_STRING`        |
| Static Web App × 3                | centralus     | `portal`, `admin`, `scanner` (SWA not yet GA in UAE N)   |

The App Service uses **Managed Identity** for KV + Storage access. App Settings reference KV via `@Microsoft.KeyVault(...)` — secrets never appear in the App Settings blade plaintext.

## What's deferred (next passes)

See [DECISIONS.md → "What is NOT in this commit"](DECISIONS.md#what-is-not-in-this-commit) for the full list. Headlines: React frontends, Playwright e2e tests, GitHub Actions for Azure deploy, real SendGrid integration, Azure Blob.
