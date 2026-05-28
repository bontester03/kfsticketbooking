# KFS School Event — Ticket Booking & QR Verification Platform

A full-stack platform for King Faisal School's Annual Function: parents reserve VIP seats and Guest tickets through a portal, admins manage the roster / generate VVIP-Guest-Staff-Media QR passes / monitor scans, and gate staff verify tickets through an iPad camera scanner — all backed by a single .NET 8 API and PostgreSQL.

Originally built from [`kfs_ticket_booking_prompt_v2 (2).md`](kfs_ticket_booking_prompt_v2%20%282%29.md); every architectural call is captured in [DECISIONS.md](DECISIONS.md).

---

## What's in the box

```
KFS/
├── api/                              ASP.NET Core 8 + EF Core + PostgreSQL
│   ├── KFS.sln
│   └── src/
│       ├── KFS.Domain/               Entities, enums (no dependencies)
│       ├── KFS.Application/          DTOs, service interfaces + implementations, validators
│       ├── KFS.Infrastructure/       EF Npgsql, JWT, QR (QRCoder), PDF (QuestPDF),
│       │                             Excel (ClosedXML), SMTP email, blob storage
│       └── KFS.Api/                  Controllers, SignalR hubs, hosted-services, middleware
│
├── web/                              pnpm workspace (Vite + React 18 + TypeScript)
│   ├── apps/portal/                  Parent/student booking portal
│   ├── apps/admin/                   Admin console (full)
│   ├── apps/scanner/                 Gate scanner (iPad camera, tokened link)
│   └── packages/{ui,api-client,types,utils,i18n}
│
├── infra/                            Bicep templates for Azure UAE North
├── docker-compose.yml                api + postgres + azurite + 3 web apps
├── DECISIONS.md                      Every non-obvious architectural choice
└── .env.example                      Copy → .env (gitignored)
```

### Apps and what they do

| App | Port | Who uses it | What it does |
|---|---|---|---|
| **Portal** | 5173 | Parents / students | Sign in, see assigned VIP group, pick a seat (system auto-pairs Mother/Father across Female/Male sides), book a Guest ticket, download tickets PDF, see scan status. |
| **Admin** | 5174 | School office staff | Roster upload, "Send welcome emails", per-type pass limits, Generate/Preview/Delete passes (PDF or ZIP), Guest analytics + issue-to-child, **Scan audit** with type/status filters, Live seat map, Reports, Reminders, Event settings. |
| **Scanner** | 5175 | Gate staff (iPad) | Tokened deep-link opens the camera; jsQR decodes; one POST to `/scan/verify`. Single-use tickets reject the 2nd scan; Guest tickets allow 3 admissions then reject. |

---

## Quick start

```bash
cp .env.example .env
# Set strong randoms for JWT_SECRET, QR_SIGNING_KEY, SUPER_ADMIN_PASSWORD.
# (See "Email" below for optional SMTP settings.)
docker compose up --build
```

| Surface | URL |
|---|---|
| Portal (parents) | <http://localhost:5173> |
| Admin console | <http://localhost:5174> |
| Scanner (gate) | <http://localhost:5175> |
| API + Swagger | <http://localhost:5080/swagger> |
| Public event endpoint (sign-in banner) | <http://localhost:5080/api/v1/public/event> |
| PostgreSQL | `localhost:5432` (kfs / `POSTGRES_PASSWORD`) |
| Azurite (blob) | <http://localhost:10000/devstoreaccount1> |

On first boot the API applies EF migrations and seeds: 1 active event, 8 zones, 304 VIP seats, 1 super-admin, 5 sample students.

### Default credentials

| Role | Email | Password |
|---|---|---|
| Admin | `admin@kfs.sch.sa` | `${SUPER_ADMIN_PASSWORD}` (`Admin@123` default) |
| Sample student | `safa.albuhairan@stu.kfs.sch.sa` | `Saf15032010` *(legacy seed: First3 + DDMMYYYY)* |

After you upload a real roster from the admin app, each student's initial password becomes **`First3Cap + StudentID`** (e.g. `Ahm437079`). All accounts start with `MustChangePassword = true`.

---

## Roster upload — column layout

Admin → **Students** → **Download sample (.xlsx)** for the exact template. Columns, in order:

| # | Column | Required | Notes |
|---|---|---|---|
| 1 | Student ID | optional | School roster number (used for the initial password). |
| 2 | First Name | **yes** | Drives the password prefix (first 3 letters, capitalised). |
| 3 | Last Name | **yes** | |
| 4 | Preferred Name | optional | Arabic / display name. |
| 5 | Email | **yes** | Login identifier. |
| 6 | Gender | optional | |
| 7 | Grade | optional | |
| 8 | Group | optional | `VIP A` / `VIP B` (also accepts `A` / `B`). Pre-assigns the child's section. |

After uploading, click **Send welcome emails** — every active student is reset to their initial password and emailed sign-in instructions with the KFS logo footer.

---

## VIP group assignment — booking is enforced

When the roster has a Group column filled in:
- The student's `AssignedGroup` (A or B) ships back in `AuthResponse`.
- The portal auto-routes past the A/B picker straight to that section's seat map.
- The API rejects `POST /cart/select` for any other group with `400 wrong_group`.

If the column is left blank for some students, those keep the old "pick A or B" behaviour.

---

## Scan engine

`POST /api/v1/scan/verify` takes a QR payload (signed-JWT, distinct signing key from auth) and the event scanner token. The same code path handles:

- **Booking items** (parent seats) — single-use. 2nd scan returns `AlreadyUsed` with the time of the first valid scan.
- **Admin passes** — admit up to `SeatsCount` people, one per scan.
  - VVIP / Staff / Media: 1 scan.
  - Guest: **3 admissions** then `AlreadyUsed`. Response includes `admittedCount` so the gate display can show "Person 2 of 3, 1 entry left".

Every valid and rejected scan is logged in `scan_logs`. The admin **Scans** tab exposes this audit:
- Filters: **Ticket type** (VVIP / Guest / Staff / Media / Student seat) · **Status** (All / Scanned / Not scanned) · free-text search by ticket # or holder.
- Per-row: ticket #, holder, detail (zone/seat), Scanned Yes/No (`N/M` for guest), first → last scan time.

### Scanner deep link

```
http://<scanner-host>/?token=<event.scanner_token>
```

- Admin can copy this from **Guest → Gate scanner link**.
- The scanner caches the token in `localStorage`; if the server ever returns "Scanner token invalid", the app self-heals back to the token entry screen.
- **iPad note:** the camera API requires HTTPS or `localhost`. On the deployed (Azure) URL it works; on a plain `http://<pc-ip>` LAN URL it won't — the Manual entry box is the fallback.
- jsQR is loaded from a CDN (`cdn.jsdelivr.net`) because the corporate proxy blocks bundling it.

---

## Tickets a parent sees

Two surfaces, both visible from the portal **after a confirmed booking**:

1. **The on-screen ticket card** — same design as the printed reference: violet category badge, GATE / BLOCK / SEAT / ROW grid, Arabic seat-pair line, *"Ticket is sent to ... "* receipt + QR.
2. **Download tickets PDF** button — produces `{name}-tickets.pdf` with one card per page: each parent pass (Mother / Father) plus the Guest ticket (if booked). Same visual language, embedded Dubai font for the Arabic.

A child can also book **one Guest ticket** (`POST /guest`) — one QR that admits 3, shown on the portal **Guest ticket** page, with live scan status (`2 of 3 admitted`).

---

## Email

The API ships with two `IEmailService` implementations:

- **ConsoleEmailService** (default) — writes the rendered HTML + attachments to `/app/logs/email/...`. No external dependency; great for dev.
- **SmtpEmailService** — real SMTP. Tested with **Gmail / Google Workspace** (`smtp.gmail.com:587`, STARTTLS, app-password auth). Microsoft 365 (`smtp.office365.com:587`) also wired but requires tenant-level `SmtpClientAuthentication` to be allowed.

Set in `.env` (gitignored):

```bash
EMAIL_PROVIDER=Smtp           # or Console
EMAIL_HOST=smtp.gmail.com
EMAIL_PORT=587
EMAIL_USERNAME=<the-mailbox>
EMAIL_PASSWORD=<16-char Google App Password (no spaces)>
EMAIL_FROM=<same as username>
EMAIL_FROM_NAME=King Faisal School
EMAIL_PORTAL_URL=http://localhost:5173

# Local dev only — accept a corporate proxy's intercepting cert on the SMTP TLS handshake.
# DO NOT set this on the Azure deploy.
EMAIL_ACCEPT_INVALID_CERT=true
```

Then `docker compose up -d --force-recreate api` to pick up the new env.

Emails sent by the API:
- **Booking confirmation** (one per ticket, with QR PNG attachment)
- **Welcome email** (bulk, after roster upload) — credentials + booking steps + inline KFS logo footer
- **Password reset** (admin clicks Reset password on a student) — fire-and-forget so the admin sees the new password instantly even if SMTP is slow

---

## REST endpoints (under `/api/v1`)

### Auth
- `POST /auth/login` (student) · `POST /auth/admin/login`
- `POST /auth/refresh`
- `POST /auth/forgot-password` / `POST /auth/reset-password`
- `POST /auth/change-password`

### Public (no auth)
- `GET /public/event` — safe aggregates for the sign-in banner (name, date, venue, seats remaining)
- `POST /scan/verify` — gated by the event scanner token, not by login

### Student (role `Student`)
- `GET /me` · `GET /me/tickets.pdf`
- `GET /events/active` · `GET /events/{id}/seatmap?group=A|B`
- `GET /cart` · `POST /cart/select` · `DELETE /cart` · `POST /cart/checkout`
- `GET /bookings` · `POST /bookings/{id}/cancel` · `POST /bookings/{id}/resend-emails`
- `GET /guest` · `POST /guest`

### Admin (role `Admin`)
- Students: `POST/GET/PATCH/DELETE /admin/students/...`, `POST upload`, `GET sample`, `POST {id}/reset-password`, `POST send-welcome-emails`
- Passes: `GET/POST/DELETE /admin/passes/...`, `GET batches`, `DELETE batches/{id}` and `DELETE batches?type=`, `GET batches/{id}/download?format=pdf|zip`, `GET/PUT quota`
- Guest: `GET analytics`, `GET students`, `POST issue`
- Bookings: `GET /admin/bookings`, `GET /admin/seatmap?group=`, `POST {id}/force-cancel`
- Scans: `GET /admin/scans?search=&status=&kind=`
- Reports: `GET /admin/reports/dashboard`, `GET /admin/reports/group/{A|B}?format=csv|xlsx|pdf`
- Reminders: `POST /admin/reminders/unbooked`, `GET /admin/reminders/logs`
- Event: `GET /admin/event`, `PUT /admin/event/{id}`

### SignalR
- `/hubs/seatmap` — `JoinGroup(eventId, group)` / `LeaveGroup(eventId, group)`; emits `seat-changed`.

---

## Stack

| Concern | Choice |
|---|---|
| API | ASP.NET Core 8, C# 12 |
| Persistence | PostgreSQL 16, EF Core 8 (Npgsql), code-first migrations |
| Real-time | SignalR (`/hubs/seatmap`) |
| Auth | JWT (15-min access + 7-day refresh, hashed in DB), BCrypt |
| QR | QRCoder; payload = signed JWT (separate signing key from auth) |
| PDF | QuestPDF + bundled **Dubai** font (Arabic + Latin) |
| Excel | ClosedXML |
| Emails | `IEmailService` + `SmtpEmailService` (System.Net.Mail) |
| Blob storage | `IBlobStorage` (`AzureBlobStorage` via Azurite locally; SAS re-signed on every read) |
| Background jobs | `IHostedService` + `PeriodicTimer` |
| Logging | Serilog (console + rolling file) |
| Web monorepo | pnpm workspaces + Turborepo |
| Web framework | Vite + React 18 + TypeScript |
| Server state | TanStack Query v5 |
| Client state | Zustand (auth) |
| Forms | React Hook Form + Zod |
| i18n | i18next (English + Arabic with RTL) |
| QR decoder (web) | jsQR from CDN |
| Brand tokens | KFS forest `#0d3128` · sage `#548b7d` · gold `#a08b16` · sign-in panel teal `#124D41` |
| Fonts (web) | Source Sans 3 (Latin) + IBM Plex Sans Arabic |
| Fonts (PDF) | Dubai (Latin + Arabic) — bundled in `KFS.Infrastructure/Pdf/Fonts` |
| Time zone | Asia/Riyadh (Windows id `Arab Standard Time`, NOT `Arabian Standard Time`) |

---

## Local dev (no Docker, hot reload)

```bash
# Web — three Vite dev servers
cd web
corepack enable && corepack prepare pnpm@9.7.0 --activate   # one-time
pnpm install
pnpm --filter portal dev    # http://localhost:5173
pnpm --filter admin dev     # http://localhost:5174
pnpm --filter scanner dev   # http://localhost:5175
```

Each Vite dev server proxies `/api` and `/hubs` to `http://localhost:5080`.

```bash
# API — needs Postgres. Easiest:
docker run --name kfs-pg -e POSTGRES_PASSWORD=kfs -e POSTGRES_USER=kfs \
  -e POSTGRES_DB=kfs -p 5432:5432 -d postgres:16-alpine

cd api && dotnet run --project src/KFS.Api
```

Open <http://localhost:5080/swagger>.

### EF migrations

```bash
dotnet ef migrations add <Name> \
  --project src/KFS.Infrastructure --startup-project src/KFS.Api

dotnet ef database update \
  --project src/KFS.Infrastructure --startup-project src/KFS.Api
```

Migrations apply automatically on container start (`Database__RunMigrationsOnStartup=true`).

---

## Behind a corporate TLS proxy

If your network intercepts HTTPS (causing `UNABLE_TO_VERIFY_LEAF_SIGNATURE` on npm or `UntrustedRoot` on .NET):

- **Git** — `git config --global http.sslBackend schannel` (uses the Windows cert store).
- **Docker frontend builds (npm)** — they fail through the proxy. Workaround: build dist locally (`pnpm --filter <app> build`) and `docker cp` it into the running container (`docker cp web/apps/portal/dist/. kfs-portal:/usr/share/nginx/html/`).
- **Docker API builds (NuGet)** — same. Publish locally (`dotnet publish ... -o /tmp/api_publish`) and `docker cp` into `kfs-api:/app/` then `docker restart kfs-api`.
- **SMTP** — set `EMAIL_ACCEPT_INVALID_CERT=true` in `.env` (dev only; do not enable in production).

These are documented in DECISIONS.md alongside the Azure deploy notes.

---

## Azure deploy

Templates in `/infra/`. The deploy region split (added during the build):

- **Data** (Postgres, Storage, Key Vault, App Insights) stays in **UAE North** for Saudi PDPL data residency.
- **Compute** (App Service / API) runs in **UK South** because UAE North ships 0 App Service VM quota.

GitHub Actions workflow uses OIDC federation; secrets are managed in the `dev` environment in GitHub. See `infra/AZURE_SETUP.md` for the Cloud Shell runbook.

Deploy to Azure once the App Service VM quota is granted (see [DECISIONS.md](DECISIONS.md) for the quota request flow).

---

## License & credits

Internal project for King Faisal School. Dubai font (Arabic + Latin) bundled under its free-use licence. jsQR licensed MIT and loaded from CDN at runtime.
