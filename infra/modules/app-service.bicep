@description('App Service name.')
param name string

@description('App Service Plan name.')
param planName string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object = {}

@description('SKU. F1 (Free, no VM quota) for trial subs, B1 dev, P1V3 prod.')
@allowed(['F1', 'B1', 'P1V3'])
param sku string = 'F1'

@description('App settings to apply. Key Vault references use @Microsoft.KeyVault(...) format.')
param appSettings object = {}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  tags: tags
  sku: { name: sku, tier: sku == 'P1V3' ? 'PremiumV3' : (sku == 'F1' ? 'Free' : 'Basic') }
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
      // Free (F1) forbids AlwaysOn; only the prod tier gets it.
      alwaysOn: sku == 'P1V3'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      // Free tier doesn't support the platform health-check feature; omit it on F1.
      healthCheckPath: sku == 'F1' ? null : '/healthz'
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
