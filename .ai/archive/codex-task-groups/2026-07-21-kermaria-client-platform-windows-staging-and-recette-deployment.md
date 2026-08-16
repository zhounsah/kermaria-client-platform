---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-21
---

# Task Group: kermaria-client-platform / Windows staging and recette deployment

scope: Remote verification and Windows deployment work for API-INTERNAL and WEBPORTAL on SRV-01/SRV-02, including runtime-layout diagnosis, staging swaps, and health verification.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria server verification and deploy tasks on the SRV-01/SRV-02 topology; treat exact commit hashes, backup folder names, and endpoint states as rollout-specific evidence.

## Task 1: Diagnose SRV-01 runtime layout and verify live health, success

### rollout_summary_files

- rollout_summaries/2026-07-06T16-12-18-vmxx-kermaria_srv01_webportal_staging_health_diagnosis.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\06\rollout-2026-07-06T18-12-18-019f3833-acea-7bc3-bdbd-cbb60234fe9f.jsonl, updated_at=2026-07-06T16:17:55+00:00, thread_id=019f3833-acea-7bc3-bdbd-cbb60234fe9f, runtime layout and on-host health proof)

### keywords

- KERMARIA-SRV-01, Remote PowerShell, C:\ProgramData\Kermaria, C:\apps\webportal, validate:staging, check:health, curl.exe, start-webportal.ps1, webportal.config.json, health/ready

## Task 2: Deploy API-INTERNAL and WEBPORTAL to SRV-02/SRV-01 with rollback-safe swaps, success

### rollout_summary_files

- rollout_summaries/2026-07-13T15-14-03-BKPq-kermaria_srv01_srv02_api_webportal_recette_deploy.md (cwd=C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\13\rollout-2026-07-13T17-14-03-019f5c0a-dc66-7cf0-8ffb-eed61cb84f04.jsonl, updated_at=2026-07-13T15:26:37+00:00, thread_id=019f5c0a-dc66-7cf0-8ffb-eed61cb84f04, latest end-to-end deployment proof)

### keywords

- deployment, SRV-01, SRV-02, KermariaWebportal, KermariaApiInternal, robocopy, Get-SmbOpenFile, Close-SmbOpenFile, start-webportal.ps1, SHA-256, staging swap, health/live, health/ready

## Task 3: Rapid SRV-01 webportal deploy after canonical-host fix, partial

### rollout_summary_files

- rollout_summaries/2026-07-21T16-16-05-PPyx-kermaria_client_login_host_routing_deploy_srv01.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\21\rollout-2026-07-21T18-16-10-019f8576-88e8-7ff3-bda7-f0226b4e3d21.jsonl, updated_at=2026-07-21T16:31:34+00:00, thread_id=019f8576-88e8-7ff3-bda7-f0226b4e3d21, requested no-smoke deploy; service running only)

### keywords

- KERMARIA-SRV-01.home.bzh, webportal-staging, KermariaWebportal, server.js, start-webportal.ps1, .next/static, public, logs, HOME\\svc_api_portal_ad, robocopy exit code 1

## Task 4: Publish `v0.39` cart/dashboard changes and deploy API plus webportal, success

### rollout_summary_files

- rollout_summaries/2026-07-21T12-34-25-GxM2-kermaria_set_password_and_v039_deployment.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\21\rollout-2026-07-21T14-34-30-019f84ab-955d-7681-b996-eb13107528f6.jsonl, updated_at=2026-07-21T16:12:27+00:00, thread_id=019f84ab-955d-7681-b996-eb13107528f6, `v0.39` publish, rollback recovery, and health proof)

### keywords

- c470f56, v0.39, npm run test:cart, DOWNLOAD_STORAGE_ROOT, RuntimeConfigurationException, C:\ProgramData\Kermaria\downloads, KermariaApiInternal, KermariaWebportal, health/ready

## User preferences

- when the user said the deployment was on `KERMARIA-SRV-01.home.bzh` and pointed to `C:\ProgramData\Kermaria`, switch to on-host verification quickly instead of reasoning only from the local checkout [Task 1]
- when the continuation objective includes publication and deployment -> finish commit/tag/push, artifact creation, rollback-safe deployment, and health checks rather than stopping at code validation. [Task 4]
- when the user asked `Est-ce que tu peux déployer l'API et le webportail sur les serveurs ?` -> default to end-to-end operational execution: build, transfer, swap, verify, and keep rollback copies, not just a plan [Task 2]
- when the user asked `Déploie le rapidement sans vérification sur SRV-01` -> respect the explicit no-functional-check exception, but keep the missing smoke test visible in handoff. [Task 3]

## Reusable knowledge

- `C:\apps\webportal` on SRV-01 is a runtime artifact root, not the monorepo root; root npm scripts such as `validate:staging` and `check:health` live in `C:\Users\zhounsah\Documents\Dev\kermaria-client-platform\package.json`, while `C:\apps\webportal\apps\webportal\package.json` only contains app-local scripts [Task 1]
- `C:\ProgramData\Kermaria\webportal.config.json` is the runtime config source for the NSSM wrapper, and `C:\apps\webportal\start-webportal.ps1` injects env vars before launching `apps\webportal\server.js` [Task 1]
- Reliable remote health verification in this environment used `curl.exe` against `http://127.0.0.1:3000/api/health/live`, `http://127.0.0.1:3000/api/health/ready`, and `http://192.168.100.202:5000/health*`; `Invoke-WebRequest` was less reliable in the remote PowerShell session [Task 1]
- The current Windows deploy pattern is: build locally, package API and Next.js standalone WEBPORTAL, copy to `C:\apps\api-internal-staging` / `C:\apps\webportal-staging`, stop service, rename live to backup, rename staging to live, restart, then verify readiness and public URLs [Task 2]
- WEBPORTAL standalone deploys must include `.next/standalone`, `.next/static`, `public`, `start-webportal.ps1`, writable `logs\`, and the service-account ACL; a bare standalone copy is not enough [Task 2]
- `Get-FileHash` on `Kermaria.ApiInternal.exe` and `server.js` is a cheap integrity check before or after cutover [Task 2]
- The July 21 standalone payload required separate copies of `.next/static`, `public`, `start-webportal.ps1`, and `logs`; the old directory was retained as `C:\apps\webportal-old-20260721-182619`, the service was `Running`, and `server.js` was active. [Task 3]
- Related skill: skills/kermaria-windows-staging-deploy/SKILL.md [Task 1][Task 2][Task 4]
- Before deploying the V0.37+ API binary, configure persistent `DOWNLOAD_STORAGE_ROOT` (verified value: `C:\ProgramData\Kermaria\downloads`) and grant `HOME\svc_api_portal_ad:(OI)(CI)M`; then verify both services are Running and query API plus public readiness. [Task 4]

## Failures and how to do differently

- symptom: `npm run validate:staging` or `npm run check:health` fails on SRV-01 with `ENOENT` or `Missing script` -> cause: command ran from the runtime artifact root instead of the monorepo root -> fix: distinguish `C:\apps\webportal` from the source checkout before deciding where scripts live [Task 1]
- symptom: remote HTTP inspection fails with a PowerShell null-reference path -> cause: `Invoke-WebRequest` / `Invoke-RestMethod` instability in this setup -> fix: use `curl.exe` for direct readiness/live endpoint checks [Task 1]
- symptom: `Rename-Item` on `C:\apps\webportal-staging` fails with access denied or the service cannot return to `Running` -> cause: lingering `robocopy.exe` and open SMB handles on the staging folder -> fix: restore live, kill lingering `robocopy`, close `Get-SmbOpenFile` handles on `C:\apps\webportal-staging*`, then retry the swap [Task 2]
- symptom: WEBPORTAL swap appears partially broken despite a usable copy -> cause: `robocopy` exit code `1` is informational in this workflow -> fix: treat only `>= 8` as copy failure and continue with package verification and health checks [Task 2]
- symptom: a quick deploy is treated as a verified release -> cause: `robocopy` returned exit code `1` without detailed output and no HTTP/browser smoke followed -> fix: verify staged files and run a smoke test as soon as the exception window ends. [Task 3]
- symptom: API starts after a swap then immediately fails with `RuntimeConfigurationException: Configuration invalide : DOWNLOAD_STORAGE_ROOT` -> cause: V0.37+ download storage config/ACL is absent -> fix: roll back immediately, configure the persistent root with service-account Modify access, retry, then independently verify timestamps, services, backups, and readiness endpoints. [Task 4]

