#!/bin/bash

# Generate EF migrations bundle
# This script creates an executable that can run database migrations

set -e  # Exit on any error

echo "Generating EF migrations bundle..."

# Change to the Truman.Api directory (in case script is run from elsewhere)
cd "$(dirname "$0")"

# Generate the bundle
dotnet ef migrations bundle \
  --project "../Truman.Data/Truman.Data.csproj" \
  --startup-project "Truman.Api.csproj" \
  --output "efbundle" \
  --force

echo "Bundle generated successfully: $(pwd)/efbundle"
echo ""
echo "To run migrations:"
echo "  ./efbundle --connection \"your-connection-string\""