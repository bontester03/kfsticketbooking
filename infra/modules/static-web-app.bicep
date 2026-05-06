@description('Static Web App name.')
param name string

@description('Azure region. Static Web Apps is not yet GA in UAE North — global regions only.')
@allowed(['centralus', 'eastus2', 'westus2', 'westeurope', 'eastasia'])
param location string = 'centralus'

@description('Resource tags.')
param tags object = {}

@description('SKU tier. Free for dev, Standard for prod (custom domains, SLA).')
@allowed(['Free', 'Standard'])
param sku string = 'Free'

resource swa 'Microsoft.Web/staticSites@2023-12-01' = {
  name: name
  location: location
  tags: tags
  sku: { name: sku, tier: sku }
  properties: {
    // The actual GitHub Actions deploy is wired up post-create via the repo workflow,
    // which uses the deployment token output below.
    allowConfigFileUpdates: true
    enterpriseGradeCdnStatus: 'Disabled'
  }
}

output id string = swa.id
output name string = swa.name
output defaultHostname string = 'https://${swa.properties.defaultHostname}'
