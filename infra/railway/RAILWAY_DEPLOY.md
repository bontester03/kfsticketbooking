# Deploy KFS Booking to Railway — auto-deploy from GitHub on push

Railway is a PaaS that takes your `Dockerfile`s, builds them in the cloud, runs them, and re-deploys on every `git push origin main`. No SSH, no servers, no CI/CD glue.

The whole stack runs as **5 services in one Railway project**:

| Service | Source | Notes |
|---|---|---|
| `postgres` | Managed plugin | One click; provides `DATABASE_URL` |
| `api` | Dockerfile at `/api/Dockerfile` | The .NET backend |
| `portal` | Dockerfile at `/web/Dockerfile`, build arg `APP=portal` | Parent-facing SPA |
| `admin` | Same `/web/Dockerfile`, build arg `APP=admin` | Admin console SPA |
| `scanner` | Same `/web/Dockerfile`, build arg `APP=scanner` | iPad gate-scanner SPA |

Cost estimate: **~$25–40/mo** total (Trial plan + ~$20 usage). Pro plan adds SLA + priority support for $20/mo more.

---

## 0. Prerequisites

- A Railway account (free): <https://railway.app> → **Login with GitHub** → authorize `bontester03`.
- This repo pushed to GitHub: `bontester03/kfsticketbooking` (already done).
- A Gmail App Password for `graduation@kfs.sch.sa` (you already have one).

---

## 1. Create the project

1. Railway dashboard → **New Project** → **Empty Project** (we'll add services manually so we have full control).
2. Top of the project page → click the project name → rename to **`kfs-booking`**.
3. **Settings → Environment** → confirm region is **`eu-west` (Amsterdam)**. Closest to KSA without me-central/me-south.

---

## 2. Add Postgres

1. **+ New** (top right) → **Database** → **Add PostgreSQL**.
2. Wait ~30 seconds for it to provision. You'll see a `Postgres` service appear.
3. Click it → **Variables** tab → confirm `DATABASE_URL` is auto-populated (you don't paste it; it's a managed value).

---

## 3. Add the API service

1. **+ New** → **GitHub Repo** → select **`kfsticketbooking`** → confirm.
2. The service is created with auto-detected settings. We need to fix the **root directory** so it builds only the API:
   - Click the new service → **Settings** → **Source** → **Root Directory** = `api`
   - **Settings** → **Build** → **Dockerfile Path** = `Dockerfile` (relative to the root dir above, so it resolves to `api/Dockerfile`)
3. **Settings** → **Networking** → **Generate Domain** (click the button). Railway gives you something like `kfs-api-production.up.railway.app`. Note it down.
4. **Settings** → general → **Service Name** = `api` (the lowercase, no-spaces version — this is what `api.railway.internal` resolves to).
5. **Variables** tab → paste in everything from [`infra/railway/.env.example`](.env.example) under the `# Service: api` section. Use the **Raw Editor** so you can paste many at once.
   - For `DATABASE_URL`, click **+ New Variable** → **Add Reference** → pick `Postgres.DATABASE_URL`. That wires it to the managed plugin.
   - Generate fresh values for `Jwt__Secret`, `Qr__SigningKey`, `Auth__SuperAdminPassword` (Railway has a "Generate Secret" button; or run `openssl rand -hex 32` locally).
   - Set `Email__Password` to your Gmail App Password.
6. **Settings** → **Volumes** → **+ New Volume** → mount path `/app/wwwroot`, size 1 GB. This persists the QR/PDF files across deploys.
7. Click **Deploy** (top right). First build takes ~5–8 minutes.

---

## 4. Add the three web services

Repeat this **3 times** — once per app — changing only the `APP` build arg and service name.

1. **+ New** → **GitHub Repo** → `kfsticketbooking` (same repo).
2. **Settings → Source → Root Directory** = `web`.
3. **Settings → Build → Dockerfile Path** = `Dockerfile`.
4. **Settings → Build → Build Args** → click **+ Add** → key `APP`, value one of:
   - For the **portal** service: `portal`
   - For the **admin** service: `admin`
   - For the **scanner** service: `scanner`
5. **Settings → general → Service Name** = `portal` / `admin` / `scanner`.
6. **Settings → Networking → Generate Domain** — note the URL. Update the API service's `Cors__AllowedOrigins__*` variables to include these.
7. **Variables tab** → add the two from [`.env.example`](.env.example):
   - `API_HOST=api.railway.internal`
   - `API_PORT=8080`
8. **Deploy**.

After all three are built, the API's `Cors__AllowedOrigins__*` should list all three URLs. **Edit and re-deploy the API** if you set CORS placeholders earlier.

---

## 5. First smoke test

Open in a browser:

| URL | What you should see |
|---|---|
| `https://kfs-portal-production.up.railway.app` | Sign-in landing page |
| `https://kfs-portal-production.up.railway.app/api/v1/public/event` | JSON with event name + seats remaining |
| `https://kfs-admin-production.up.railway.app` | Admin console login (`admin@kfs.sch.sa` / `Auth__SuperAdminPassword`) |
| `https://kfs-scanner-production.up.railway.app` | Scanner token entry page |

The lock icon should appear in all three — Railway provides TLS automatically.

Sign in to admin → **Guest** tab → copy the gate-scanner link (it embeds the event scanner token). That's the URL you give the iPad gate staff.

---

## 6. Auto-deploy on git push

**Already on.** Every push to `main` triggers Railway to:
1. Re-pull the repo
2. Rebuild the affected services' Docker images (Railway tracks which paths each service watches)
3. Roll the new image in with zero-downtime

You can watch a deploy in real time: **Deployments** tab → click the most recent → live logs.

To **roll back** a bad deploy: Deployments tab → find a previous green build → **⋯** → **Redeploy**.

---

## 7. Custom domain (optional)

Each service supports custom domains. Example for the portal:

1. Portal service → **Settings → Networking → Custom Domain → + Custom Domain**.
2. Enter `kfs.your-domain.com`.
3. Railway shows you a CNAME target. Add it to your DNS provider.
4. Railway provisions a TLS cert (~1 min).

Update the API's `Cors__AllowedOrigins__*` and `Email__PortalUrl` to the new custom URL when done.

---

## 8. Day-to-day commands

```bash
# Tail logs (from your laptop, using the Railway CLI)
npm i -g @railway/cli      # one-time install
railway login              # one-time browser auth
railway link               # pick the kfs-booking project
railway logs --service api

# Open a shell on a running container
railway shell --service api

# Take a Postgres backup
railway run --service postgres "pg_dump -U postgres railway > backup.sql"
```

---

## 9. Pausing for cost between events

Railway charges for compute-hours. After the event:

- **Pause the project**: project page → **⋮** → **Pause** (kills compute, keeps data).
- When you need it again: **Resume**. Boots in ~30 seconds.

Postgres data and the API's volume both persist across pauses.

---

## 10. Load test against the live URL

After deploy, validate the 300-parent burst handles:

```bash
# From your laptop or any Unix shell
./infra/aws/load-test/login-burst.sh https://kfs-api-production.up.railway.app
```

Healthy on 4 vCPU API service: **p95 < 1.5s, max < 4s, all 200**. If p95 climbs above 3s, bump the API service in **Settings → Resources** from 4 vCPU → 8 vCPU (slider, no downtime).

---

## Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| API deploy fails: `unable to connect to Postgres` | The `DATABASE_URL` variable reference is missing — re-add it under API's Variables → Add Reference. |
| Web SPA loads but `/api/v1/...` returns 502 | The web service's `API_HOST` is wrong. Must be exactly `api.railway.internal` (the API service must also be named `api`). |
| Login returns 401 but creds are right | API hasn't completed migrations yet. Check API Deployments tab — wait for "Healthy". |
| Email not sending | `Email__Password` is wrong, OR Gmail Workspace blocks the IP. Check API logs for `SMTP` lines. |
| Healthcheck failures during deploy | API's startup migration is slow on first deploy (~30s). Increase **Settings → Health Check → Timeout** to 100s. |
| QR images broken (red cross) | `Storage__PublicBaseUrl` must match the API's public Railway URL. Update and redeploy. |

---

## What this differs from the AWS setup

| Concern | AWS EC2 + Docker Compose | Railway |
|---|---|---|
| GitHub auto-deploy | GitHub Actions workflow (would need writing) | **Built-in** — push to main, it deploys |
| Postgres | Container on the EC2 | **Managed** (automatic backups, point-in-time restore) |
| HTTPS | Caddy + Let's Encrypt | **Automatic** on `*.up.railway.app` + custom domains |
| Reverse proxy | Caddy with rate-limit module | Railway ingress (no rate-limit; add via .NET middleware later) |
| Region for KSA | Bahrain (not available right now) | `eu-west` Amsterdam (similar PDPL trade-off to AWS Ireland) |
| Cost (monthly) | ~$60 (t3.large) | ~$25–40 |
| Tear-down | `aws ec2 terminate-instances` | **Pause** the project (data preserved) |

Pragmatic for a school event with auto-deploy as the priority.
