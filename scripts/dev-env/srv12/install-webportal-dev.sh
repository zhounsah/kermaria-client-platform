#!/usr/bin/env bash
# Installe ou met a jour le WebPortal DEV sur SRV-12, a cote de la production.
#
# Usage (en root) : install-webportal-dev.sh <archive.tar.gz> <release-name>
#   L'environnement DEV est lu sur l'entree standard et ecrit dans
#   /etc/kermaria/webportal-dev.env (root:root 0600). Aucun secret en argument.
#
# Purement additif : kermaria-webportal.service, /opt/kermaria/webportal,
# /opt/kermaria/releases et /etc/kermaria/webportal.env ne sont jamais touches.
set -euo pipefail

archive="$1"
release_name="$2"
releases_root=/opt/kermaria/releases-dev
release_dir="$releases_root/$release_name"
link=/opt/kermaria/webportal-dev
env_file=/etc/kermaria/webportal-dev.env
unit=kermaria-webportal-dev.service
stamp=$(date +%Y%m%d-%H%M%S)

case "$release_dir" in
  /opt/kermaria/releases/*|/opt/kermaria/webportal) echo "Garde-fou : cible PROD" >&2; exit 1 ;;
esac
if tar -tzf "$archive" | grep -q '\\'; then
  echo "Archive refusee : separateurs Windows dans les chemins" >&2; exit 1
fi

id kermaria-web-dev >/dev/null 2>&1 || \
  useradd --system --no-create-home --shell /usr/sbin/nologin kermaria-web-dev

install -d -m 0755 -o root -g root "$releases_root"
install -d -m 0750 -o kermaria-web-dev -g kermaria-web-dev /var/log/kermaria-dev

# Environnement : sauvegarde de la version precedente, jamais d'ecrasement muet.
if [ -f "$env_file" ]; then cp -p "$env_file" "$env_file.bak-$stamp"; fi
umask 077
cat > "$env_file.new"
if grep -Eq '^(STRIPE_SECRET_KEY|STRIPE_PUBLISHABLE_KEY)=.*(sk|rk|pk)_live_' "$env_file.new"; then
  rm -f "$env_file.new"; echo "Environnement DEV refuse : cle Stripe live" >&2; exit 1
fi
grep -q '^APP_ENV=Development$' "$env_file.new" || { rm -f "$env_file.new"; echo "APP_ENV=Development requis" >&2; exit 1; }
chown root:root "$env_file.new"; chmod 0600 "$env_file.new"; mv "$env_file.new" "$env_file"
umask 022

# Release : extraction, cache Next inscriptible par le compte DEV uniquement.
install -d -m 0755 -o root -g root "$release_dir"
tar -xzf "$archive" -C "$release_dir"
chown -R root:root "$release_dir"
chmod -R u=rwX,go=rX "$release_dir"
install -d -m 0750 -o kermaria-web-dev -g kermaria-web-dev "$release_dir/apps/webportal/.next/cache"
ln -sfn "$release_dir" "$link"

install -m 0644 -o root -g root "$(dirname "$0")/kermaria-webportal-dev.service" "/etc/systemd/system/$unit"
systemctl daemon-reload
systemctl enable "$unit" >/dev/null 2>&1
systemctl restart "$unit"
sleep 4
systemctl --no-pager --lines=0 status "$unit" | head -5
