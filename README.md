# KFS School Event — Ticket Booking & QR Verification Platform

Reference implementation of [kfs_ticket_booking_prompt_v2.md](kfs_ticket_booking_prompt_v2.md). For every architectural choice see [DECISIONS.md](DECISIONS.md).

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

## Deploying to Railway

1. Push the repo to GitHub.
2. In Railway → **New Project → Deploy from GitHub repo** → pick `bontester03/kfsticketbooking`.
3. The first service is the API: set **Root Directory = `api`**. Railway picks up [api/Dockerfile](api/Dockerfile) + [api/railway.json](api/railway.json).
4. **+ Add Database → PostgreSQL** — Railway exposes `DATABASE_URL` automatically.
5. On the api service → **Variables**, set `DATABASE_URL=${{ Postgres.DATABASE_URL }}`. The API parses the URL into Npgsql key=value form on startup ([Program.cs](api/src/KFS.Api/Program.cs) → `NormalizeConnectionString`). Add the `Jwt__Secret`, `Qr__SigningKey`, etc. from the env list above.
6. **Settings → Networking → Generate Domain** to get the public API URL.
7. Push to `main`. The API auto-deploys, runs migrations, and seeds.

The frontends (`portal`, `admin`, `scanner`) — once shipped — will live under `web/apps/*` and each becomes its own Railway service with `Root Directory` pointing at that subfolder.

## What's deferred (next passes)

See [DECISIONS.md → "What is NOT in this commit"](DECISIONS.md#what-is-not-in-this-commit) for the full list. Headlines: React frontends, Playwright e2e tests, GitHub Actions for Azure deploy, real SendGrid integration, Azure Blob.
