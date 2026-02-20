#!/bin/bash
# Check Azure CLI login status and subscription

echo "Checking Azure CLI status..."
echo ""

# Check if logged in
echo "Current account:"
az account show --query "{Name:name, SubscriptionId:id, TenantId:tenantId}" -o table
echo ""

# List all subscriptions
echo "Available subscriptions:"
az account list --query "[].{Name:name, SubscriptionId:id, IsDefault:isDefault}" -o table
