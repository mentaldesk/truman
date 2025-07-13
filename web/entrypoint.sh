#!/bin/sh
# Write the runtime API URL to config.js
: "${VITE_API_URL:=http://localhost:8080}"
echo "window.__API_URL__ = \"${VITE_API_URL}\";" > /usr/share/nginx/html/config.js
exec nginx -g 'daemon off;' 