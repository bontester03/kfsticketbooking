@description('Postgres Flexible Server name.')
param name string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object = {}

@description('Administrator login.')
param administratorLogin string

@secure()
@description('Administrator password.')
param administratorPassword string

@description('SKU object: name (e.g. Standard_B2s) and tier (Burstable / GeneralPurpose / MemoryOptimized).')
param sku object

@description('Storage in GB.')
param storageGB int = 32

@description('Database name.')
param databaseName string = 'kfs'

@description('Comma-separated extensions to enable via azure.extensions.')
param extensions string = 'CITEXT,PGCRYPTO,PG_TRGM'

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2023-12-01-preview' = {
  name: name
  location: location
  tags: tags
  sku: sku
  properties: {
    version: '16'
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorPassword
    storage: { storageSizeGB: storageGB, autoGrow: 'Enabled' }
    backup: { backupRetentionDays: 7, geoRedundantBackup: 'Disabled' }
    highAvailability: { mode: 'Disabled' }
    network: { publicNetworkAccess: 'Enabled' } // VNet integration is a prod follow-up
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Enabled'
      tenantId: subscription().tenantId
    }
  }
}

resource extConfig 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2023-12-01-preview' = {
  parent: server
  name: 'azure.extensions'
  properties: {
    value: extensions
    source: 'user-override'
  }
}

resource db 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-12-01-preview' = {
  parent: server
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// Allow Azure-internal services (App Service) — broad rule for now; tighten with private
// endpoint + VNet integration as a prod follow-up.
resource fwAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-12-01-preview' = {
  parent: server
  name: 'AllowAllAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output id string = server.id
output host string = server.properties.fullyQualifiedDomainName
output databaseName string = db.name
output connectionString string = 'Host=${server.properties.fullyQualifiedDomainName};Port=5432;Database=${db.name};Username=${administratorLogin};Password=${administratorPassword};SslMode=Require;Trust Server Certificate=true'
