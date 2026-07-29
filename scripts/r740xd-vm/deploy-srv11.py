#!/usr/bin/env python3
"""Install the safe nginx bootstrap configuration on KERMARIA-SRV-11."""

from __future__ import annotations

import argparse
import base64
import hashlib
import re
import shlex
import sys
import uuid
from pathlib import Path

import paramiko


HOST = "192.168.100.211"
EXPECTED_HOSTNAME = "KERMARIA-SRV-11"
HOST_KEY_SHA256 = "H90b48dxyBmq6Mcvw9kF4xrVVBZ9ISc9DC5jsaJlrfg"

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


class PinnedHostKeyPolicy(paramiko.MissingHostKeyPolicy):
    def missing_host_key(self, client, hostname, key) -> None:  # noqa: ANN001
        actual = base64.b64encode(hashlib.sha256(key.asbytes()).digest()).decode()
        if actual.rstrip("=") != HOST_KEY_SHA256:
            raise paramiko.SSHException(
                f"Unexpected SSH host key for {hostname}: SHA256:{actual.rstrip('=')}"
            )
        client.get_host_keys().add(hostname, key.get_name(), key)


def parse_credentials(path: Path) -> tuple[str, str]:
    username = None
    password = None
    for raw_line in path.read_text(encoding="utf-8-sig").splitlines():
        line = raw_line.strip()
        if re.match(r"(?i)^SRV-13\s*[:=]", line):
            break
        match = re.match(r"^([^:=]+)\s*[:=]\s*(.*)$", line)
        if not match:
            continue
        key, value = match.group(1).strip().lower(), match.group(2).strip()
        if key in {"identifiant", "utilisateur", "username", "login"}:
            username = value
        elif key in {"mdp", "mot de passe", "password"}:
            password = value
    if not username or not password:
        raise ValueError("Missing SRV-11/12 SSH credentials")
    return username, password


def run(
    client: paramiko.SSHClient,
    command: str,
    *,
    sudo_password: str | None = None,
    timeout: int = 120,
) -> str:
    if sudo_password is not None:
        command = f"sudo -k -S -p '' bash -lc {shlex.quote(command)}"
    stdin, stdout, stderr = client.exec_command(command, timeout=timeout)
    if sudo_password is not None:
        stdin.write(sudo_password + "\n")
        stdin.flush()
    stdin.close()
    output = stdout.read().decode("utf-8", errors="replace")
    error = stderr.read().decode("utf-8", errors="replace")
    status = stdout.channel.recv_exit_status()
    if status != 0:
        raise RuntimeError(error.strip() or output.strip() or f"Command failed: {status}")
    return output.strip()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--credentials", type=Path, required=True)
    args = parser.parse_args()

    source_dir = Path(__file__).parent / "srv11"
    sources = (
        source_dir / "kermaria-nginx-bootstrap.conf",
        source_dir / "kermaria-nginx.conf",
        source_dir / "activate-kermaria-tls.sh",
        source_dir / "install-nginx-bootstrap.sh",
    )
    for source in sources:
        if not source.is_file():
            raise FileNotFoundError(source)

    username, password = parse_credentials(args.credentials.resolve())
    remote_dir = f"/tmp/kermaria-srv11-{uuid.uuid4().hex}"
    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(PinnedHostKeyPolicy())
    client.connect(
        hostname=HOST,
        username=username,
        password=password,
        look_for_keys=False,
        allow_agent=False,
        timeout=10,
        auth_timeout=10,
        banner_timeout=10,
    )

    try:
        hostname = run(client, "hostnamectl --static").upper()
        addresses = run(client, "ip -4 -o address show scope global")
        if hostname != EXPECTED_HOSTNAME or f"{HOST}/" not in addresses:
            raise RuntimeError(f"Refusing unexpected host: {hostname}")

        run(client, f"install -d -m 700 {shlex.quote(remote_dir)}")
        with client.open_sftp() as sftp:
            for source in sources:
                target = f"{remote_dir}/{source.name}"
                sftp.put(str(source.resolve()), target)
                sftp.chmod(target, 0o600)
                local_hash = hashlib.sha256(source.read_bytes()).hexdigest()
                remote_hash = run(client, f"sha256sum {shlex.quote(target)}").split()[0]
                if local_hash.lower() != remote_hash.lower():
                    raise RuntimeError(f"SHA-256 mismatch for {source.name}")
        print(f"transfer_sha256=verified ({len(sources)} files)")

        syntax_command = "\n".join(
            f"bash -n {shlex.quote(remote_dir + '/' + source.name)}"
            for source in sources
            if source.suffix == ".sh"
        )
        run(client, syntax_command)
        print("shell_syntax=valid")

        installer = f"{remote_dir}/install-nginx-bootstrap.sh"
        print(run(client, f"bash {shlex.quote(installer)}", sudo_password=password, timeout=900))

        verify = r"""
set -euo pipefail
nginx -t
printf 'service_enabled=%s\n' "$(systemctl is-enabled nginx.service)"
printf 'service_active=%s\n' "$(systemctl is-active nginx.service)"
systemctl show nginx.service --property=MainPID --property=ExecMainStatus --property=NRestarts --no-pager
ss -ltnp | grep -E ':(80)[[:space:]]'
for host in www.zacharyhounsa.ovh dashboard.zacharyhounsa.ovh administration.zacharyhounsa.ovh; do
  status="$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 10 --header "Host: ${host}" http://192.168.100.211/api/health/ready)"
  printf '%s=%s\n' "${host}" "${status}"
  test "${status}" = 200
done
test -f /etc/nginx/sites-available/kermaria-tls.pending
test ! -e /etc/ssl/kermaria/fullchain.pem
printf 'tls_state=pending_certificate\n'
""".strip()
        print(run(client, verify, sudo_password=password, timeout=120))
        return 0
    finally:
        try:
            run(client, f"rm -rf -- {shlex.quote(remote_dir)}", sudo_password=password)
        except Exception:  # noqa: BLE001
            pass
        client.close()


if __name__ == "__main__":
    raise SystemExit(main())
