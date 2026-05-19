# Azure deployment runbook — KFS Booking

> This machine can't run `az` (corporate TLS-intercepting proxy blocks `login.microsoftonline.com`).
> Everything Azure-side is done in **Azure Cloud Shell** — a browser terminal *inside* Azure, so
> there's no local proxy and `az` is already signed in. The actual app/infra deploy then runs from
> **GitHub Actions** (`.github/workflows/deploy.yml`) using OIDC — no secrets ever stored in Azure.

## Region

- Target region: **UAE North (`uaenorth`)**.
- Azure **Saudi Arabia** regions are **not GA until Q4 2026** (today is within 2026 H1), so they
  can't host this yet. UAE North is the nearest GA region and is what the Bicep + params target.
- Data-residency / Saudi PDPL note is in [../DECISIONS.md](../DECISIONS.md). Plan: migrate to
  Saudi East when it goes GA — it's a `location` param flip, nothing else.
- ⚠️ Some subscriptions can't deploy to UAE North without a quota/region request. Step 1 checks this.

## Cost

Default deploy = **`dev`** params: App Service **B1** + Postgres **Burstable B2s** + 32 GB +
3 Static Web Apps (Free) ≈ **~$50/month** → roughly 4 months on a $200 credit.
The $200 trial credit itself expires 30 days after activation regardless of balance — confirm the
client's credit window before relying on it for event day.

---

## Step 0 — Sign in (browser)

1. Open <https://portal.azure.com> in a normal browser.
2. Sign in with the account the client provided (the `…@Kfsksa.onmicrosoft.com` one).
   Type the password directly into the Microsoft login page — never paste it into a terminal or chat.
3. Top bar → the **Cloud Shell** icon (`>_`). Choose **Bash**. If first run, accept the storage prompt.

## Step 1 — Pick subscription + check region

```bash
az account show --query "{sub:name, id:id, tenant:tenantId}" -o table

# If multiple subscriptions, pick the one with the credit:
# az account list -o table
# az account set --subscription "<SUBSCRIPTION_ID>"

# Is UAE North usable here?  (no output for a row = OK; an error = region/quota request needed)
az provider show -n Microsoft.DBforPostgreSQL --query "resourceTypes[?resourceType=='flexibleServers'].locations | [0]" -o tsv | tr ',' '\n' | grep -i "uae north" || echo "UAE NORTH NOT LISTED — open a region/quota request"
```

## Step 2 — Register resource providers (fresh subscriptions need this)

```bash
for p in Microsoft.Web Microsoft.DBforPostgreSQL Microsoft.Storage Microsoft.KeyVault \
         Microsoft.Insights Microsoft.OperationalInsights Microsoft.ManagedIdentity; do
  az provider register -n $p
done
# Re-run after a few minutes; all should read "Registered":
az provider list --query "[?namespace=='Microsoft.Web' || namespace=='Microsoft.DBforPostgreSQL'].{p:namespace,s:registrationState}" -o table
```

## Step 3 — Resource group

```bash
az group create --name rg-kfs-dev --location uaenorth
```

## Step 4 — App registration + GitHub OIDC federation

```bash
SUB_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)
REPO="bontester03/kfsticketbooking"

APP_ID=$(az ad app create --display-name "kfs-booking-gha" --query appId -o tsv)
az ad sp create --id "$APP_ID"

# Federated credential — subject MUST match the workflow's `environment: dev`
az ad app federated-credential create --id "$APP_ID" --parameters "{
  \"name\": \"gha-dev\",
  \"issuer\": \"https://token.actions.githubusercontent.com\",
  \"subject\": \"repo:${REPO}:environment:dev\",
  \"audiences\": [\"api://AzureADTokenExchange\"]
}"

# Roles: Contributor (create resources) + User Access Administrator (the Bicep assigns
# Key Vault / Storage roles to the App Service managed identity).
SP_OID=$(az ad sp show --id "$APP_ID" --query id -o tsv)
SCOPE="/subscriptions/${SUB_ID}/resourceGroups/rg-kfs-dev"
az role assignment create --assignee-object-id "$SP_OID" --assignee-principal-type ServicePrincipal --role "Contributor" --scope "$SCOPE"
az role assignment create --assignee-object-id "$SP_OID" --assignee-principal-type ServicePrincipal --role "User Access Administrator" --scope "$SCOPE"

echo "AZURE_CLIENT_ID       = $APP_ID"
echo "AZURE_TENANT_ID       = $TENANT_ID"
echo "AZURE_SUBSCRIPTION_ID = $SUB_ID"
```

Copy those three values.

## Step 5 — GitHub repo configuration

In <https://github.com/bontester03/kfsticketbooking> → **Settings**:

1. **Environments → New environment → `dev`** (the name must be exactly `dev`).
2. In that `dev` environment, add **Environment secrets**:

   | Secret | Value |
   | --- | --- |
   | `AZURE_CLIENT_ID` | from Step 4 |
   | `AZURE_TENANT_ID` | from Step 4 |
   | `AZURE_SUBSCRIPTION_ID` | from Step 4 |
   | `AZURE_RESOURCE_GROUP` | `rg-kfs-dev` |
   | `POSTGRES_ADMIN_PASSWORD` | a strong 16+ char string |
   | `JWT_SECRET` | random 32+ chars |
   | `QR_SIGNING_KEY` | random 32+ chars, different from `JWT_SECRET` |
   | `SUPER_ADMIN_PASSWORD` | the first admin login password |

3. Add an **Environment variable** (not secret): `AZURE_APP_NAME = app-kfs-dev-uaen`
   (that's the App Service name `main.bicep` produces for `env=dev`).

Generate good secrets in Cloud Shell:
```bash
openssl rand -base64 32   # run 3× for POSTGRES_ADMIN_PASSWORD, JWT_SECRET, QR_SIGNING_KEY
```

## Step 6 — Deploy

GitHub → **Actions → "Deploy to Azure" → Run workflow → environment: `dev` → Run**.

The workflow: OIDC login → `az deployment group what-if` → `create` (provisions everything in
`rg-kfs-dev`, UAE North) → publishes the .NET API → smoke-tests `/healthz`.

First run takes ~15–20 min (Postgres Flexible Server is the slow part).

## Step 7 — Post-deploy

```bash
az webapp show -g rg-kfs-dev -n app-kfs-dev-uaen --query defaultHostName -o tsv
curl -fsS https://<that-host>/healthz && echo OK
```

The API seeds the event + 304 VIP seats + super-admin on first boot. Static Web Apps (portal/admin/
scanner) deploy separately — see the follow-up note in [README.md](../README.md); they need their
own SWA build workflow which is the next deployment task.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `AADSTS70021` / no federated match | Subject must be exactly `repo:bontester03/kfsticketbooking:environment:dev`; the GH environment must be named `dev`. |
| `RequestDisallowedByPolicy` / region | Subscription can't use UAE North — raise an Azure region/quota request, or get the client to enable it. |
| `AuthorizationFailed` on role assignment in Bicep | The SP is missing **User Access Administrator** on the RG (Step 4). |
| Postgres deploy times out | Re-run the workflow — Flexible Server provisioning is occasionally slow; the deployment is idempotent. |
