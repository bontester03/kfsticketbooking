using './main.bicep'

param env = 'prod'
param location = 'uaenorth'
param project = 'kfs'
param postgresAdminLogin = 'kfsadmin'
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD', '')
param appServiceSku = 'P1V3'
param postgresSku = {
  name: 'Standard_D2ds_v5'
  tier: 'GeneralPurpose'
}
param postgresStorageGB = 64
param jwtSecret = readEnvironmentVariable('JWT_SECRET', '')
param qrSigningKey = readEnvironmentVariable('QR_SIGNING_KEY', '')
param superAdminPassword = readEnvironmentVariable('SUPER_ADMIN_PASSWORD', '')
