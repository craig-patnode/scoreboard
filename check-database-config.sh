#!/bin/bash
# Check Azure SQL Database Configuration

RESOURCE_GROUP="rg-scoreboard"
APP_SERVICE_NAME="scoreboard-app"

echo "========================================"
echo "Checking Database Configuration"
echo "========================================"
echo ""

# Check if SQL Server exists
echo "1. Checking for SQL Servers in resource group..."
SQL_SERVERS=$(az sql server list --resource-group $RESOURCE_GROUP --query "[].{Name:name, Location:location, State:state}" -o table)
if [ -z "$SQL_SERVERS" ]; then
  echo "⚠️  No SQL Servers found in $RESOURCE_GROUP"
else
  echo "$SQL_SERVERS"
fi
echo ""

# Get the first SQL server name (if exists)
SQL_SERVER_NAME=$(az sql server list --resource-group $RESOURCE_GROUP --query "[0].name" -o tsv)

if [ ! -z "$SQL_SERVER_NAME" ]; then
  echo "2. Checking databases on server: $SQL_SERVER_NAME"
  az sql db list --resource-group $RESOURCE_GROUP --server $SQL_SERVER_NAME --query "[].{Name:name, Status:status, Tier:currentServiceObjectiveName}" -o table
  echo ""
fi

# Check App Service connection strings
echo "3. Checking App Service connection strings..."
CONNECTION_STRINGS=$(az webapp config connection-string list \
  --name $APP_SERVICE_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "[].{Name:name, Type:type}" -o table)

if [ -z "$CONNECTION_STRINGS" ]; then
  echo "⚠️  No connection strings configured"
else
  echo "$CONNECTION_STRINGS"
fi
echo ""

# Check App Service configuration settings
echo "4. Checking App Service settings..."
az webapp config appsettings list \
  --name $APP_SERVICE_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "[?name=='ASPNETCORE_ENVIRONMENT' || name=='Jwt__Key'].{Name:name, Value:value}" -o table
echo ""

echo "========================================"
echo "Configuration Check Complete"
echo "========================================"
