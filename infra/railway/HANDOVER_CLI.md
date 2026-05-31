# Railway deploy via CLI (no Git, repo stays private)

Setup the client does once, you do all subsequent deploys from your laptop with `railway up`.
Source code stays on your private GitHub; the client's Railway account never sees git directly.

---

## Part A — Client (one-time, ~5 min)

1. Sign up at <https://railway.app> with **email + Google / Microsoft** (any provider, no GitHub needed).
2. Top right → **+ New Project → Empty Project**. Rename it `kfs-booking` (Project Settings → Name).
3. Project **Settings → Environment** → set region: **`eu-west` (Amsterdam)** — closest PDPL-friendly option without a true MENA region.
4. Project **Settings → Members → + Invite** → enter **siddharthasharma.uk@gmail.com** with the **Admin** role. (Send.)
5. Project **+ New → Database → Add PostgreSQL**. Wait ~30s for it to provision.
6. Tell you the project is ready.

That's it — they never see code or git.

---

## Part B — You, one-time (~10 min)

1. Install the Railway CLI (Windows PowerShell):

   ```powershell
   npm install -g @railway/cli
   # or, if you prefer no npm:
   # iwr -useb https://railway.app/install.ps1 | iex
   railway --version
   ```

2. Accept the invite in your email, then authenticate the CLI:

   ```powershell
   railway login          # opens browser, click "Login"
   railway whoami         # confirms your account
   ```

3. From the repo root, link the client's project:

   ```powershell
   cd c:\Users\SIDDHARTHA\KFS
   railway link           # pick the kfs-booking project from the list
   ```

4. **Create the 4 application services** in the Railway dashboard. Each one needs the same kind of config — different Root Directory / build arg.

   For **each** service (api / portal / admin / scanner):
   - Project page → **+ New → Empty Service** → name it exactly `api` (then `portal`, `admin`, `scanner`).
   - Click the service → **Settings → Source → Connect Source → none / upload via CLI** (don't connect a repo).
   - **Settings → Build** → set:
     - **Root Directory**
     - **Dockerfile Path** (relative to root directory)
     - **Build Arguments** (web services only)
   - **Settings → Networking → Generate Domain** (only for portal / admin / scanner; the API is reached via internal DNS).

   | Service | Root Directory | Dockerfile | Build Args | Generate public domain? |
   |---|---|---|---|---|
   | `api`     | `api` | `Dockerfile` | (none) | No (internal only) |
   | `portal`  | `web` | `Dockerfile` | `APP=portal`  | Yes |
   | `admin`   | `web` | `Dockerfile` | `APP=admin`   | Yes |
   | `scanner` | `web` | `Dockerfile` | `APP=scanner` | Yes |

5. **Paste env vars** into each service's **Variables** tab. The full list is in [`infra/railway/.env.example`](.env.example) — Variables → Raw Editor → paste the matching section. Notes:

   - **`api` service**: under **Add Variable → Add Reference**, pick `Postgres.DATABASE_URL` so the API service inherits the managed Postgres connection string automatically.
   - **Generate secrets locally** so they never go through chat:
     ```powershell
     # in PowerShell
     [Convert]::ToHexString((1..32 | ForEach-Object { Get-Random -Maximum 256 })) -replace '-',''
     ```
     Run twice → use for `Jwt__Secret` and `Qr__SigningKey`.
   - `Email__Password` = your Gmail App Password (`graduation@kfs.sch.sa`).
   - **Web services** (`portal` / `admin` / `scanner`): set `API_HOST=api.railway.internal` and `API_PORT=8080`.
   - **`Cors__AllowedOrigins__0/1/2`** (on api): set after generating the public domains in step 4 — paste the three `https://...up.railway.app` URLs.

6. Add a **Volume** to the API service so QR PNGs / PDFs survive redeploys:
   - api service → **Settings → Volumes → + New Volume** → mount path `/app/wwwroot`, size 1 GB.

---

## Part C — Deploy (you, every code change)

From the repo root, one PowerShell call:

```powershell
cd c:\Users\SIDDHARTHA\KFS
.\infra\railway\deploy.ps1
```

This runs `railway up --service <name>` for **api → portal → admin → scanner** in sequence, streaming build logs from Railway.

Deploy just one service (faster while iterating on, say, the admin app):

```powershell
.\infra\railway\deploy.ps1 -Only admin
```

Detach so each upload returns immediately (Railway keeps building in the background):

```powershell
.\infra\railway\deploy.ps1 -Detach
```

Watch logs after a detached deploy:

```powershell
railway logs --service api
```

---

## What the client sees

- They open `https://kfs-portal-production.up.railway.app` (or your custom domain) and the app works.
- They see build status / logs in the Railway dashboard but never touch git, npm, the CLI, or your repo.
- When you push an update from your laptop, Railway rebuilds → their URL serves the new version after ~3 min.

---

## If you ever want to switch to auto-deploy later

You can flip back to Option 1 (public repo + GitHub-connected deploys) any time — Railway lets you change each service's Source from "Upload" to "GitHub Repo" without rebuilding the project. The volumes, env vars, and DNS stay in place.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `railway up` says "no service linked" | `railway link` again, or pass `--service api` explicitly. |
| Build fails: "Dockerfile not found" | The service's **Root Directory** is wrong. For api set it to `api`; for the 3 web services set it to `web`. |
| Web app loads but `/api/v1/...` returns 502 | The web service's `API_HOST` env var must be `api.railway.internal` (exact string) — and the API service must be named `api` (lowercase). |
| API logs say "no event" on login | The seeder needs a fresh DB to plant both events. Click **Postgres → Data → Reset** in the dashboard, then redeploy the api service so the seeder runs. |
| `railway up` uploads slow or huge | Check the upload context — `.gitignore` should be excluding `node_modules`, `bin`, `obj`. Add anything else big to `.railwayignore`. |
