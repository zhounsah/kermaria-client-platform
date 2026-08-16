---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-08
---

# Task Group: kermaria-client-platform / staged SRV-13 then SRV-12 V1.1 deployment

scope: Verified API-first deployment across SRV-13 Windows/.NET and SRV-12 Ubuntu/Next, with staging swaps, migration/AD checks, and public readiness proof.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse for the dedicated SRV-12/SRV-13 release topology; recheck current archives, hashes, active release, and service state.

## Task 1: Deploy V1.1.0 API then webportal through staging, success

### rollout_summary_files

- rollout_summaries/2026-08-03T16-07-52-YSYY-kermaria_v1_1_0_srv13_srv12_deployment.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\03\rollout-2026-08-03T18-07-52-019fc861-ad2c-7d50-8e67-dbb3143dbd1c.jsonl, updated_at=2026-08-03T16:27:15+00:00, thread_id=019fc861-ad2c-7d50-8e67-dbb3143dbd1c, API-first staged deployment; no demo functional test)
- rollout_summaries/2026-08-03T14-40-24-K3oy-deploy_lot3_v1_1_srv13.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\03\rollout-2026-08-03T16-40-24-019fc811-9938-7450-9881-457a5534818e.jsonl, updated_at=2026-08-03T14:47:21+00:00, thread_id=019fc811-9938-7450-9881-457a5534818e, SRV-13 Lot 3 configuration safeguards)

### keywords

- V1.1.0, SHA256, C:\kmw\out, KermariaApiInternal, 031_backup_policy_public_copy_refresh, AD_ALLOWED_GROUPS, api-internal-staging, kermaria-webportal, MODULE_NOT_FOUND, /opt/kermaria/releases

- Related skill: skills/kermaria-srv12-srv13-runtime-deploy/SKILL.md

## Task 2: Deploy tagged v1.1.13 to SRV-13 then SRV-12, success

### rollout_summary_files

- rollout_summaries/2026-08-07T22-18-54-WYak-zachary_it_diagnostic_configurator_v1_1_13_deploy.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\08\rollout-2026-08-08T00-18-54-019fde4e-cddf-7bd2-af95-2ecd9cba76b0.jsonl, updated_at=2026-08-08T12:26:32+00:00, thread_id=019fde4e-cddf-7bd2-af95-2ecd9cba76b0, API and webportal deployed from the exact tag)

### keywords

- v1.1.13, b7980c4, SRV-13, SRV-12, Plink, KermariaApiInternal, kermaria-webportal, 042_signup_catalog_configuration, 043_fiscal_regime_franchise_base, /diagnostic

## User preferences

- when deploying V1.1.0, the user required SHA verification, staging-only swaps, SRV-13 before SRV-12, migration 031, and no demo-account functional test -> follow that exact technical/deployment boundary. [Task 1]
- when deploying v1.1.13, the user said that uncommitted files came from another Codex session -> preserve the concurrent worktree; build only from the validated tag and do not globally clean, stash, reset, or include unrelated Backup/Veeam files. [Task 2]

## Reusable knowledge

- SRV-13 is Windows/.NET: copy and hash-check the archive on-host, extract to `C:\apps\api-internal-staging`, preserve a timestamped live-directory backup, run the approved migration, start `KermariaApiInternal`, check `/health/ready`, and grep logs for `AD_CONFIGURATION_INVALID` and `AD_TARGET_OUTSIDE_ALLOWED_ROOTS`. [Task 1]
- SRV-12 is Ubuntu/Next: normalize Windows `\\` ZIP paths before extraction, require `apps/webportal/server.js` before switching `/opt/kermaria/webportal`, restart `kermaria-webportal`, wait/retry for `192.168.100.212:3000`, then verify private and public readiness. [Task 1]
- For AD groups, reread the raw `AD_ALLOWED_GROUPS` value and test `-split ','` before restart; the expected config has the clients required root, KoXoAdm/Groupes_TEST allowed roots, `TEST_SITE_WEB`, and the three `GG_DEMO_*` groups. [Task 1]
- For a tagged release, build from the exact tag. SRV-13 uses `KermariaApiInternal` and `http://192.168.100.213:5000/health/ready`; SRV-12 uses `kermaria-webportal`, active symlink `/opt/kermaria/webportal`, and `192.168.100.212:3000`. Transfer a webportal `.tar.gz` containing standalone output, `.next/static`, and `public`; create `.next/cache` owned by `kermaria-web` before restarting systemd. [Task 2]
- v1.1.13 evidence: migrations 042/043 applied via `--apply-migrations` with `ExitCode 0`; SRV-13 readiness HTTP 200 with MariaDB healthy and AD `controlled_write`; SRV-12/public readiness HTTP 200 with configuration/API healthy; `/diagnostic` returned HTTP 200 and showed `Version v1.1.13`. [Task 2]

## Failures and how to do differently

- symptom: remote PowerShell returns no summary or fails on complex inline code -> cause: quoting/policy -> fix: use a temporary local script and independently inspect the host before assuming a deployment changed anything. [Task 1]
- symptom: Linux extraction yields literal Windows paths and `MODULE_NOT_FOUND` -> cause: backslashes and directory entries were not normalized -> fix: normalize separators, handle directories explicitly, then prove `server.js` exists. Generate remote Bash with LF, not CRLF (`set: pipefail: invalid option name`). [Task 1]
- symptom: public site becomes healthy but footer remains `Version v1.0.0.6` -> cause: visible marker was not incremented -> fix: treat that as a separate version-marker follow-up, not evidence that the V1.1 deployment failed. [Task 1]
- symptom: the first curl after restart fails -> cause: the service is not ready yet -> fix: wait and retry before deciding deployment failure, then check the requested public route. [Task 2]
- symptom: remote PowerShell/Plink commands become fragile -> cause: long nested quoting or uncertain SSH tooling -> fix: use an explicitly verified Plink host fingerprint, short commands, and LF remote scripts; the v1.1.13 session did not have usable OpenSSH/Pageant access. [Task 2]

