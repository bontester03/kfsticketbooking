# Deploy the KFS stack to Railway via the CLI (no Git push needed).
#
# Prerequisites (one-time):
#   1. npm install -g @railway/cli   (or `iwr -useb https://railway.app/install.ps1 | iex`)
#   2. railway login                  (browser auth, one-time)
#   3. railway link                   (pick the kfs-booking project)
#   4. Each of api / portal / admin / scanner exists as a Railway service,
#      configured with Root Directory + Dockerfile + build args per
#      infra/railway/RAILWAY_DEPLOY.md.
#
# Usage:
#   cd c:\Users\SIDDHARTHA\KFS
#   .\infra\railway\deploy.ps1
#
# Pass -Only to deploy a subset:
#   .\infra\railway\deploy.ps1 -Only api,portal
#
# The script runs each `railway up` synchronously so you see build logs.
# Pass -Detach to fire-and-forget (each kicks off then returns).

param(
    [string[]] $Only = @('api', 'portal', 'admin', 'scanner'),
    [switch]   $Detach
)

$ErrorActionPreference = 'Stop'

# Sanity: must run from the repo root so railway up has the whole monorepo
# as its upload context (Railway's per-service Root Directory then narrows
# the build to api/ or web/ as configured).
if (-not (Test-Path 'docker-compose.yml')) {
    Write-Error 'Run this from the repo root (the folder with docker-compose.yml).'
    exit 1
}

$extra = if ($Detach) { '--detach' } else { '' }

foreach ($svc in $Only) {
    Write-Host "==> Deploying $svc" -ForegroundColor Cyan
    & railway up --service $svc $extra
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Deploy failed for $svc (exit $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
}

Write-Host "==> Done. Open the project in Railway to watch logs." -ForegroundColor Green
