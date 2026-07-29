#!/usr/bin/env python3
"""Audit and configure the SRV-11/SRV-12 Ubuntu network and NTP baseline."""

from __future__ import annotations

import argparse
import base64
import hashlib
import re
import shlex
import sys
import time
from dataclasses import dataclass
from pathlib import Path

import paramiko


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


@dataclass(frozen=True)
class Target:
    name: str
    address: str
    host_key_sha256: str


TARGETS = (
    Target(
        "KERMARIA-SRV-11",
        "192.168.100.211",
        "H90b48dxyBmq6Mcvw9kF4xrVVBZ9ISc9DC5jsaJlrfg",
    ),
    Target(
        "KERMARIA-SRV-12",
        "192.168.100.212",
        "Iq5Nce6cRu0xD3fWDC/IsGhmBz0IujnFbpxjfQkK8eI",
    ),
)


class PinnedHostKeyPolicy(paramiko.MissingHostKeyPolicy):
    def __init__(self, expected_sha256: str) -> None:
        self.expected_sha256 = expected_sha256

    def missing_host_key(self, client, hostname, key) -> None:  # noqa: ANN001
        actual = base64.b64encode(hashlib.sha256(key.asbytes()).digest()).decode()
        actual = actual.rstrip("=")
        if actual != self.expected_sha256:
            raise paramiko.SSHException(
                f"Unexpected SSH host key for {hostname}: SHA256:{actual}"
            )
        client.get_host_keys().add(hostname, key.get_name(), key)


def parse_ssh_credentials(path: Path) -> tuple[str, str]:
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


def connect(target: Target, username: str, password: str) -> paramiko.SSHClient:
    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(PinnedHostKeyPolicy(target.host_key_sha256))
    client.connect(
        hostname=target.address,
        port=22,
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
    password: str | None = None,
    timeout: int = 60,
) -> str:
    if password is not None:
        command = f"sudo -k -S -p '' bash -lc {shlex.quote(command)}"
    stdin, stdout, stderr = client.exec_command(command, timeout=timeout)
    if password is not None:
        stdin.write(password + "\n")
        stdin.flush()
    stdin.close()
    output = stdout.read().decode("utf-8", errors="replace")
    error = stderr.read().decode("utf-8", errors="replace")
    status = stdout.channel.recv_exit_status()
    if status != 0:
        details = error.strip() or output.strip()
        raise RuntimeError(f"Command failed ({status}): {details}")
    return output.rstrip()


def audit(client: paramiko.SSHClient, target: Target, password: str) -> None:
    command = """
set -eu
printf '%s\n' '--- identity ---'
hostnamectl --static
ip -4 -brief address
ip -4 route
printf '%s\n' '--- netplan files ---'
ls -l /etc/netplan/
printf '%s\n' '--- netplan configuration ---'
netplan get
printf '%s\n' '--- chrony service ---'
systemctl status chrony --no-pager || true
printf '%s\n' '--- chrony configured sources ---'
grep -RniE '^[[:space:]]*(server|pool|peer)' /etc/chrony/chrony.conf /etc/chrony/sources.d/ || true
printf '%s\n' '--- chrony runtime ---'
chronyc tracking || true
chronyc sources -v || true
printf '%s\n' '--- system clock ---'
timedatectl
date --iso-8601=seconds
""".strip()
    print(f"\n===== {target.name} ({target.address}) =====")
    print(run(client, command, password=password, timeout=60))


def verify_baseline(
    client: paramiko.SSHClient, target: Target, password: str
) -> None:
    command = """
set -eu
netplan get | grep -Eq 'dhcp-identifier: "?mac"?'
test "$(cat /etc/chrony/sources.d/kermaria.sources)" = \
  "server KERMARIA-SRV-17.home.bzh iburst prefer"
test ! -e /etc/chrony/sources.d/ubuntu-ntp-pools.sources
test -f /etc/chrony/sources.d/ubuntu-ntp-pools.sources.disabled
test ! -e /etc/systemd/timesyncd.conf
test "$(systemctl is-enabled chrony)" = "enabled"
test "$(systemctl is-active chrony)" = "active"
test "$(timedatectl show --property=NTPSynchronized --value)" = "yes"
chronyc sources -n | awk \
  '$1 == "^*" && ($2 == "192.168.100.217" || $2 == "KERMARIA-SRV-17.home.bzh") \
  {found=1} END {exit !found}'
printf '%s\n' 'netplan_dhcp_identifier=mac'
printf '%s\n' 'chrony_source=KERMARIA-SRV-17.home.bzh'
printf '%s\n' 'chrony_service=enabled/active'
printf '%s\n' 'system_clock_synchronized=yes'
printf '%s\n' 'timesyncd_conf=absent'
""".strip()
    print(f"\n===== Verified {target.name} ({target.address}) =====")
    print(run(client, command, password=password, timeout=60))


def validate_identity(client: paramiko.SSHClient, target: Target) -> None:
    hostname = run(client, "hostnamectl --static").strip().upper()
    if hostname != target.name:
        raise RuntimeError(
            f"Refusing {target.address}: hostname is {hostname!r}, expected {target.name!r}"
        )
    addresses = run(client, "ip -4 -o address show scope global")
    if not re.search(rf"\b{re.escape(target.address)}/\d+\b", addresses):
        raise RuntimeError(
            f"Refusing {target.name}: expected address {target.address} is not configured"
        )


def netplan_try(client: paramiko.SSHClient, password: str) -> str:
    transport = client.get_transport()
    if transport is None:
        raise RuntimeError("SSH transport is not available")
    channel = transport.open_session(timeout=10)
    channel.get_pty(term="xterm", width=120, height=40)
    channel.exec_command("sudo -k -S -p '' netplan try --timeout 20")
    channel.sendall(password + "\n")
    output = bytearray()
    deadline = time.monotonic() + 35
    accepted = False
    while time.monotonic() < deadline:
        if channel.recv_ready():
            output.extend(channel.recv(4096))
        if channel.recv_stderr_ready():
            output.extend(channel.recv_stderr(4096))
        text = output.decode("utf-8", errors="replace")
        if not accepted and "Press ENTER" in text:
            channel.sendall("\n")
            accepted = True
        if channel.exit_status_ready():
            break
        time.sleep(0.2)
    if not channel.exit_status_ready():
        channel.close()
        raise RuntimeError("netplan try did not complete before its safety timeout")
    status = channel.recv_exit_status()
    text = output.decode("utf-8", errors="replace").strip()
    if status != 0 or not accepted:
        raise RuntimeError(f"netplan try was not accepted safely: {text}")
    return text


def configure_netplan(
    client: paramiko.SSHClient, target: Target, password: str, backup_dir: str
) -> None:
    patch_script = r'''
from pathlib import Path
import os
import re
import tempfile

path = Path("/etc/netplan/00-installer-config.yaml")
text = path.read_text(encoding="utf-8")
if re.search(
    r"""(?m)^\s*dhcp-identifier:\s*["']?mac["']?\s*(?:#.*)?$""",
    text,
):
    raise SystemExit(0)
lines = text.splitlines(keepends=True)
for index, line in enumerate(lines):
    match = re.match(r"^(\s*)dhcp4:\s*true\s*(?:#.*)?(?:\r?\n)?$", line)
    if match:
        newline = "\r\n" if line.endswith("\r\n") else "\n"
        lines.insert(index + 1, f"{match.group(1)}dhcp-identifier: mac{newline}")
        break
else:
    raise SystemExit("No 'dhcp4: true' entry found in 00-installer-config.yaml")
mode = path.stat().st_mode & 0o777
fd, temporary_name = tempfile.mkstemp(prefix=path.name + ".", dir=path.parent)
try:
    with os.fdopen(fd, "w", encoding="utf-8", newline="") as stream:
        stream.writelines(lines)
    os.chmod(temporary_name, mode)
    os.replace(temporary_name, path)
finally:
    if os.path.exists(temporary_name):
        os.unlink(temporary_name)
'''.strip()
    encoded_patch = base64.b64encode(patch_script.encode()).decode()
    command = f"""
set -eu
cp -a /etc/netplan {shlex.quote(backup_dir)}/netplan
python3 -c "import base64; exec(base64.b64decode('{encoded_patch}'))"
netplan generate
""".strip()
    run(client, command, password=password)
    print(netplan_try(client, password))
    run(client, "netplan apply", password=password)

    # A new SSH connection proves that the accepted network configuration persists.
    transport = client.get_transport()
    if transport is None:
        raise RuntimeError("SSH transport is not available")
    username = transport.get_username()
    client.close()
    replacement = connect(target, username, password)
    try:
        validate_identity(replacement, target)
        print("Network validation: OK")
    finally:
        replacement.close()


def configure_chrony(
    client: paramiko.SSHClient, password: str, backup_dir: str
) -> None:
    preflight = """
set -eu
resolved=$(getent ahostsv4 KERMARIA-SRV-17.home.bzh | awk 'NR == 1 {print $1}')
printf 'Resolved SRV-17: %s\n' "$resolved"
test "$resolved" = "192.168.100.217"
chronyc add server KERMARIA-SRV-17.home.bzh iburst prefer >/dev/null 2>&1 || true
ready=0
for attempt in $(seq 1 15); do
  if chronyc sources -n | awk '($2 == "192.168.100.217" || $2 == "KERMARIA-SRV-17.home.bzh") && $5 != "0" {found=1} END {exit !found}'; then
    ready=1
    break
  fi
  sleep 1
done
chronyc sources -n
test "$ready" -eq 1
""".strip()
    run(client, preflight, password=password, timeout=30)
    command = f"""
set -eu
cp -a /etc/chrony {shlex.quote(backup_dir)}/chrony
if [ -e /etc/systemd/timesyncd.conf ]; then
  cp -a /etc/systemd/timesyncd.conf {shlex.quote(backup_dir)}/timesyncd.conf
fi
printf '%s\n' 'server KERMARIA-SRV-17.home.bzh iburst prefer' > /etc/chrony/sources.d/kermaria.sources
if [ -e /etc/chrony/sources.d/ubuntu-ntp-pools.sources ]; then
  mv /etc/chrony/sources.d/ubuntu-ntp-pools.sources /etc/chrony/sources.d/ubuntu-ntp-pools.sources.disabled
fi
rm -f /etc/systemd/timesyncd.conf
systemctl enable chrony
systemctl restart chrony
chronyc makestep
chronyc waitsync 20 0.1 0 1
""".strip()
    run(client, command, password=password, timeout=90)


def apply_baseline(
    client: paramiko.SSHClient,
    target: Target,
    username: str,
    password: str,
    component: str,
) -> None:
    validate_identity(client, target)
    backup_dir = run(
        client,
        "set -eu; stamp=$(date -u +%Y%m%dT%H%M%SZ); "
        "dir=/var/backups/kermaria-baseline/$stamp; mkdir -p \"$dir\"; "
        "printf '%s' \"$dir\"",
        password=password,
    )
    print(f"\n===== Applying {target.name} ({target.address}) =====")
    print(f"Backup: {backup_dir}")
    if component in {"all", "netplan"}:
        configure_netplan(client, target, password, backup_dir)
        # configure_netplan closes its client after validating a fresh connection.
        client = connect(target, username, password)
    try:
        if component in {"all", "chrony"}:
            configure_chrony(client, password, backup_dir)
        audit(client, target, password)
        verify_baseline(client, target, password)
    finally:
        client.close()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("audit", "apply", "verify"))
    parser.add_argument("--credentials", type=Path, required=True)
    parser.add_argument(
        "--target",
        choices=("SRV-11", "SRV-12", "all"),
        default="all",
    )
    parser.add_argument(
        "--component",
        choices=("all", "netplan", "chrony"),
        default="all",
        help="Limit apply mode to one resumable component",
    )
    args = parser.parse_args()
    username, password = parse_ssh_credentials(args.credentials)
    selected = TARGETS if args.target == "all" else tuple(
        target for target in TARGETS if target.name.endswith(args.target)
    )
    try:
        for target in selected:
            client = connect(target, username, password)
            try:
                if args.mode == "audit":
                    validate_identity(client, target)
                    audit(client, target, password)
                elif args.mode == "verify":
                    validate_identity(client, target)
                    verify_baseline(client, target, password)
                else:
                    apply_baseline(
                        client,
                        target,
                        username,
                        password,
                        args.component,
                    )
            finally:
                client.close()
    finally:
        password = ""
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:  # noqa: BLE001
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
