#!/usr/bin/env bash
set -euo pipefail

NODE_VERSION="${NODE_VERSION:-24.18.0}"
INSTALL_ROOT="/opt/kermaria"

if [[ "$(id -u)" -ne 0 ]]; then
  echo "This script must run as root." >&2
  exit 1
fi

case "$(uname -m)" in
  x86_64) node_arch="x64" ;;
  aarch64|arm64) node_arch="arm64" ;;
  *)
    echo "Unsupported architecture: $(uname -m)" >&2
    exit 1
    ;;
esac

archive="node-v${NODE_VERSION}-linux-${node_arch}.tar.xz"
release_url="https://nodejs.org/dist/v${NODE_VERSION}"
target="${INSTALL_ROOT}/node-v${NODE_VERSION}-linux-${node_arch}"

if [[ -x "${target}/bin/node" ]] &&
   [[ "$("${target}/bin/node" --version)" == "v${NODE_VERSION}" ]]; then
  ln -sfn "${target}" "${INSTALL_ROOT}/node"
  echo "Node.js v${NODE_VERSION} is already installed."
  exit 0
fi

export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y --no-install-recommends ca-certificates curl xz-utils

work_dir="$(mktemp -d)"
trap 'rm -rf "${work_dir}"' EXIT
cd "${work_dir}"

curl --fail --silent --show-error --location --output "${archive}" \
  "${release_url}/${archive}"
curl --fail --silent --show-error --location --output SHASUMS256.txt \
  "${release_url}/SHASUMS256.txt"
grep "  ${archive}$" SHASUMS256.txt | sha256sum --check --strict -

mkdir -p "${INSTALL_ROOT}"
tar -xJf "${archive}" -C "${INSTALL_ROOT}"
ln -sfn "${target}" "${INSTALL_ROOT}/node"
ln -sfn "${INSTALL_ROOT}/node/bin/node" /usr/local/bin/node
ln -sfn "${INSTALL_ROOT}/node/bin/npm" /usr/local/bin/npm
ln -sfn "${INSTALL_ROOT}/node/bin/npx" /usr/local/bin/npx

echo "Installed $("${INSTALL_ROOT}/node/bin/node" --version) with npm $("${INSTALL_ROOT}/node/bin/npm" --version)."
