@description('App Service name.')
param name string

@description('App Service Plan name.')
param planName string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object = {}

@description('SKU. B1 for dev, P1V3 for prod.')
@allowed(['B1', 'P1V3'])
param sku string = 'B1'

@description('App settings to apply. Key Vault references use @Microsoft.KeyVault(...) format.')
param appSettings object = {}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  tags: tags
  sku: { name: sku, tier: sku == 'P1V3' ? 'PremiumV3' : 'Basic' }
  kind: 'linux'
  properties: { reserved: true } // Linux
}

resource site 'Microsoft.Web/sites@2023-12-01' = {
  name: name
  location: location
  tags: tags
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    reserved: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: sku != 'B1'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      healthCheckPath: '/healthz'
      appSettings: [for setting in items(appSettings): {
        name: setting.key
        value: setting.value
      }]
    }
  }
}

// HSTS / security headers can be added via siteConfig as a prod follow-up.

output id string = site.id
output name string = site.name
output principalId string = site.identity.principalId
output defaultHostname string = 'https://${site.properties.defaultHostName}'
