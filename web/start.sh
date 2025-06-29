#!/usr/bin/env bash

# Exit immediately on error, treat unset vars as errors, and fail pipelines
set -euo pipefail

# Enable better error tracing
trap 'echo "❌ Error on line $LINENO. Command: $BASH_COMMAND"; exit 1;' ERR

# Ensure we're in the web directory
if [[ ! -f "package.json" ]]; then
    echo "❌ Error: Must be run from the web directory"
    echo "Please cd to the web directory first"
    exit 1
fi

# Print startup banner
echo "🛠️  Starting Truman Web Development Server"
echo "============================================"

echo "🧹 Cleaning cache and node modules..."
rm -rf .svelte-kit node_modules/.vite

# Ensure yarn is installed
if ! command -v yarn >/dev/null 2>&1; then
    echo "🚫 yarn is not installed. Please install it and try again."
    echo "💡 You can install yarn via: npm install -g yarn"
    exit 1
fi

echo "📦 Installing dependencies..."
yarn install

echo "🚀 Starting dev server..."
yarn dev