@description('Resource name.')
param name string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object = {}

@description('App Service Managed Identity object ID — granted Key Vault Secrets User.')
param appServicePrincipalId string

// Each element is { name, value }. The *values* originate from @secure() params in
// main.bicep; @secure() can't decorate an array type, so it's omitted here. Bicep still
// keeps the individual secret values out of deployment output because their source params
// upstream are secure.
@description('Secrets to store at deploy time: array of { name, value }.')
param secrets array = []

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled' // tighten with private endpoint in prod follow-up
  }
}

// Secrets User RBAC role for App Service MI.
var secretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
resource appServiceKvAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, appServicePrincipalId, secretsUserRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsUserRoleId)
    principalId: appServicePrincipalId
    principalType: 'ServicePrincipal'
  }
}

@batchSize(1)
resource secretResources 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = [for s in secrets: {
  parent: keyVault
  name: s.name
  properties: {
    value: s.value
    contentType: 'text/plain'
  }
}]

output id string = keyVault.id
output name string = keyVault.name
output uri string = keyVault.properties.vaultUri
