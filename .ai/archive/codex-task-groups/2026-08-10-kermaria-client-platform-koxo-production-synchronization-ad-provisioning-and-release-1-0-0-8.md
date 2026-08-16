---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-10
---

# Task Group: kermaria-client-platform / KoXo production synchronization, AD provisioning, and release 1.0.0.8

scope: Verify the password-set KoXo trigger, update SRV-13 child-domain AD mappings, apply bounded project ACLs, and report the precise release state.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse only for the dedicated SRV-12/SRV-13/SRV-21 Kermaria flow; revalidate live configuration, receiver/log evidence, and Git refs before treating production/release state as current.

## Task 1: Verify the SRV-13-to-SRV-21 KoXo webhook chain, code/config/network verified

### rollout_summary_files

- rollout_summaries/2026-08-02T20-23-15-QVWs-koxo_production_chain_ad_group_mapping_and_release_1008.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\02\rollout-2026-08-02T22-23-15-019fc425-22f2-7dc3-82e6-5f3307cc40ec.jsonl, updated_at=2026-08-10T09:17:20+00:00, thread_id=019fc425-22f2-7dc3-82e6-5f3307cc40ec, full end-to-end replay remains unverified)

### keywords

- KoXo, SRV-21, webhook, 8042, koxo_pending, set-password, SERVICE_AUTH_REQUIRED, SERVICE_AUTH_TOKEN, KOXO_SYNC_TIMEOUT_SECONDS, /Synchro=CLIENTS.xml, Fin de l'opération

- Related skill: skills/kermaria-koxo-webhook-sync/SKILL.md

## Task 2: Remap production provisioning groups to child-domain DNs, success

### rollout_summary_files

- rollout_summaries/2026-08-02T20-23-15-QVWs-koxo_production_chain_ad_group_mapping_and_release_1008.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\02\rollout-2026-08-02T22-23-15-019fc425-22f2-7dc3-82e6-5f3307cc40ec.jsonl, updated_at=2026-08-10T09:17:20+00:00, thread_id=019fc425-22f2-7dc3-82e6-5f3307cc40ec, SRV-13 config/restart/readiness verified)

### keywords

- AD_PROVISIONING_GROUP_DNS, GG_RDS, GG_VPN, GG_NextCloud, AD_ALLOWED_ROOTS, AD_CLIENTS_OU_DN, AD_REQUIRED_OU_ROOT, SubscriptionProvisioningRuntimeConfiguration, controlled_write

## Task 3: Grant only HOME\\zhounsah project-directory access on SRV-13, success

### rollout_summary_files

- rollout_summaries/2026-08-02T20-23-15-QVWs-koxo_production_chain_ad_group_mapping_and_release_1008.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\02\rollout-2026-08-02T22-23-15-019fc425-22f2-7dc3-82e6-5f3307cc40ec.jsonl, updated_at=2026-08-10T09:17:20+00:00, thread_id=019fc425-22f2-7dc3-82e6-5f3307cc40ec, inherited Full Control verified on bounded project roots)

### keywords

- HOME\\zhounsah, icacls, (OI)(CI)(F), C:\apps\api-internal, C:\ProgramData\Kermaria, D:\Kermaria, KoXoExchange

## Task 4: Commit, tag, and reconcile 1.0.0.8 release state, success

### rollout_summary_files

- rollout_summaries/2026-08-02T20-23-15-QVWs-koxo_production_chain_ad_group_mapping_and_release_1008.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\02\rollout-2026-08-02T22-23-15-019fc425-22f2-7dc3-82e6-5f3307cc40ec.jsonl, updated_at=2026-08-10T09:17:20+00:00, thread_id=019fc425-22f2-7dc3-82e6-5f3307cc40ec, corrected remote tag and branch containment checked)

### keywords

- 1.0.0.8, 6efa2ec, release: 1.0.0.8, chore/remise-a-plat-agentique, git show-ref --tags, git ls-remote --tags origin, git branch --contains, next-env.d.ts

## User preferences

- when the user asked whether `SRV-13 -> SRV-21` was really usable -> state code, live configuration, network, and real POST evidence separately. [Task 1]
- when the user supplied exact child-domain DNs -> preserve logical aliases `GG_RDS`, `GG_VPN`, and `GG_NextCloud` and change only their runtime DN overrides; identify an appropriate admin settings surface as a future option without claiming one was implemented. [Task 2]
- when the user clarified "Uniquement mon compte utilisateur" -> grant only the named account, preserve SYSTEM/Administrators rights, and inspect the actual project root rather than recursively changing a whole drive. [Task 3]
- when the user asked to publish only "tes modifications de cette conversation" -> stage explicit paths and report remaining worktree edits, tag target, remote refs, and `main` containment separately. [Task 4]

## Reusable knowledge

- Expected path is `webportal SRV-12 -> api-internal SRV-13 -> webhook privé SRV-21 -> PowerShell -> KoXoAdm.exe`; `SignupService` triggers `password_set` only for `koxo_pending` after AD synchronization. [Task 1]
- `KoxoSyncWebhookTriggerService` uses a dedicated bearer-authenticated HTTP client and logs failure without failing password setup. SRV-13 was healthy and TCP to SRV-21:8042 returned `TCP_OK`; no fresh signed POST was replayed. [Task 1]
- The verified architecture is `webportal/SRV-12 -> api-internal/SRV-13 -> private webhook/SRV-21 -> PowerShell -> KoXoAdm`; active AD-linked portal users are exported through private `/api/internal/koxo/users`. `SignupService` invokes `KoxoSyncWebhookTriggerService` with `password_set` after AD provisioning leaves `koxo_pending`. The named client is `koxo-sync-webhook`; failures are logged without failing password setup. [Task 1]
- SRV-13 readiness was HTTP 200 with MariaDB healthy and AD `controlled_write`; live webhook configuration was present (redacted), allowed insecure HTTP, used a 10-second timeout, and SRV-13-to-SRV-21:8042 returned `TCP_OK`. [Task 1]
- Map aliases with `AD_PROVISIONING_GROUP_DNS__GG_RDS`, `__GG_VPN`, and `__GG_NextCloud`. When groups live under `CN=Users` outside the client OU, retain `AD_CLIENTS_OU_DN` exactly once in `AD_ALLOWED_ROOTS`, include both the client OU and group root, and use the common domain root for `AD_REQUIRED_OU_ROOT`; these AD variables are API-only, not SRV-12/webportal settings. [Task 2]
- `HOME\\zhounsah:(OI)(CI)(F)` was verified at `C:\apps\api-internal`, `C:\ProgramData\Kermaria`, `D:\Kermaria`, and `D:\Kermaria\KoXoExchange`; relevant D: project areas are Downloads, KoXoExchange, and Logs. [Task 3]
- Release `1.0.0.8` ultimately pointed to `6efa2ecae11918abad4e390d626626e4b1f6168f` (`release: 1.0.0.8`) on origin, but the commit was on `chore/remise-a-plat-agentique`, not `main`/`origin/main` at the later check. The release staged only `package.json`, `package-lock.json`, and the two KoXo receiver/invocation scripts; `apps/webportal/next-env.d.ts` is generated route-type plumbing, not business logic. [Task 4]

## Failures and how to do differently

- symptom: code/config/TCP is treated as end-to-end proof -> cause: no fresh signed POST was made -> fix: require `202 queued`, receiver/script logs, and fresh KoXo/CSV output before declaring the flow usable. [Task 1]
- symptom: the scheduled-task installer appears periodic -> cause: its `Interval` parameter registers a `-Once` trigger at five minutes -> fix: treat it as one-shot until implementation changes. [Task 1]
- symptom: a same-session check fails after `KermariaApiInternal` restart -> cause: the restart drops the remoting session -> fix: check again from the controller or a fresh remote session. [Task 2]
- symptom: remote `icacls`/ACL work fails or backup is claimed without evidence -> cause: nested quoting and unverified `icacls /save` -> fix: use `& icacls.exe` with native PowerShell argument arrays, then create and separately verify the ACL export. [Task 3]
- symptom: release tag or claimed branch is wrong -> cause: tag propagation/race or unverified containment -> fix: run `git rev-parse <tag>`, `git show <tag>`, `git ls-remote --tags origin <tag>`, `git branch --contains <commit>`, and `git branch -r --contains <commit>` sequentially. [Task 4]

