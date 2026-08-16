---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-08
---

# Task Group: kermaria-client-platform / Veeam backup status and release handoff

scope: Customer-safe Veeam protection status from internal collector through API/MariaDB to the client portal, plus precise release status across SRV-13, SRV-16, and SRV-12.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse for Veeam/customer backup-status work in this Kermaria topology; revalidate business mapping, collector state, and public release before treating status as current.

## Task 1: Implement Veeam collection and business mapping, success

### rollout_summary_files

- rollout_summaries/2026-08-08T11-59-26-9Qt6-veeam_backup_status_v1_1_14_partial_deployment.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\08\rollout-2026-08-08T13-59-26-019fe13e-07a5-70a1-9aa7-3e8293392104.jsonl, updated_at=2026-08-08T15:06:53+00:00, thread_id=019fe13e-07a5-70a1-9aa7-3e8293392104, API/collector implementation and mapping validated)

### keywords

- Veeam, KoXoDATA, backup_jobs, backup_runs, protection_status, 044_veeam_backup_status, X-Service-Auth, Invoke-VeeamBackupCollection.ps1, test:backups

## Task 2: Commit, tag, and deploy v1.1.14, partial

### rollout_summary_files

- rollout_summaries/2026-08-08T11-59-26-9Qt6-veeam_backup_status_v1_1_14_partial_deployment.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\08\rollout-2026-08-08T13-59-26-019fe13e-07a5-70a1-9aa7-3e8293392104.jsonl, updated_at=2026-08-08T15:06:53+00:00, thread_id=019fe13e-07a5-70a1-9aa7-3e8293392104, SRV-13/SRV-16 deployed; SRV-12 blocked by SSH access)

### keywords

- v1.1.14, 69d6060, SRV-13, SRV-16, SRV-12, kermaria-webportal, kermaria_ai_admin, /backups, 404, Kermaria Veeam Backup Collector

- Related skill: skills/kermaria-srv12-srv13-runtime-deploy/SKILL.md

## User preferences

- when modifying the backup flow, the user required a full analysis of the existing project and reuse of `Customer`/`Service`/tickets models -> inspect the real architecture before introducing abstractions or parallel workflows. [Task 1]
- after an initial SRV-13-based test, the user corrected the business perimeter to KoXoDATA -> never associate an available Veeam job merely for convenience; confirm that it represents the service actually sold to the client. [Task 1]
- the user accepted `Sauvegarde des donnees metier KoXo` without internal details -> keep hostnames, SMB paths, repositories, and technical errors on administration-only surfaces. [Task 1]

## Reusable knowledge

- Kermaria flow: Veeam collector -> private API -> MariaDB -> webportal. `backup_jobs` stores current status and `backup_runs` stores history; the stable Veeam session key makes ingestion idempotent. The portal reads the database, never Veeam directly. [Task 1]
- Reuse the existing support-request workflow for restore requests with customer/service linkage and repository-side `customer_id` checks. Ingestion requires `X-Service-Auth`; admin routes additionally require an authenticated portal session. [Task 1]
- Validated test mapping: `CLI-XS6GCP` -> `Sauvegarde des donnees metier KoXo` -> Veeam job `KoXoDATA`. It reported `protected`, `success`, 77 seconds, seven-day retention, and `0` bytes; do not turn a zero/missing source volume into a positive business metric. [Task 1]
- Release v1.1.14: `69d6060`, tag `v1.1.14`, migration `044_veeam_backup_status` applied and API running on SRV-13. SRV-16 collector lives in `C:\ProgramData\Kermaria\VeeamCollector`; scheduled task `Kermaria Veeam Backup Collector` runs as SYSTEM every 30 minutes and logged `jobs=11 envoyes=11 echecs=0`. `npm run validate` and `npm run test:backups` passed. [Task 1][Task 2]
- The prepared SRV-12 archive is `C:\Users\zhounsah\Documents\Dev\_artifacts\kermaria-webportal-v1.1.14.tar.gz`, built from `69d60600d3fb05f2336b8519c83c54c7b86e4037`; it includes `/backups`, `/backups/[id]`, and `/admin/backups`. [Task 2]

## Failures and how to do differently

- symptom: a technically valid job mapping produces the wrong customer status -> cause: the selected Veeam job does not match the real sold service -> fix: remove stale state and recollect after the business mapping is corrected. [Task 1]
- symptom: an admin endpoint returns unauthorized despite `X-Service-Auth` -> cause: admin routes also require a portal admin session -> fix: test with both required authentication layers. [Task 1]
- symptom: API and collector are deployed but public `/backups` returns `404` and the site shows v1.1.13 -> cause: SRV-12 was not deployed because the local `kermaria_ai_admin` key was refused and no SSH agent key was loaded -> fix: obtain the accepted SRV-12 key, transfer/hash-check the archive, create `apps/webportal/.next/cache` owned by `kermaria-web`, switch `/opt/kermaria/webportal`, restart `kermaria-webportal`, then prove version and `/backups` publicly. Do not call the webportal deployed before those checks. [Task 2]

