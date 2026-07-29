#!/usr/bin/env bash
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  echo "This script must run as root." >&2
  exit 1
fi

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
bootstrap_source="${script_dir}/kermaria-nginx-bootstrap.conf"
tls_source="${script_dir}/kermaria-nginx.conf"
activation_source="${script_dir}/activate-kermaria-tls.sh"

for source in "${bootstrap_source}" "${tls_source}" "${activation_source}"; do
  if [[ ! -f "${source}" ]]; then
    echo "Missing deployment input: ${source}" >&2
    exit 1
  fi
done

if [[ -f /etc/ssl/kermaria/fullchain.pem ]] ||
   grep -Eq '^[[:space:]]*listen[[:space:]].*443' /etc/nginx/sites-available/kermaria 2>/dev/null; then
  echo "Refusing to replace an active TLS configuration with the HTTP bootstrap." >&2
  exit 1
fi

export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y --no-install-recommends nginx ca-certificates curl openssl

backup_dir="/var/backups/kermaria-srv11/$(date -u +%Y%m%dT%H%M%SZ)"
install -d -m 700 "${backup_dir}"
for path in /etc/nginx/sites-available/kermaria /etc/nginx/sites-enabled/kermaria; do
  if [[ -e "${path}" || -L "${path}" ]]; then
    cp -a "${path}" "${backup_dir}/"
  fi
done

install -d -m 755 /var/www/letsencrypt /etc/ssl/kermaria
install -o root -g root -m 644 "${bootstrap_source}" /etc/nginx/sites-available/kermaria
install -o root -g root -m 644 "${tls_source}" /etc/nginx/sites-available/kermaria-tls.pending
install -d -m 755 /usr/local/lib/kermaria
install -o root -g root -m 750 "${activation_source}" /usr/local/lib/kermaria/activate-kermaria-tls.sh

rm -f /etc/nginx/sites-enabled/default
ln -sfn /etc/nginx/sites-available/kermaria /etc/nginx/sites-enabled/kermaria
nginx -t
systemctl enable nginx.service
systemctl restart nginx.service

curl --fail --silent --show-error --max-time 10 \
  --header 'Host: dashboard.zacharyhounsa.ovh' \
  http://192.168.100.211/api/health/ready >/dev/null

echo "nginx_bootstrap=active"
echo "tls_configuration=staged_pending_certificate"
echo "backup=${backup_dir}"
