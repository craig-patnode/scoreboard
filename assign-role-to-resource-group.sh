#!/bin/bash
# Assign Contributor role to resource group instead of individual app service

set -e

RESOURCE_GROUP="rg-scoreboard"
MANAGED_IDENTITY_NAME="oidc-msi-aef8"

echo "Assigning Contributor role to resource group..."
echo ""

# Get managed identity principal ID
PRINCIPAL_ID=$(az identity show \
  --name $MANAGED_IDENTITY_NAME \
  --resource-group $RESOURCE_GROUP \
  --query principalId -o tsv)

echo "✓ Managed Identity Principal ID: $PRINCIPAL_ID"

# Get subscription ID
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
echo "✓ Subscription ID: $SUBSCRIPTION_ID"

# Get resource group ID
RESOURCE_GROUP_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP"
echo "✓ Resource Group ID: $RESOURCE_GROUP_ID"
echo ""

# Check if role assignment already exists
echo "Checking existing role assignments..."
EXISTING_ROLE=$(az role assignment list \
  --assignee $PRINCIPAL_ID \
  --scope $RESOURCE_GROUP_ID \
  --query "[?roleDefinitionName=='Contributor'].roleDefinitionName" -o tsv)

if [ -z "$EXISTING_ROLE" ]; then
  echo "Assigning Contributor role to managed identity on resource group..."
  az role assignment create \
    --assignee $PRINCIPAL_ID \
    --role "Contributor" \
    --scope $RESOURCE_GROUP_ID
  echo ""
  echo "✓ Role assigned successfully!"
else
  echo "✓ Contributor role already assigned to resource group"
fi

echo ""
echo "========================================"
echo "✓ Setup Complete!"
echo "========================================"
echo ""
echo "The managed identity '$MANAGED_IDENTITY_NAME' now has Contributor access"
echo "to the entire resource group '$RESOURCE_GROUP'."
echo ""
echo "This allows GitHub Actions to deploy to any resource in this group."
