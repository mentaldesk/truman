#!/bin/sh
# Write the runtime configuration to config.js
: "${VITE_API_URL:=http://localhost:5001}"
: "${VITE_ENVIRONMENT:=production}"
: "${Sentry__Dsn:=}"

cat > /usr/share/nginx/html/config.js << EOF
window.__API_URL__ = "${VITE_API_URL}";
window.__ENVIRONMENT__ = "${VITE_ENVIRONMENT}";
window.__SENTRY_DSN__ = "${Sentry__Dsn}";
EOF

exec nginx -g 'daemon off;' 