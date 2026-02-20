#!/bin/bash
# Query the Discount table to check coupon codes

SERVER_NAME="scoreboard-asql"
DATABASE_NAME="ScoreboardDb"
RESOURCE_GROUP="rg-scoreboard"

echo "========================================"
echo "Checking Discount/Coupon Codes"
echo "========================================"
echo ""

echo "Querying Discount table from Azure SQL..."
echo ""

# Get the admin username
read -p "Enter SQL admin username (e.g., scoreadmin): " ADMIN_USER

# Query the database
az sql db query \
  --server $SERVER_NAME \
  --database $DATABASE_NAME \
  --name $RESOURCE_GROUP \
  --admin-user $ADMIN_USER \
  --query-text "SELECT DiscountId, Code, DiscountPercent, ValidFrom, ValidUntil, IsActive FROM Discount;" \
  -o table

echo ""
echo "========================================"
echo "If no results appear, the Discount table might be empty."
echo "You may need to run your seed data script."
echo "========================================"
