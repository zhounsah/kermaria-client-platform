---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-03
---

# Task Group: kermaria-client-platform / public backup policy, privacy recovery, and forced release

scope: Public legal/pack content correctness, its persistent managed-content boundary, production privacy recovery, and explicit versioned Git release from a dirty checkout.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse for Kermaria public legal/content and related API release work; recheck database-managed content and current version/tag state.

## Task 1: Align public backup policy and managed content, success

### rollout_summary_files

- rollout_summaries/2026-08-03T12-47-38-wuhQ-kermaria_backup_policy_public_content_harmonization.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\03\rollout-2026-08-03T14-47-38-019fc7aa-5a53-7703-9904-f957b09bf24a.jsonl, updated_at=2026-08-03T13:25:56+00:00, thread_id=019fc7aa-5a53-7703-9904-f957b09bf24a, source/tests validated; production database not changed)

### keywords

- SAVE-PERSO, getPublicPackBackupPolicySummary, managed_content_entries, 031_backup_policy_public_copy_refresh, MANAGED_CONTENT_BACKUP_COPY_UPDATE.sql, 31 jours, /cgv, politique-confidentialite

## Task 2: Restore production privacy page and force 1.0.0.7 release, success

### rollout_summary_files

- rollout_summaries/2026-08-03T12-15-00-EHXJ-kermaria_privacy_fix_and_forced_1_0_0_7_release.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\03\rollout-2026-08-03T14-15-00-019fc78c-7b36-7a20-aa53-c79940df4773.jsonl, updated_at=2026-08-03T15:04:52+00:00, thread_id=019fc78c-7b36-7a20-aa53-c79940df4773, public proof and Git push; full suite not rerun)

### keywords

- legal:politique-confidentialite, ManagedContentService, SeedContent, api-internal-old-20260803-1436-privacyfix, ee79775, 1.0.0.7, v1.0.0.7, chore/remise-a-plat-agentique

## User preferences

- when public policy content changes, the user asked to "inspecter d'abord l'existant", publish no unverified commitment, and finish with files/tests/remaining validations -> inspect first, use a focused diff, and label unknowns. [Task 1]
- the confirmed public backup wording is daily backup, rolling 31-day retention, possible loss since the last successful backup, and points dependent on effective success; do not publish server/topology/RAID/technology details or contradictory 30-day/monthly-restoration text. [Task 1]
- when the user said "1.0.0.7 quand même." and "Commit, tag et push en 1.0.0.7" -> honor the explicit version override while warning it is retrograde; complete both tag variants and branch push. [Task 2]

## Reusable knowledge

- Public packs use `SAVE-PERSO` for backup inclusion; shared presentation text is `getPublicPackBackupPolicySummary` in `packages/shared/src/index.ts`. `/cgv` and `/politique-confidentialite` use `getPublicManagedContent(...)`; persistent `managed_content_entries` overrides SeedContent, so seeds alone do not publish an update. [Task 1]
- Use `031_backup_policy_public_copy_refresh.sql` for guarded `commercial_offers` correction and `docs/MANAGED_CONTENT_BACKUP_COPY_UPDATE.sql` to inspect/update legal content without blind overwrite; production SQL was not applied in this rollout. `test:commercial`, `check:web`, and `git diff --check` passed. [Task 1]
- A missing `legal:politique-confidentialite` in production can be an outdated API deployment: `ManagedContentService` seeds from `AppContext.BaseDirectory\\SeedContent`, copied by `Kermaria.ApiInternal.csproj`. Validate service/readiness plus exact page markers on apex and `www`. [Task 2]
- In a dirty worktree, stage an explicit product-file list and exclude unrelated `.codex/factory/*` and generated `next-env.d.ts`. Before version edits, reread package manifests, `HEAD`, version history, and tags. [Task 2]

## Failures and how to do differently

- symptom: changed seed content does not appear publicly -> cause: an existing database-managed entry wins -> fix: back up/review the target DB and apply only a guarded update. [Task 1]
- symptom: UNC API swap seems complete but active binary is unchanged -> cause: first directory rename did not actually replace live folder -> fix: prove the rename method, then verify directory name, executable timestamp, service status, readiness, and both public URLs. [Task 2]
- symptom: version patch expects 1.0.0.6 -> cause: checkout already advanced to 1.0.0.8 -> fix: reread live manifests immediately before applying the patch; do not claim full tests if only Git push was verified. [Task 2]

