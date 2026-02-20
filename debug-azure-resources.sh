#!/bin/bash
# Debug Azure resources

RESOURCE_GROUP="rg-scoreboard"
APP_SERVICE_NAME="scoreboard-app"

echo "Debugging Azure Resources"
echo "========================="
echo ""

# Check current subscription
echo "Current subscription:"
az account show --query "{Name:name, SubscriptionId:id, State:state}" -o table
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
echo ""

# List all resource groups
echo "All resource groups in subscription:"
az group list --query "[].name" -o table
echo ""

# Check if our resource group exists
echo "Checking resource group: $RESOURCE_GROUP"
if az group show --name $RESOURCE_GROUP &>/dev/null; then
  echo "✓ Resource group exists"
else
  echo "✗ Resource group NOT found"
  exit 1
fi
echo ""

# List all app services in the resource group
echo "All App Services in $RESOURCE_GROUP:"
az webapp list --resource-group $RESOURCE_GROUP --query "[].{Name:name, State:state, Location:location}" -o table
echo ""

# Try to get our specific app service with explicit subscription
echo "Trying to access App Service: $APP_SERVICE_NAME"
if az webapp show --name $APP_SERVICE_NAME --resource-group $RESOURCE_GROUP --subscription $SUBSCRIPTION_ID &>/dev/null; then
  echo "✓ App Service is accessible"
  az webapp show --name $APP_SERVICE_NAME --resource-group $RESOURCE_GROUP --subscription $SUBSCRIPTION_ID --query "{Name:name, State:state, ResourceGroup:resourceGroup}" -o table
else
  echo "✗ App Service NOT accessible or doesn't exist"
fi
