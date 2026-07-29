#!/usr/bin/env python3
"""Deploy a validated standalone WEBPORTAL release to KERMARIA-SRV-12."""

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


HOST = "192.168.100.212"
EXPECTED_HOSTNAME = "KERMARIA-SRV-12"
HOST_KEY_SHA256 = "Iq5Nce6cRu0xD3fWDC/IsGhmBz0IujnFbpxjfQkK8eI"


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


def connect(username: str, password: str) -> paramiko.SSHClient:
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
    return client


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


def upload(sftp: paramiko.SFTPClient, source: Path, target: str, mode: int) -> None:
    sftp.put(str(source), target)
    sftp.chmod(target, mode)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--credentials", type=Path, required=True)
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--env-file", type=Path, required=True)
    parser.add_argument("--release-id", required=True)
    args = parser.parse_args()

    local_files = {
        "archive": args.archive.resolve(),
        "env": args.env_file.resolve(),
        "node": (Path(__file__).parent / "srv12" / "install-node-runtime.sh").resolve(),
        "service": (Path(__file__).parent / "srv12" / "kermaria-webportal.service").resolve(),
        "logrotate": (Path(__file__).parent / "srv12" / "kermaria-webportal.logrotate").resolve(),
    }
    for path in local_files.values():
        if not path.is_file():
            raise FileNotFoundError(path)
    if not re.fullmatch(r"[0-9]{8}-[0-9]{6}", args.release_id):
        raise ValueError("release-id must use yyyyMMdd-HHmmss")

    username, password = parse_credentials(args.credentials.resolve())
    transfer_id = uuid.uuid4().hex
    remote_dir = f"/tmp/kermaria-srv12-{transfer_id}"
    remote_files = {
        "archive": f"{remote_dir}/webportal.tar.gz",
        "env": f"{remote_dir}/webportal.env",
        "node": f"{remote_dir}/install-node-runtime.sh",
        "service": f"{remote_dir}/kermaria-webportal.service",
        "logrotate": f"{remote_dir}/kermaria-webportal.logrotate",
    }
    local_hash = hashlib.sha256(local_files["archive"].read_bytes()).hexdigest()

    client = connect(username, password)
    previous_target = ""
    switched = False
    had_active_service = False
    try:
        hostname = run(client, "hostnamectl --static").upper()
        addresses = run(client, "ip -4 -o address show scope global")
        if hostname != EXPECTED_HOSTNAME or f"{HOST}/" not in addresses:
            raise RuntimeError(f"Refusing unexpected host: {hostname}")

        run(client, f"install -d -m 700 {shlex.quote(remote_dir)}")
        with client.open_sftp() as sftp:
            for name, source in local_files.items():
                upload(sftp, source, remote_files[name], 0o600)
        remote_hash = run(client, f"sha256sum {shlex.quote(remote_files['archive'])}").split()[0]
        if remote_hash.lower() != local_hash.lower():
            raise RuntimeError("Archive SHA-256 mismatch after transfer")

        run(client, f"bash -n {shlex.quote(remote_files['node'])}")
        print("transfer_sha256=verified")
        print("node_installer_syntax=valid")

        node_install = f"""
install -d -m 755 /usr/local/lib/kermaria
install -m 750 {shlex.quote(remote_files['node'])} /usr/local/lib/kermaria/install-node-runtime.sh
/usr/local/lib/kermaria/install-node-runtime.sh
""".strip()
        print(run(client, node_install, sudo_password=password, timeout=900))

        had_active_service = run(
            client,
            "systemctl is-active kermaria-webportal.service >/dev/null 2>&1 && echo yes || echo no",
        ) == "yes"
        previous_target = run(
            client,
            "readlink -f /opt/kermaria/webportal 2>/dev/null || true",
        )
        release_dir = f"/opt/kermaria/releases/{args.release_id}"
        backup_dir = f"/var/backups/kermaria-srv12/{args.release_id}"

        deploy = f"""
set -euo pipefail
getent group kermaria-web >/dev/null || groupadd --system kermaria-web
id kermaria-web >/dev/null 2>&1 || useradd --system --gid kermaria-web --home-dir /nonexistent --shell /usr/sbin/nologin kermaria-web
install -d -m 755 /opt/kermaria/releases
test ! -e {shlex.quote(release_dir)}
install -d -m 755 {shlex.quote(release_dir)}
tar -xzf {shlex.quote(remote_files['archive'])} -C {shlex.quote(release_dir)}
test -f {shlex.quote(release_dir + '/apps/webportal/server.js')}
test -d {shlex.quote(release_dir + '/apps/webportal/.next/static')}
test -d {shlex.quote(release_dir + '/apps/webportal/public')}
chown -R root:root {shlex.quote(release_dir)}
find {shlex.quote(release_dir)} -type d -exec chmod 755 {{}} +
find {shlex.quote(release_dir)} -type f -exec chmod 644 {{}} +
install -d -o kermaria-web -g kermaria-web -m 750 {shlex.quote(release_dir + '/apps/webportal/.next/cache')}
install -d -o kermaria-web -g kermaria-web -m 750 /var/log/kermaria
install -d -m 700 {shlex.quote(backup_dir)}
for file in /etc/kermaria/webportal.env /etc/systemd/system/kermaria-webportal.service /etc/logrotate.d/kermaria-webportal; do
  if test -f "$file"; then cp -a "$file" {shlex.quote(backup_dir)}/; fi
done
install -d -m 755 /etc/kermaria
install -o root -g root -m 600 {shlex.quote(remote_files['env'])} /etc/kermaria/webportal.env
install -o root -g root -m 644 {shlex.quote(remote_files['service'])} /etc/systemd/system/kermaria-webportal.service
install -o root -g root -m 644 {shlex.quote(remote_files['logrotate'])} /etc/logrotate.d/kermaria-webportal
ln -s {shlex.quote(release_dir)} /opt/kermaria/.webportal-next
mv -Tf /opt/kermaria/.webportal-next /opt/kermaria/webportal
systemd-analyze verify /etc/systemd/system/kermaria-webportal.service
logrotate --debug /etc/logrotate.d/kermaria-webportal >/dev/null
systemctl daemon-reload
systemctl enable kermaria-webportal.service
systemctl restart kermaria-webportal.service
""".strip()
        run(client, deploy, sudo_password=password, timeout=300)
        switched = True

        verify = """
set -euo pipefail
for attempt in $(seq 1 30); do
  if curl --fail --silent --show-error --max-time 5 http://192.168.100.212:3000/api/health/live >/dev/null && \
     curl --fail --silent --show-error --max-time 10 http://192.168.100.212:3000/api/health/ready >/dev/null; then
    break
  fi
  if test "$attempt" -eq 30; then
    systemctl status kermaria-webportal.service --no-pager >&2 || true
    journalctl -u kermaria-webportal.service -n 80 --no-pager >&2 || true
    exit 1
  fi
  sleep 2
done
systemctl is-enabled kermaria-webportal.service
systemctl is-active kermaria-webportal.service
/opt/kermaria/node/bin/node --version
/opt/kermaria/node/bin/npm --version
ss -ltnp | grep -F '192.168.100.212:3000'
curl --fail --silent --show-error http://192.168.100.212:3000/api/health/live
printf '\n'
curl --fail --silent --show-error http://192.168.100.212:3000/api/health/ready
printf '\n'
""".strip()
        print(run(client, verify, sudo_password=password, timeout=120))
        print(f"release={args.release_id}")
        print(f"archive_sha256={local_hash.upper()}")
        return 0
    except Exception:
        if switched:
            if previous_target:
                rollback_link = (
                    f"ln -s {shlex.quote(previous_target)} /opt/kermaria/.webportal-rollback && "
                    "mv -Tf /opt/kermaria/.webportal-rollback /opt/kermaria/webportal"
                )
            else:
                rollback_link = "rm -f /opt/kermaria/webportal"
            rollback = f"""
systemctl stop kermaria-webportal.service || true
{rollback_link}
systemctl daemon-reload
{('systemctl start kermaria-webportal.service || true' if had_active_service else 'true')}
""".strip()
            try:
                run(client, rollback, sudo_password=password, timeout=120)
                print("rollback=completed", file=sys.stderr)
            except Exception as rollback_error:  # noqa: BLE001
                print(f"rollback=failed: {rollback_error}", file=sys.stderr)
        raise
    finally:
        try:
            run(client, f"rm -rf -- {shlex.quote(remote_dir)}", sudo_password=password)
        except Exception:  # noqa: BLE001
            pass
        client.close()


if __name__ == "__main__":
    raise SystemExit(main())
