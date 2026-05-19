using './main.bicep'

param env = 'dev'
param location = 'uaenorth'
param project = 'kfs'
param postgresAdminLogin = 'kfsadmin'
// Pull these from your dev Key Vault or pass via `--parameters postgresAdminPassword=...` on the CLI.
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD', '')
// B1 Basic: ~$13/mo, Always-On, no daily CPU cap. Requires App Service VM quota,
// which the Pay-As-You-Go upgrade unlocks (Free Trial had 0).
param appServiceSku = 'B1'
// Burstable B1ms (1 vCore / 2 GB) — cheap (~$12/mo) and ample for a single event.
param postgresSku = {
  name: 'Standard_B1ms'
  tier: 'Burstable'
}
param postgresStorageGB = 32
param jwtSecret = readEnvironmentVariable('JWT_SECRET', '')
param qrSigningKey = readEnvironmentVariable('QR_SIGNING_KEY', '')
param superAdminPassword = readEnvironmentVariable('SUPER_ADMIN_PASSWORD', 'Admin@123')
