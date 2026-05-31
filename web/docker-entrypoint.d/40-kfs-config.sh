#!/bin/sh
set -eu

api_base="${VITE_API_BASE_URL:-/api/v1}"
scanner_url="${VITE_SCANNER_URL:-}"

cat > /usr/share/nginx/html/config.js <<EOF
window.__KFS_CONFIG__ = {
  apiBaseUrl: "${api_base}",
  scannerUrl: "${scanner_url}"
};
EOF
