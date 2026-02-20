#!/bin/bash
# Register required Azure resource providers

echo "========================================"
echo "Registering Azure Resource Providers"
echo "========================================"
echo ""

# List of required resource providers
PROVIDERS=(
  "Microsoft.Web"
  "Microsoft.Authorization"
  "Microsoft.ManagedIdentity"
  "Microsoft.Sql"
)

echo "Checking and registering resource providers..."
echo ""

for PROVIDER in "${PROVIDERS[@]}"; do
  echo "Checking $PROVIDER..."

  # Check current registration state
  STATE=$(az provider show --namespace $PROVIDER --query "registrationState" -o tsv 2>/dev/null)

  if [ "$STATE" == "Registered" ]; then
    echo "  ✓ Already registered"
  else
    echo "  ⚙ Registering $PROVIDER..."
    az provider register --namespace $PROVIDER --wait
    echo "  ✓ Registered successfully"
  fi
  echo ""
done

echo "========================================"
echo "✓ All Required Providers Registered!"
echo "========================================"
echo ""
echo "You can now proceed with the deployment setup."
