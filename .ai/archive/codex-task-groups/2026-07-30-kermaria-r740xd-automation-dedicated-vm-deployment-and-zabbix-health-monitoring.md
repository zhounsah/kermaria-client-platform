---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-30
---

# Task Group: kermaria-r740xd-automation / dedicated VM deployment and Zabbix health monitoring

scope: Implemented SRV-11/12/13 dedicated-VM topology, validation evidence, and idempotent Zabbix application monitoring.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev; reuse_rule=reuse for the Kermaria R740xd automation repository and this SRV-11/12/13 topology; revalidate live VM, Zabbix, and deferred-scope status before further changes.

## Task 1: Implement and validate SRV-11/12/13 deployment and monitoring, success

### rollout_summary_files

- rollout_summaries/2026-07-29T10-09-54-QAma-r740xd_srv11_12_13_deployment_zabbix_and_local_secret_update.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\29\rollout-2026-07-29T12-09-54-019fad5a-2650-7e90-982b-8c1b5e1af2ee.jsonl, updated_at=2026-07-30T16:25:41+00:00, thread_id=019fad5a-2650-7e90-982b-8c1b5e1af2ee, validated deployment and monitoring)

### keywords

- R740xd, SRV-11, SRV-12, SRV-13, 192.168.100.211, 192.168.100.212, 192.168.100.213, Zabbix, Set-KermariaHealthMonitoring.ps1, verify-linux-phase2.py, web.page.regexp, expandExpression, 89a6617

## User preferences

- when the user says `PLEASE IMPLEMENT THIS PLAN`, provide the agreed repo-backed runbooks/scripts and validation, not architecture discussion alone. [Task 1]
- for this approved lot, Veeam, KoXo, Windows firewall hardening, DSC/GPO, and secret rotation were deliberately deferred -> preserve those boundaries unless the user reopens them. [Task 1]

## Reusable knowledge

- Dedicated topology: SRV-11 is Ubuntu/nginx/TLS/public entry (`192.168.100.211`), SRV-12 is Ubuntu/Node 24/Next standalone (`192.168.100.212`), and SRV-13 is Windows/.NET API (`192.168.100.213`). [Task 1]
- SRV-12:3000 remains restricted to SRV-11. Monitor it locally through Zabbix Agent 2 `web.page.regexp` rather than opening port 3000 to SRV-10. [Task 1]
- `scripts/r740xd-vm/phase2/zabbix/Set-KermariaHealthMonitoring.ps1` is audit-by-default and applies only with explicit `-Apply`; after fixes, the six monitored objects were conformant. [Task 1]
- Evidence included Linux verifier PASS, 21/21 SRV-13 desired-state checks, public health HTTP 200/`healthy`, `web.test.fail=0`, local SRV-12 `healthy`, no active Zabbix problems, running VMs with automatic start/no checkpoints on SRV-01, and no registered restore-test VM on SRV-02. Monitoring commit: `89a6617`; earlier restore commit: `4627034`. [Task 1]

## Failures and how to do differently

- symptom: PowerShell monitoring audit errors when assigning `$host` -> cause: collision with read-only automatic `$Host` -> fix: use another variable name. [Task 1]
- symptom: idempotence check reports differing Zabbix triggers -> cause: Zabbix canonicalizes expressions -> fix: query/compare `expandExpression=$true`. [Task 1]
- symptom: a large Markdown patch fails -> cause: encoding-sensitive context -> fix: use smaller targeted patches. [Task 1]
- symptom: deployment summary overstates the completed lot -> cause: deferred plan items are mixed with verified state -> fix: explicitly separate live validation from Veeam/KoXo/hardening/DSC/GPO/secret-rotation scope left deferred. [Task 1]

