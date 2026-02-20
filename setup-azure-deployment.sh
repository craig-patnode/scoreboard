#!/bin/bash
# Setup Azure Deployment with Federated Credentials
# This script configures your existing managed identity for GitHub Actions deployment

set -e

# Configuration
RESOURCE_GROUP="rg-scoreboard"
MANAGED_IDENTITY_NAME="oidc-msi-aef8"
APP_SERVICE_NAME="scoreboard-app"
GITHUB_REPO="craig-patnode/scoreboard"
GITHUB_BRANCH="main"

echo "========================================"
echo "Azure Deployment Setup for Scoreboard"
echo "========================================"
echo ""

# Step 1: Get Managed Identity Details
echo "Step 1: Getting Managed Identity details..."
CLIENT_ID=$(az identity show \
  --name $MANAGED_IDENTITY_NAME \
  --resource-group $RESOURCE_GROUP \
  --query clientId -o tsv)

PRINCIPAL_ID=$(az identity show \
  --name $MANAGED_IDENTITY_NAME \
  --resource-group $RESOURCE_GROUP \
  --query principalId -o tsv)

TENANT_ID=$(az identity show \
  --name $MANAGED_IDENTITY_NAME \
  --resource-group $RESOURCE_GROUP \
  --query tenantId -o tsv)

echo "✓ Client ID: $CLIENT_ID"
echo "✓ Tenant ID: $TENANT_ID"
echo "✓ Principal ID: $PRINCIPAL_ID"
echo ""

# Step 2: Get Subscription ID
echo "Step 2: Getting Subscription ID..."
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
echo "✓ Subscription ID: $SUBSCRIPTION_ID"
echo ""

# Step 3: Configure Federated Credentials for GitHub
echo "Step 3: Configuring federated credentials for GitHub Actions..."

# Check if federated credential already exists
EXISTING_CRED=$(az identity federated-credential list \
  --identity-name $MANAGED_IDENTITY_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "[?name=='github-scoreboard-main'].name" -o tsv)

if [ -z "$EXISTING_CRED" ]; then
  echo "Creating new federated credential..."
  if az identity federated-credential create \
    --name "github-scoreboard-main" \
    --identity-name $MANAGED_IDENTITY_NAME \
    --resource-group $RESOURCE_GROUP \
    --issuer "https://token.actions.githubusercontent.com" \
    --subject "repo:$GITHUB_REPO:ref:refs/heads/$GITHUB_BRANCH" \
    --audiences "api://AzureADTokenExchange" 2>/dev/null; then
    echo "✓ Federated credential created"
  else
    echo "✓ Federated credential already exists (or creation skipped)"
  fi
else
  echo "✓ Federated credential already exists"
fi
echo ""

# Step 4: Assign Contributor Role to App Service
echo "Step 4: Checking App Service permissions..."
echo "Debug: Using subscription ID: $SUBSCRIPTION_ID"
echo "Debug: App Service name: $APP_SERVICE_NAME"
echo "Debug: Resource group: $RESOURCE_GROUP"
APP_SERVICE_ID=$(az webapp show \
  --name $APP_SERVICE_NAME \
  --resource-group $RESOURCE_GROUP \
  --subscription $SUBSCRIPTION_ID \
  --query id -o tsv)

# Check if role assignment exists
EXISTING_ROLE=$(az role assignment list \
  --assignee $PRINCIPAL_ID \
  --scope $APP_SERVICE_ID \
  --subscription $SUBSCRIPTION_ID \
  --query "[?roleDefinitionName=='Contributor'].roleDefinitionName" -o tsv)

if [ -z "$EXISTING_ROLE" ]; then
  echo "Assigning Contributor role to managed identity..."
  az role assignment create \
    --assignee $PRINCIPAL_ID \
    --role "Contributor" \
    --scope $APP_SERVICE_ID \
    --subscription $SUBSCRIPTION_ID
  echo "✓ Role assigned"
else
  echo "✓ Contributor role already assigned"
fi
echo ""

# Step 5: Display GitHub Secrets
echo "========================================"
echo "GitHub Secrets Configuration"
echo "========================================"
echo ""
echo "Add these secrets to your GitHub repository:"
echo "https://github.com/$GITHUB_REPO/settings/secrets/actions"
echo ""
echo "Secret Name: AZUREAPPSERVICE_CLIENTID_87112EF76FB547EDA1F950B342316998"
echo "Secret Value: $CLIENT_ID"
echo ""
echo "Secret Name: AZUREAPPSERVICE_TENANTID_A9392AF866254E5A85431BA9971B7B40"
echo "Secret Value: $TENANT_ID"
echo ""
echo "Secret Name: AZUREAPPSERVICE_SUBSCRIPTIONID_6E9A4870735D41CD8562ED5ED83A9519"
echo "Secret Value: $SUBSCRIPTION_ID"
echo ""
echo "========================================"
echo "✓ Setup Complete!"
echo "========================================"
echo ""
echo "Next steps:"
echo "1. Add the three secrets above to your GitHub repository"
echo "2. Push to main branch or manually trigger the workflow"
echo "3. Monitor deployment at: https://github.com/$GITHUB_REPO/actions"
