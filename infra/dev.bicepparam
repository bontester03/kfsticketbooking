using './main.bicep'

param env = 'dev'
param location = 'uaenorth'
param project = 'kfs'
param postgresAdminLogin = 'kfsadmin'
// Pull these from your dev Key Vault or pass via `--parameters postgresAdminPassword=...` on the CLI.
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD', '')
// F1 = Free App Service tier: consumes no VM quota (trial subs have 0 VM quota).
// Trade-offs: 60 CPU-min/day, no Always-On (cold starts), 1 GB. Fine to validate the
// deploy; bump to B1 once the client lifts App Service quota for event day.
param appServiceSku = 'F1'
// Smallest Burstable Postgres — lowest quota footprint on a trial subscription.
param postgresSku = {
  name: 'Standard_B1ms'
  tier: 'Burstable'
}
param postgresStorageGB = 32
param jwtSecret = readEnvironmentVariable('JWT_SECRET', '')
param qrSigningKey = readEnvironmentVariable('QR_SIGNING_KEY', '')
param superAdminPassword = readEnvironmentVariable('SUPER_ADMIN_PASSWORD', 'Admin@123')
