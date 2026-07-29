#!/usr/bin/env bash
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  echo "This script must run as root." >&2
  exit 1
fi

certificate="/etc/ssl/kermaria/fullchain.pem"
private_key="/etc/ssl/kermaria/privkey.pem"
pending_config="/etc/nginx/sites-available/kermaria-tls.pending"
active_config="/etc/nginx/sites-available/kermaria"
required_names=(
  "www.zacharyhounsa.ovh"
  "dashboard.zacharyhounsa.ovh"
  "administration.zacharyhounsa.ovh"
)

for path in "${certificate}" "${private_key}" "${pending_config}"; do
  if [[ ! -f "${path}" ]]; then
    echo "Missing TLS input: ${path}" >&2
    exit 1
  fi
done

if ! openssl x509 -checkend 2592000 -noout -in "${certificate}"; then
  echo "Certificate expires in less than 30 days." >&2
  exit 1
fi

subject_names="$(openssl x509 -in "${certificate}" -noout -ext subjectAltName)"
for name in "${required_names[@]}"; do
  if ! grep -Fq "DNS:${name}" <<<"${subject_names}"; then
    echo "Certificate does not cover ${name}." >&2
    exit 1
  fi
done

certificate_key_hash="$(openssl x509 -in "${certificate}" -pubkey -noout | openssl pkey -pubin -outform der | sha256sum | cut -d' ' -f1)"
private_key_hash="$(openssl pkey -in "${private_key}" -pubout -outform der | sha256sum | cut -d' ' -f1)"
if [[ "${certificate_key_hash}" != "${private_key_hash}" ]]; then
  echo "Certificate and private key do not match." >&2
  exit 1
fi

chown root:root "${certificate}" "${private_key}"
chmod 644 "${certificate}"
chmod 600 "${private_key}"

backup="${active_config}.bootstrap.$(date -u +%Y%m%dT%H%M%SZ)"
cp -a "${active_config}" "${backup}"
rollback_on_error() {
  local status="$?"
  cp -a "${backup}" "${active_config}"
  nginx -t >/dev/null
  systemctl reload nginx.service
  echo "TLS activation failed; bootstrap configuration restored." >&2
  exit "${status}"
}
trap rollback_on_error ERR

install -o root -g root -m 644 "${pending_config}" "${active_config}"
if ! nginx -t; then
  trap - ERR
  cp -a "${backup}" "${active_config}"
  nginx -t
  echo "TLS activation rejected; bootstrap configuration restored." >&2
  exit 1
fi

systemctl reload nginx.service
expected_fingerprint="$(openssl x509 -in "${certificate}" -noout -fingerprint -sha256 | cut -d= -f2)"
served_fingerprint="$(
  openssl s_client -connect 192.168.100.211:443 \
    -servername dashboard.zacharyhounsa.ovh </dev/null 2>/dev/null |
    openssl x509 -noout -fingerprint -sha256 | cut -d= -f2
)"
if [[ "${expected_fingerprint}" != "${served_fingerprint}" ]]; then
  trap - ERR
  cp -a "${backup}" "${active_config}"
  nginx -t
  systemctl reload nginx.service
  echo "nginx does not serve the expected certificate; bootstrap configuration restored." >&2
  exit 1
fi

# Origin certificates are trusted by Cloudflare, not by a browser trust store.
curl --fail --silent --show-error --max-time 10 \
  --insecure \
  --resolve dashboard.zacharyhounsa.ovh:443:192.168.100.211 \
  https://dashboard.zacharyhounsa.ovh/api/health/ready >/dev/null

trap - ERR
echo "tls_configuration=active"
openssl x509 -in "${certificate}" -noout -subject -issuer -dates
