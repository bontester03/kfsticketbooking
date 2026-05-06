# KFS Booking — School Auditorium Booking System

A full-stack monorepo for booking auditoriums at KFS School. Production-ready starter built with ASP.NET Core 8, Angular 19, PostgreSQL, JWT auth, Serilog, Swagger, EF Core migrations, and a Dockerized deployment behind Nginx.

## Architecture

```
KFS/
├── api/                              ASP.NET Core 8 Web API
│   ├── KfsBooking.sln
│   ├── src/
│   │   ├── KfsBooking.Domain/        Entities, enums, base types (no dependencies)
│   │   ├── KfsBooking.Application/   DTOs, service interfaces + implementations, validators
│   │   ├── KfsBooking.Infrastructure/ EF Core DbContext, JWT, password hashing, seeding
│   │   └── KfsBooking.Api/           Controllers, middleware, Program.cs, settings
│   └── tests/
│       └── KfsBooking.Tests/         xUnit + EF InMemory tests
│
├── web/                              Angular 19 (standalone components, signals)
│   └── src/app/
│       ├── core/                     models, services, guards, interceptors
│       ├── shared/                   reusable components & pipes
│       ├── features/                 auth, dashboard, auditoriums, bookings
│       └── layout/                   header, main layout
│
├── docker-compose.yml                api + web + postgres
└── .env.example                      copy → .env to override defaults
```

## Stack

| Concern         | Technology                                          |
| --------------- | --------------------------------------------------- |
| API             | ASP.NET Core 8, C# 12                               |
| Persistence     | PostgreSQL 16, EF Core 8 (Npgsql), code-first migrations |
| Auth            | JWT bearer tokens, BCrypt password hashing          |
| Validation      | FluentValidation                                    |
| Logging         | Serilog (console + rolling file)                    |
| API docs        | Swagger / OpenAPI                                   |
| Web             | Angular 19, standalone components, signals, RxJS    |
| Web hosting     | Nginx (production image)                            |
| Testing         | xUnit + FluentAssertions + EF InMemory              |
| Container       | Multi-stage Docker builds, docker-compose           |

## Prerequisites

You only need one of these paths:

- **Docker route** (recommended): Docker Desktop 4.x+
- **Local dev route**: .NET 8 SDK, Node.js 20+, PostgreSQL 14+

## Quick Start (Docker)

```bash
cp .env.example .env
# Edit JWT_SECRET to a long random string before going past local dev.

docker compose up --build
```

The first start runs EF Core migrations against PostgreSQL and seeds default users + auditoriums.

| Service     | URL                                       |
| ----------- | ----------------------------------------- |
| Web (Nginx) | http://localhost:8080                     |
| API         | http://localhost:5080/api                 |
| Swagger     | http://localhost:5080/swagger             |
| Postgres    | localhost:5432 (kfsbooking / kfsbooking)  |

The Nginx config in `web/nginx.conf` proxies `/api/` → `http://api:8080/api/`, so the production Angular bundle calls a same-origin `/api`.

## Default Seeded Users

| Role    | Email                | Password     |
| ------- | -------------------- | ------------ |
| Admin   | admin@kfs.local      | Admin@123    |
| Teacher | teacher@kfs.local    | Teacher@123  |
| Student | student@kfs.local    | Student@123  |

Change these the moment you deploy beyond your laptop.

## Local development (no Docker)

### 1. Start Postgres

```bash
docker run --name kfs-pg -e POSTGRES_PASSWORD=kfsbooking -e POSTGRES_USER=kfsbooking \
  -e POSTGRES_DB=kfsbooking -p 5432:5432 -d postgres:16-alpine
```

### 2. Run the API

```bash
cd api
dotnet restore
dotnet ef migrations add InitialCreate \
    --project src/KfsBooking.Infrastructure \
    --startup-project src/KfsBooking.Api
dotnet run --project src/KfsBooking.Api
```

The API listens on http://localhost:5080. Migrations + seed data run on startup.

### 3. Run the Angular app

```bash
cd web
npm install
npm start
```

Open http://localhost:4200. The dev environment points at `http://localhost:5080/api`.

## Running tests

```bash
cd api
dotnet test
```

Tests use EF Core InMemory; no Postgres required.

## Environment variables

Defined in `.env` (consumed by `docker-compose.yml`) or via `appsettings.json` overrides.

| Variable                     | Default                  | Notes                                                  |
| ---------------------------- | ------------------------ | ------------------------------------------------------ |
| `POSTGRES_DB`                | `kfsbooking`             | Database name                                          |
| `POSTGRES_USER`              | `kfsbooking`             | Database user                                          |
| `POSTGRES_PASSWORD`          | `kfsbooking`             | Database password                                      |
| `POSTGRES_PORT`              | `5432`                   | Host port mapped to Postgres                           |
| `ASPNETCORE_ENVIRONMENT`     | `Production`             | `Development` enables Swagger + verbose logs           |
| `API_PORT`                   | `5080`                   | Host port mapped to the API                            |
| `JWT_ISSUER`                 | `kfsbooking`             | JWT `iss` claim                                        |
| `JWT_AUDIENCE`               | `kfsbooking-clients`     | JWT `aud` claim                                        |
| `JWT_SECRET`                 | *(must override)*        | HMAC-SHA256 signing key — at least 32 chars in prod    |
| `JWT_EXPIRY_MINUTES`         | `480`                    | Token lifetime                                         |
| `SWAGGER_ENABLED`            | `true`                   | Set `false` in production                              |
| `WEB_PORT`                   | `8080`                   | Host port mapped to the Nginx web container            |
| `WEB_ORIGIN`                 | `http://localhost:8080`  | Added to API CORS allowed origins                      |

## REST endpoints

All `/api/*` endpoints (except `/api/auth/*` and `/api/health`) require `Authorization: Bearer <token>`.

### Auth

| Method | Path                | Body                           | Notes                  |
| ------ | ------------------- | ------------------------------ | ---------------------- |
| POST   | `/api/auth/register`| `{fullName, email, password}`  | Creates a Student user |
| POST   | `/api/auth/login`   | `{email, password}`            | Returns JWT            |

### Auditoriums

| Method | Path                          | Roles  |
| ------ | ----------------------------- | ------ |
| GET    | `/api/auditoriums`            | any    |
| GET    | `/api/auditoriums/{id}`       | any    |
| POST   | `/api/auditoriums`            | Admin  |
| PUT    | `/api/auditoriums/{id}`       | Admin  |
| DELETE | `/api/auditoriums/{id}`       | Admin  |

### Bookings

| Method | Path                                | Roles           |
| ------ | ----------------------------------- | --------------- |
| GET    | `/api/bookings`                     | Admin, Teacher  |
| GET    | `/api/bookings/mine`                | any             |
| GET    | `/api/bookings/{id}`                | any             |
| POST   | `/api/bookings`                     | any             |
| PATCH  | `/api/bookings/{id}/status`         | Admin           |
| POST   | `/api/bookings/{id}/cancel`         | owner or Admin  |

### Misc

| Method | Path           | Notes                |
| ------ | -------------- | -------------------- |
| GET    | `/api/health`  | liveness check       |
| GET    | `/swagger`     | Swagger UI (when enabled) |

## EF Core migrations

```bash
cd api
# Add a new migration
dotnet ef migrations add <Name> \
    --project src/KfsBooking.Infrastructure \
    --startup-project src/KfsBooking.Api

# Apply pending migrations explicitly
dotnet ef database update \
    --project src/KfsBooking.Infrastructure \
    --startup-project src/KfsBooking.Api
```

`Database__RunMigrationsOnStartup=true` (default) makes the API apply pending migrations + seed data at boot, which is convenient for development and small deployments. Switch it off in production if you prefer to run migrations as a separate step.

## Production notes

- Set a strong `JWT_SECRET` and disable Swagger (`SWAGGER_ENABLED=false`).
- Terminate TLS in front of the web container (e.g. behind a reverse proxy / load balancer) and tighten CORS via `Cors__AllowedOrigins`.
- The API logs to `./logs/kfsbooking-*.log` inside the container — mount a volume if you need persistent logs.
- Tune Postgres backups and resource limits in `docker-compose.yml` for your environment.

## Deploying to Railway

The repo is hosting-platform compatible. Each piece becomes its own Railway service inside one project:

| Railway service | Source | Notes |
| --------------- | ------ | ----- |
| `postgres`      | Plugin | "Add Database → PostgreSQL" — Railway provides `DATABASE_URL` automatically |
| `api`           | this repo, **root directory `api`** | Uses [api/Dockerfile](api/Dockerfile) + [api/railway.json](api/railway.json) |
| `web`           | this repo, **root directory `web`** | Uses [web/Dockerfile](web/Dockerfile) + [web/railway.json](web/railway.json) |

### Why no docker-compose on Railway?

Railway services don't share a docker network the way `docker-compose` does. The code accommodates this:

- **API** reads `DATABASE_URL` (Railway's standard postgres env var) and binds to `$PORT` automatically — see [Program.cs](api/src/KfsBooking.Api/Program.cs).
- **Web** reads its API endpoint from `assets/config.json`, which is generated at container start from the `API_URL` env var — see [docker-entrypoint.sh](web/docker-entrypoint.sh) and [main.ts](web/src/main.ts). No rebuild required to change the API URL.

### One-time setup

1. Push the repo to GitHub (the Railway dashboard connects to a git provider).
2. In Railway: **New Project → Deploy from GitHub repo** → pick this repo.
3. Create **two services** from the same repo:
   - First service: name it `api`, set **Root Directory = `api`**.
   - Add another service from the same repo: name it `web`, set **Root Directory = `web`**.
4. Add a **PostgreSQL** plugin to the project (provides `DATABASE_URL`).

### Required env vars on Railway

**`api` service:**

| Variable                  | Value                                                                       |
| ------------------------- | --------------------------------------------------------------------------- |
| `DATABASE_URL`            | `${{ Postgres.DATABASE_URL }}` (Railway reference)                          |
| `Jwt__Secret`             | a long random string (32+ chars)                                            |
| `Jwt__Issuer`             | `kfsbooking`                                                                |
| `Jwt__Audience`           | `kfsbooking-clients`                                                        |
| `Jwt__ExpiryMinutes`      | `480`                                                                       |
| `ASPNETCORE_ENVIRONMENT`  | `Production`                                                                |
| `Swagger__Enabled`        | `false` (set `true` while shaking things out)                               |
| `Cors__AllowedOrigins__0` | the web service's public URL (e.g. `https://kfsbooking-web.up.railway.app`) |

After the API service has a public URL (**Settings → Networking → Generate Domain**), copy it for the next step.

**`web` service:**

| Variable     | Value                                                                                      |
| ------------ | ------------------------------------------------------------------------------------------ |
| `API_URL`    | API service's public URL + `/api` (e.g. `https://kfsbooking-api.up.railway.app/api`)       |

### Deploy

Push to the branch Railway is watching. Each service rebuilds and redeploys automatically. The API runs migrations + seeds default users on first boot.

### Verifying

- Visit the web service URL → land on the login page. Sign in with the seeded admin (`admin@kfs.local` / `Admin@123`) and **change the password immediately**.
- Hit `https://<api-url>/api/health` → expect `{ "status": "ok" }`.
- If the web app can't reach the API, check the browser console — the URL it's calling comes from `/assets/config.json`, which mirrors the `API_URL` env var. CORS errors mean `Cors__AllowedOrigins__0` doesn't match the web's public origin.
