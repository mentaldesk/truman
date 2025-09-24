#!/bin/sh
# Write the runtime configuration to config.js
: "${VITE_API_URL:=http://localhost:5001}"
: "${VITE_ENVIRONMENT:=production}"
: "${Sentry__Dsn:=}"

VERSION="dev"
if [ -f /usr/share/nginx/html/version.txt ]; then
  VERSION=$(cat /usr/share/nginx/html/version.txt)
fi

cat > /usr/share/nginx/html/config.js << EOF
window.__API_URL__ = "${VITE_API_URL}";
window.__ENVIRONMENT__ = "${VITE_ENVIRONMENT}";
window.__SENTRY_DSN__ = "${Sentry__Dsn}";
window.__VERSION__ = "${VERSION}";
EOF

exec nginx -g 'daemon off;'
