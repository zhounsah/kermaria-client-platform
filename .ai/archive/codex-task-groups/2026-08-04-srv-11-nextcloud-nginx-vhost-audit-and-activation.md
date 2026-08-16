---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-04
---

# Task Group: SRV-11 Nextcloud Nginx vhost audit and activation

scope: Read-only Nginx/TLS audit followed by a bounded, transactionally validated activation of `nextcloud.home.bzh` through the existing SRV-11 HAProxy edge.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev; reuse_rule=reuse only for SRV-11 Nginx/HAProxy topology changes; re-audit loaded config, certificates, listeners, and backend before applying another vhost change.

## Task 1: Audit the SRV-11 Nginx/TLS architecture, success

### rollout_summary_files

- rollout_summaries/2026-08-04T13-56-54-KDlB-srv11_nextcloud_nginx_vhost_audit_and_apply.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\04\rollout-2026-08-04T15-56-54-019fcd10-20e5-7882-9747-a5190896cff7.jsonl, updated_at=2026-08-04T14:16:02+00:00, thread_id=019fcd10-20e5-7882-9747-a5190896cff7, read-only audit before user-approved application)

### keywords

- KERMARIA-SRV-11, Nginx, HAProxy, nextcloud.home.bzh, send-proxy-v2, 127.0.0.1:8443, home-bzh-clean-fullchain.pem, cloudflared, Plink

## Task 2: Activate and verify the Nextcloud vhost, success

### rollout_summary_files

- rollout_summaries/2026-08-04T13-56-54-KDlB-srv11_nextcloud_nginx_vhost_audit_and_apply.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\04\rollout-2026-08-04T15-56-54-019fcd10-20e5-7882-9747-a5190896cff7.jsonl, updated_at=2026-08-04T14:16:02+00:00, thread_id=019fcd10-20e5-7882-9747-a5190896cff7, nginx test/reload and end-to-end HTTP/HTTPS proof)

### keywords

- /etc/nginx/sites-available/nextcloud.home.bzh, /etc/nginx/sites-enabled/nextcloud.home.bzh, proxy_protocol, proxy_pass, 192.168.100.209:8080, nginx -t, HSTS, CardDAV, CalDAV

## User preferences

- when infrastructure changes are requested, the user said: "Avant toute modification, réalise uniquement un audit en lecture seule" -> inventory and report the verifiable state before modifying anything. [Task 1]
- when auditing TLS, the user required that private-key contents never be displayed, while paths, permissions, and public certificates are checked -> keep that separation. [Task 1]
- when approving the vhost change, the user required a backup, `nginx -t` before reload, automatic restore on test failure, then HTTP/HTTPS/log checks; they also said "Ne modifie pas HAProxy" and "Ne modifie pas cloudflared" -> use this bounded transactional workflow and preserve excluded systems. [Task 2]

## Reusable knowledge

- Active edge topology was Internet -> HAProxy `:443` -> Nginx `127.0.0.1:8443` with `send-proxy-v2`; HTTP `:80` reaches Nginx directly. Do not configure Nginx to listen directly on `443` in this topology. [Task 1]
- The wildcard Let's Encrypt certificate `/etc/ssl/kermaria/home-bzh-clean-fullchain.pem` covered `nextcloud.home.bzh`; its key had mode `600`, while the public certificate had mode `644`. `cloudflared` had no Nextcloud route, so it was intentionally unchanged. [Task 1]
- The working vhost uses `listen 127.0.0.1:8443 ssl proxy_protocol`, `set_real_ip_from 127.0.0.1`, `real_ip_header proxy_protocol`, `http2 on`, and `proxy_pass http://192.168.100.209:8080`; it includes ACME handling, HTTP-to-HTTPS redirect, 10G uploads, disabled buffering, 3600s proxy timeouts, `X-Forwarded-*`, HSTS without `includeSubDomains`/`preload`, and CardDAV/CalDAV redirects. [Task 2]
- Validation proved `nginx -t`, reload success, local HTTP `301`, HAProxy HTTPS `HTTP/2 302` to `/login` with HSTS, expected backend redirect, and no recent Nginx/Nextcloud errors. [Task 2]

## Failures and how to do differently

- symptom: complex inline remote commands fail or quote incorrectly -> cause: nested shell/Plink quoting -> fix: send a temporary script with Plink `-m`; write remote shell scripts as ASCII to avoid `bash: ... ï»¿set: command not found` from an UTF-8 BOM. [Task 1]
- symptom: an initial HTTPS response seems to be the wrong app or lacks HSTS -> cause: a single request is insufficient routing proof -> fix: cross-check `nginx -T`, SNI via `openssl s_client`, direct backend, and repeated curl through HAProxy before concluding the vhost is wrong. [Task 2]

