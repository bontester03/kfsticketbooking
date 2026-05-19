// KFS Booking — main Bicep template for Azure UAE North.
// Provisions: App Insights, Key Vault, Storage, Postgres Flex, App Service (Linux .NET 8),
// three Static Web Apps (portal/admin/scanner). Wires up Managed Identity → KV/Storage/Postgres.
//
// Deploy:
//   az deployment group create \
//     --resource-group rg-kfs-prod \
//     --template-file infra/main.bicep \
//     --parameters infra/prod.bicepparam
//
// Validate first with `what-if`:
//   az deployment group what-if --resource-group rg-kfs-prod \
//     --template-file infra/main.bicep --parameters infra/prod.bicepparam

targetScope = 'resourceGroup'

@description('Environment short name (dev|prod). Used in resource names.')
@allowed(['dev', 'prod'])
param env string

@description('Azure region. UAE North for data residency.')
param location string = 'uaenorth'

@description('Project short slug. Keep ≤6 chars; rolled into globally-unique names.')
@minLength(3)
@maxLength(8)
param project string = 'kfs'

@description('Postgres administrator login.')
param postgresAdminLogin string = 'kfsadmin'

@secure()
@description('Postgres administrator password. Pull from Key Vault in CI.')
param postgresAdminPassword string

@description('App Service plan SKU. F1 (Free, no VM quota) for trial subs, B1 dev, P1V3 prod.')
@allowed(['F1', 'B1', 'P1V3'])
param appServiceSku string = 'F1'

@description('Postgres Flex SKU. Burstable for dev, GeneralPurpose for prod.')
param postgresSku object = {
  name: 'Standard_B2s'
  tier: 'Burstable'
}

@description('Postgres storage in GB.')
param postgresStorageGB int = 32

@description('JWT signing secret — stored in Key Vault, never in App Settings directly.')
@secure()
param jwtSecret string

@description('QR-code signing key — separate from JWT secret.')
@secure()
param qrSigningKey string

@description('Initial super-admin password.')
@secure()
param superAdminPassword string

var tags = {
  project: 'kfs-booking'
  environment: env
  managedBy: 'bicep'
  region: 'uae-north'
}
var nameSuffix = '${project}-${env}-uaen'
var storageName = toLower('${project}${env}st${uniqueString(resourceGroup().id)}')
// Derived deterministically from the (known) storage account name so the App Service module
// does NOT depend on the storage module's outputs. Without this, appService→storage and
// storage→appService (it needs the MI principalId for its RBAC grant) form a cycle.
var storageBlobEndpoint = 'https://${storageName}.blob.${environment().suffixes.storage}/'

// -------- Application Insights -----------------------------------------------------------------
module appInsights 'modules/app-insights.bicep' = {
  name: 'appInsights-deploy'
  params: {
    name: 'ai-${nameSuffix}'
    location: location
    tags: tags
  }
}

// -------- Key Vault ----------------------------------------------------------------------------
module keyVault 'modules/key-vault.bicep' = {
  name: 'keyVault-deploy'
  params: {
    name: 'kv-${nameSuffix}'
    location: location
    tags: tags
    appServicePrincipalId: appService.outputs.principalId
    secrets: [
      { name: 'JwtSecret', value: jwtSecret }
      { name: 'QrSigningKey', value: qrSigningKey }
      { name: 'SuperAdminPassword', value: superAdminPassword }
      { name: 'PostgresConnectionString', value: postgres.outputs.connectionString }
    ]
  }
}

// -------- Storage Account (qr-codes, printable-batches) ----------------------------------------
module storage 'modules/storage.bicep' = {
  name: 'storage-deploy'
  params: {
    name: storageName
    location: location
    tags: tags
    appServicePrincipalId: appService.outputs.principalId
    containers: ['qr-codes', 'printable-batches']
  }
}

// -------- Postgres Flexible Server -------------------------------------------------------------
module postgres 'modules/postgres.bicep' = {
  name: 'postgres-deploy'
  params: {
    name: 'pg-${nameSuffix}'
    location: location
    tags: tags
    administratorLogin: postgresAdminLogin
    administratorPassword: postgresAdminPassword
    sku: postgresSku
    storageGB: postgresStorageGB
    databaseName: 'kfs'
    extensions: 'CITEXT,PGCRYPTO,PG_TRGM'
  }
}

// -------- App Service (API) --------------------------------------------------------------------
module appService 'modules/app-service.bicep' = {
  name: 'appService-deploy'
  params: {
    name: 'app-${nameSuffix}'
    planName: 'asp-${nameSuffix}'
    location: location
    tags: tags
    sku: appServiceSku
    appSettings: {
      ASPNETCORE_ENVIRONMENT: env == 'prod' ? 'Production' : 'Staging'
      WEBSITE_TIME_ZONE: 'Arab Standard Time'
      Database__RunMigrationsOnStartup: 'true'
      Swagger__Enabled: env == 'prod' ? 'false' : 'true'
      Storage__Provider: 'AzureBlob'
      Storage__ServiceUri: storageBlobEndpoint
      Storage__Container: 'qr-codes'
      Storage__SasLifetimeMinutes: '5'
      'ConnectionStrings__Default': '@Microsoft.KeyVault(VaultName=kv-${nameSuffix};SecretName=PostgresConnectionString)'
      Jwt__Secret: '@Microsoft.KeyVault(VaultName=kv-${nameSuffix};SecretName=JwtSecret)'
      Qr__SigningKey: '@Microsoft.KeyVault(VaultName=kv-${nameSuffix};SecretName=QrSigningKey)'
      Auth__SuperAdminPassword: '@Microsoft.KeyVault(VaultName=kv-${nameSuffix};SecretName=SuperAdminPassword)'
      APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.outputs.connectionString
    }
  }
}

// -------- Static Web Apps (portal, admin, scanner) ---------------------------------------------
// SWA is currently not GA in UAE North — pin to a near region (centralus is the global default
// for SWA). Documented in DECISIONS.md.
module swaPortal 'modules/static-web-app.bicep' = {
  name: 'swa-portal-deploy'
  params: {
    name: 'swa-portal-${env}'
    location: 'centralus'
    tags: union(tags, { app: 'portal' })
  }
}
module swaAdmin 'modules/static-web-app.bicep' = {
  name: 'swa-admin-deploy'
  params: {
    name: 'swa-admin-${env}'
    location: 'centralus'
    tags: union(tags, { app: 'admin' })
  }
}
module swaScanner 'modules/static-web-app.bicep' = {
  name: 'swa-scanner-deploy'
  params: {
    name: 'swa-scanner-${env}'
    location: 'centralus'
    tags: union(tags, { app: 'scanner' })
  }
}

output apiUrl string = appService.outputs.defaultHostname
output portalUrl string = swaPortal.outputs.defaultHostname
output adminUrl string = swaAdmin.outputs.defaultHostname
output scannerUrl string = swaScanner.outputs.defaultHostname
output appServicePrincipalId string = appService.outputs.principalId
output postgresHost string = postgres.outputs.host
output keyVaultName string = keyVault.outputs.name
output storageAccountName string = storage.outputs.name
output appInsightsConnectionString string = appInsights.outputs.connectionString
