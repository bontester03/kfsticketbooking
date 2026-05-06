# Infrastructure (Azure UAE North)

Bicep templates that provision the entire KFS Booking footprint:

| Resource                        | File                                | Notes                                    |
| ------------------------------- | ----------------------------------- | ---------------------------------------- |
| Application Insights + Log Analytics | [modules/app-insights.bicep](modules/app-insights.bicep) | Telemetry sink                          |
| Key Vault                       | [modules/key-vault.bicep](modules/key-vault.bicep)         | RBAC mode, secrets seeded at deploy     |
| Storage Account                 | [modules/storage.bicep](modules/storage.bicep)             | Containers `qr-codes`, `printable-batches` |
| Postgres Flexible Server (v16)  | [modules/postgres.bicep](modules/postgres.bicep)           | citext / pgcrypto / pg_trgm enabled     |
| App Service Linux .NET 8        | [modules/app-service.bicep](modules/app-service.bicep)     | Health probe `/healthz`, KV refs        |
| Static Web App × 3              | [modules/static-web-app.bicep](modules/static-web-app.bicep) | One per frontend                       |

## Deploy

```bash
# 1. Login + select subscription
az login
az account set --subscription <subId>

# 2. Create the resource group in UAE North
az group create --name rg-kfs-prod --location uaenorth

# 3. Validate first
az deployment group what-if \
  --resource-group rg-kfs-prod \
  --template-file infra/main.bicep \
  --parameters infra/prod.bicepparam

# 4. Apply
az deployment group create \
  --resource-group rg-kfs-prod \
  --template-file infra/main.bicep \
  --parameters infra/prod.bicepparam
```

## Secrets

Pass at deploy time via env vars or `--parameters key=value` on the CLI. **Never** put plaintext secrets in `*.bicepparam` files committed to the repo:

```bash
export POSTGRES_ADMIN_PASSWORD='...'    # ≥12 chars, mixed case + digits + symbol
export JWT_SECRET='...'                  # ≥32 chars
export QR_SIGNING_KEY='...'              # ≥32 chars, separate from JWT_SECRET
export SUPER_ADMIN_PASSWORD='...'
```

In CI, source these from the deploy environment's Key Vault via `az keyvault secret show ...` before running the Bicep deploy.

## Outputs

The template exposes the URLs and resource names you'll need to wire up post-deploy:

- `apiUrl` — App Service public URL (also healthcheck target for SWAs)
- `portalUrl` / `adminUrl` / `scannerUrl` — Static Web App default hostnames
- `keyVaultName` / `storageAccountName` / `postgresHost` — for further configuration

## Region notes

- **Compute, Storage, Postgres, Key Vault, App Insights → UAE North** (data residency).
- **Static Web Apps → centralus** (SWA is not GA in UAE North as of writing). Documented in [DECISIONS.md](../DECISIONS.md).

## Tightening for prod (not in this commit)

- Switch Postgres to private-endpoint + VNet integration (`publicNetworkAccess: 'Disabled'`).
- Add Azure Front Door + WAF in front of the App Service.
- Tighten Storage `networkAcls.defaultAction = 'Deny'` and rely on VNet rules.
- Enable Postgres geo-redundant backups if cross-region DR is acceptable for your data-residency posture.
