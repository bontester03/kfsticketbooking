using './main.bicep'

param env = 'dev'
param location = 'uaenorth'
param project = 'kfs'
param postgresAdminLogin = 'kfsadmin'
// Pull these from your dev Key Vault or pass via `--parameters postgresAdminPassword=...` on the CLI.
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD', '')
param appServiceSku = 'B1'
param postgresSku = {
  name: 'Standard_B2s'
  tier: 'Burstable'
}
param postgresStorageGB = 32
param jwtSecret = readEnvironmentVariable('JWT_SECRET', '')
param qrSigningKey = readEnvironmentVariable('QR_SIGNING_KEY', '')
param superAdminPassword = readEnvironmentVariable('SUPER_ADMIN_PASSWORD', 'Admin@123')
