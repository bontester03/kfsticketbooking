#!/bin/sh
set -eu

# Default values keep local docker-compose / bare runs working without any env wiring.
: "${PORT:=80}"
: "${API_URL:=/api}"
: "${LOCAL_API_UPSTREAM:=http://api:8080/api/}"

export PORT API_URL LOCAL_API_UPSTREAM

# Runtime config consumed by the Angular bundle on bootstrap.
mkdir -p /usr/share/nginx/html/assets
cat > /usr/share/nginx/html/assets/config.json <<EOF
{
  "apiUrl": "${API_URL}"
}
EOF

# Render nginx config with the resolved port + upstream.
envsubst '$PORT $LOCAL_API_UPSTREAM' \
    < /etc/nginx/templates/default.conf.template \
    > /etc/nginx/conf.d/default.conf

exec nginx -g 'daemon off;'
