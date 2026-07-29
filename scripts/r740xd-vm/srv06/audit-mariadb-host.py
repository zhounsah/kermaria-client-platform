#!/usr/bin/env python3
"""Audit the MariaDB account hosts on SRV-06 without exposing secrets."""

from __future__ import annotations

import argparse
import base64
import hashlib
import re
import shlex
from pathlib import Path

import paramiko


ADDRESS = "192.168.100.206"
EXPECTED_HOSTNAME = "KERMARIA-SRV-06"
EXPECTED_HOST_KEY = "/rQ+Te3/RQZzbLTpmhUgc8YedK5SplrWXNKaRoWy/qg"


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
        if key == "identifiant":
            username = value
        elif key == "mdp":
            password = value
    if not username or not password:
        raise ValueError("Missing Linux credentials")
    return username, password


def connect(username: str, password: str) -> paramiko.SSHClient:
    transport = paramiko.Transport((ADDRESS, 22))
    transport.start_client(timeout=10)
    key = transport.get_remote_server_key()
    fingerprint = base64.b64encode(hashlib.sha256(key.asbytes()).digest()).decode()
    if fingerprint.rstrip("=") != EXPECTED_HOST_KEY:
        transport.close()
        raise RuntimeError("Unexpected SRV-06 SSH host key")
    transport.auth_password(username, password)
    client = paramiko.SSHClient()
    client._transport = transport
    return client


def run_sudo(client: paramiko.SSHClient, password: str, command: str) -> str:
    wrapped = f"sudo -k -S -p '' bash -lc {shlex.quote(command)}"
    stdin, stdout, stderr = client.exec_command(wrapped, timeout=30)
    stdin.write(password + "\n")
    stdin.flush()
    stdin.close()
    output = stdout.read().decode("utf-8", errors="replace")
    error = stderr.read().decode("utf-8", errors="replace").strip()
    status = stdout.channel.recv_exit_status()
    if status != 0:
        raise RuntimeError(error or output.strip() or f"Remote command failed: {status}")
    return output.rstrip()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--credentials", type=Path, required=True)
    args = parser.parse_args()
    username, password = parse_credentials(args.credentials)
    client = connect(username, password)
    try:
        identity = run_sudo(
            client,
            password,
            "printf 'hostname='; hostnamectl --static; "
            "printf 'ipv4='; ip -4 -o address show scope global | awk '{print $4}'",
        )
        hostname_line = next(
            line for line in identity.splitlines() if line.startswith("hostname=")
        )
        if hostname_line.split("=", 1)[1].strip().upper() != EXPECTED_HOSTNAME:
            raise RuntimeError(f"Unexpected host identity: {hostname_line}")
        sql = """
SELECT CONCAT('version=', VERSION());
SELECT CONCAT('skip_name_resolve=', @@skip_name_resolve);
SELECT CONCAT(
  'account=', User, '@', Host,
  '|plugin=', plugin,
  '|locked=', IFNULL(account_locked, 'N'),
  '|expired=', IFNULL(password_expired, 'N'))
FROM mysql.user
WHERE User = 'test_web'
ORDER BY Host;
SELECT CONCAT(
  'schema_privilege=', GRANTEE,
  '|schema=', TABLE_SCHEMA,
  '|privilege=', PRIVILEGE_TYPE,
  '|grantable=', IS_GRANTABLE)
FROM information_schema.SCHEMA_PRIVILEGES
WHERE GRANTEE LIKE "'test_web'@%"
ORDER BY GRANTEE, TABLE_SCHEMA, PRIVILEGE_TYPE;
""".strip()
        audit_command = f"""
set -eu
if [ -f /etc/mysql/debian.cnf ] &&
   mariadb --defaults-file=/etc/mysql/debian.cnf --batch --skip-column-names \
     -e 'SELECT 1' >/dev/null 2>&1; then
  client='mariadb --defaults-file=/etc/mysql/debian.cnf'
elif mariadb --batch --skip-column-names -e 'SELECT 1' >/dev/null 2>&1; then
  client='mariadb'
else
  echo 'No privileged local MariaDB identity is available.' >&2
  exit 42
fi
$client --batch --skip-column-names <<'SQL'
{sql}
SQL
""".strip()
        audit = run_sudo(client, password, audit_command)
        print(identity)
        print(audit)
    finally:
        password = ""
        client.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
