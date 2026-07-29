#!/usr/bin/env python3
"""Reboot SRV-12 then SRV-11 and prove their application services recover."""

from __future__ import annotations

import argparse
import importlib.util
import logging
import socket
import sys
import time
from pathlib import Path

import paramiko


logging.getLogger("paramiko.transport").setLevel(logging.CRITICAL)


def load_baseline_module():  # noqa: ANN201
    module_path = Path(__file__).with_name("configure-linux-baseline.py")
    spec = importlib.util.spec_from_file_location("linux_baseline", module_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load {module_path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def request_reboot(client: paramiko.SSHClient, password: str) -> None:
    stdin, stdout, stderr = client.exec_command(
        "sudo -k -S -p '' systemctl reboot", timeout=15
    )
    stdin.write(password + "\n")
    stdin.flush()
    stdin.close()
    try:
        stdout.channel.recv_exit_status()
    except (EOFError, OSError, socket.error):
        # A clean reboot commonly closes SSH before an exit status is returned.
        pass


def wait_for_new_boot(
    baseline,  # noqa: ANN001
    target,  # noqa: ANN001
    username: str,
    password: str,
    old_boot_id: str,
    timeout_seconds: int,
) -> paramiko.SSHClient:
    deadline = time.monotonic() + timeout_seconds
    last_error = "waiting for SSH"
    while time.monotonic() < deadline:
        client = None
        new_boot_id = ""
        try:
            client = baseline.connect(target, username, password)
            new_boot_id = baseline.run(
                client, "cat /proc/sys/kernel/random/boot_id", timeout=15
            ).strip()
            if new_boot_id and new_boot_id != old_boot_id:
                return client
            last_error = "SSH is reachable but boot ID has not changed"
        except (OSError, EOFError, paramiko.SSHException) as error:
            last_error = str(error)
        finally:
            if client is not None and new_boot_id == old_boot_id:
                client.close()
            elif client is not None and not new_boot_id:
                client.close()
        time.sleep(5)
    raise TimeoutError(f"{target.name} did not complete reboot: {last_error}")


def verify_srv12(baseline, client, password: str) -> str:  # noqa: ANN001
    command = r"""
set -euo pipefail
test "$(systemctl is-enabled chrony)" = enabled
test "$(systemctl is-active chrony)" = active
test "$(systemctl is-enabled kermaria-webportal.service)" = enabled
test "$(systemctl is-active kermaria-webportal.service)" = active
test "$(systemctl show kermaria-webportal.service --property=NRestarts --value)" = 0
case "$(readlink -f /opt/kermaria/webportal)" in
  /opt/kermaria/releases/*) ;;
  *) exit 1 ;;
esac
/opt/kermaria/node/bin/node --version | grep -Eq '^v24\.'
curl --fail --silent --show-error --max-time 10 http://192.168.100.213:5000/health/ready >/dev/null
curl --fail --silent --show-error --max-time 10 http://192.168.100.212:3000/api/health/live >/dev/null
curl --fail --silent --show-error --max-time 10 http://192.168.100.212:3000/api/health/ready >/dev/null
printf 'chrony=enabled/active\n'
printf 'webportal=enabled/active\n'
printf 'release=%s\n' "$(readlink -f /opt/kermaria/webportal)"
printf 'node=%s\n' "$(/opt/kermaria/node/bin/node --version)"
printf 'api_internal_ready=200\n'
printf 'webportal_live=200\n'
printf 'webportal_ready=200\n'
""".strip()
    return baseline.run(client, command, password=password, timeout=90)


def verify_srv11(baseline, client, password: str) -> str:  # noqa: ANN001
    command = r"""
set -euo pipefail
test "$(systemctl is-enabled chrony)" = enabled
test "$(systemctl is-active chrony)" = active
test "$(systemctl is-enabled nginx.service)" = enabled
test "$(systemctl is-active nginx.service)" = active
test "$(systemctl show nginx.service --property=NRestarts --value)" = 0
nginx -t
test "$(ss -ltn | grep -Ec ':(80)[[:space:]]')" -ge 1
test "$(ss -ltn | grep -Ec ':(443)[[:space:]]' || true)" = 0
for host in www.zacharyhounsa.ovh dashboard.zacharyhounsa.ovh administration.zacharyhounsa.ovh; do
  test "$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 10 --header "Host: ${host}" http://192.168.100.211/api/health/ready)" = 200
done
printf 'chrony=enabled/active\n'
printf 'nginx=enabled/active\n'
printf 'http_listener=active\n'
printf 'tls_listener=pending_certificate\n'
printf 'proxy_ready_all_hosts=200\n'
""".strip()
    return baseline.run(client, command, password=password, timeout=90)


def reboot_and_verify(
    baseline,  # noqa: ANN001
    target,  # noqa: ANN001
    username: str,
    password: str,
    timeout_seconds: int,
) -> None:
    client = baseline.connect(target, username, password)
    try:
        baseline.validate_identity(client, target)
        old_boot_id = baseline.run(
            client, "cat /proc/sys/kernel/random/boot_id", timeout=15
        ).strip()
        print(f"{target.name}: reboot requested")
        request_reboot(client, password)
    finally:
        client.close()

    client = wait_for_new_boot(
        baseline, target, username, password, old_boot_id, timeout_seconds
    )
    try:
        new_boot_id = baseline.run(
            client, "cat /proc/sys/kernel/random/boot_id", timeout=15
        ).strip()
        if target.address.endswith(".212"):
            evidence = verify_srv12(baseline, client, password)
        else:
            evidence = verify_srv11(baseline, client, password)
        print(f"{target.name}: boot_id_changed=yes")
        print(evidence)
        print(f"{target.name}: boot_id={new_boot_id}")
    finally:
        client.close()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--credentials", type=Path, required=True)
    parser.add_argument("--timeout-seconds", type=int, default=300)
    args = parser.parse_args()
    if args.timeout_seconds < 60:
        raise ValueError("timeout-seconds must be at least 60")

    baseline = load_baseline_module()
    username, password = baseline.parse_ssh_credentials(args.credentials.resolve())
    targets_by_address = {target.address: target for target in baseline.TARGETS}

    # Recover the application tier first, then prove the reverse proxy recovers.
    for address in ("192.168.100.212", "192.168.100.211"):
        reboot_and_verify(
            baseline,
            targets_by_address[address],
            username,
            password,
            args.timeout_seconds,
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
