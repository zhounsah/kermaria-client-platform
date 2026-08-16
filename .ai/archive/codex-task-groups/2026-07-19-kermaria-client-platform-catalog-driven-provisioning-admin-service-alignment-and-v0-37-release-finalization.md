---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-19
---

# Task Group: kermaria-client-platform / catalog-driven provisioning, admin service alignment, and V0.37 release finalization

scope: Post-payment provisioning analysis plus implementation and correction of catalog-driven service projection, admin AD surfaces, migration-history idempotency, and the final commit/tag/push for V0.37.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria provisioning/admin-service/migration-history tasks in this checkout; exact commit hashes, tag names, and temp-artifact paths are release-specific evidence.

## Task 1: Trace the real post-payment provisioning path, success

### rollout_summary_files

- rollout_summaries/2026-07-14T09-07-00-fXiM-catalog_driven_ad_provisioning_admin_fixes_commit_push.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\14\rollout-2026-07-14T11-07-05-019f5fe1-2cca-7d40-9048-7bdcdb87ce90.jsonl, updated_at=2026-07-15T09:22:03+00:00, thread_id=019f5fe1-2cca-7d40-9048-7bdcdb87ce90, code-backed payment-to-provisioning inspection)

### keywords

- InvoiceIssuingService.ConfirmPaymentAsync, BilledSubscriptionPaymentTrigger, SubscriptionProvisioningManager, pending_payment, pending_activation, active, rail=billing, admin subscription detail, reconcile provisioning

## Task 2: Implement catalog-driven service topology and dedicated admin AD surfaces, success

### rollout_summary_files

- rollout_summaries/2026-07-14T09-07-00-fXiM-catalog_driven_ad_provisioning_admin_fixes_commit_push.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\14\rollout-2026-07-14T11-07-05-019f5fe1-2cca-7d40-9048-7bdcdb87ce90.jsonl, updated_at=2026-07-15T09:22:03+00:00, thread_id=019f5fe1-2cca-7d40-9048-7bdcdb87ce90, catalog-driven provisioning topology and admin UX split)

### keywords

- technical_service_references, provisioning_group_sam_account_names, commercial_offers, AdminCatalogOfferForm, CustomerActiveDirectoryAdministrationService, CommercialOfferTopologyService, ClientServiceCatalogService, admin AD page, v0.37

## Task 3: Fix admin customer service projection and `schema_migrations` history for migration 033, success

### rollout_summary_files

- rollout_summaries/2026-07-14T09-07-00-fXiM-catalog_driven_ad_provisioning_admin_fixes_commit_push.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\14\rollout-2026-07-14T11-07-05-019f5fe1-2cca-7d40-9048-7bdcdb87ce90.jsonl, updated_at=2026-07-15T09:22:03+00:00, thread_id=019f5fe1-2cca-7d40-9048-7bdcdb87ce90, correction pass after explicit user acceptance feedback)

### keywords

- AdminService, AdminCustomerDetail, customerId, IClientServiceCatalogService.GetServicesAsync, customer_services, schema_migrations, INSERT IGNORE, MariaDbMigrationRunner, 033_catalog_service_topology.sql

## Task 4: Commit, tag, and push the coherent feature set as `v0.37`, success

### rollout_summary_files

- rollout_summaries/2026-07-14T09-07-00-fXiM-catalog_driven_ad_provisioning_admin_fixes_commit_push.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\14\rollout-2026-07-14T11-07-05-019f5fe1-2cca-7d40-9048-7bdcdb87ce90.jsonl, updated_at=2026-07-15T09:22:03+00:00, thread_id=019f5fe1-2cca-7d40-9048-7bdcdb87ce90, repo finalization and publication proof)

### keywords

- git commit, git tag, git push, 95d5f75, feat: add downloads and catalog-driven AD provisioning, v0.37, origin, .codex-tmp, tmp/backup-avant-reparation-tz-20260709.sql

## Task 5: Correct stale AD provisioning status and publish the focused fix, success

### rollout_summary_files

- rollout_summaries/2026-07-18T10-37-47-e76p-kermaria_ad_provisioning_commit_tag_push.md (cwd=C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\18\rollout-2026-07-18T12-37-52-019f74cd-b938-7de2-85c9-5b51e5314479.jsonl, updated_at=2026-07-19T13:28:24+00:00, thread_id=019f74cd-b938-7de2-85c9-5b51e5314479, targeted four-file AD provisioning correction)

### keywords

- PROVISIONING_SYNCHRONIZED, AD_GROUP_SCOPE_INCOMPATIBLE, SubscriptionProvisioningManager, CustomerActiveDirectoryAdministrationService, AdminCustomerActiveDirectoryWorkbench, 5410, 3101, 7a2c153, ad-provisioning-sync-2026-07-19

## User preferences

- when the user asks `Tu peux me dire où en est le provisionning une fois que le client a payé ?`, answer from the real code path and explain the actual admin surfaces, not a theoretical provisioning design [Task 1]
- when the user asks for `Un bouton pour provisionner l'ensemble et un autre pour chaque utilisateur` and says AD actions should be on `une page séparée`, prefer explicit per-user/bulk controls and split AD operations away from the main customer page [Task 2]
- when the user says `Globalement c'est très bien : juste deux petites corrections`, treat those concrete follow-up points as release-blocking acceptance criteria before calling the feature done [Task 3]
- when the user is interrupted and asks to resume the route/thought process, continue from the current investigation state instead of restarting the whole explanation [Task 3]
- when the user asks `Tu peux commid, tag et push ?`, they want publication, not just local validation -> move from feature work into careful git hygiene and remote push once the worktree is coherent [Task 4]
- when the user asks `Tu peux commit, tag et push ?` from a worktree mixing V0.38, V0.39, and unrelated changes -> isolate the validated paths and use a descriptive tag rather than pretending the full tree is a clean version release. [Task 5]

## Reusable knowledge

- The billed-subscription payment flow converges through `InvoiceIssuingService.ConfirmPaymentAsync(...)`, then `BilledSubscriptionPaymentTrigger.OnDocumentPaidAsync(...)` advances `pending_payment -> pending_activation -> active`, records payment state, and may reconcile provisioning [Task 1]
- `SubscriptionProvisioningManager` is the durable place to inspect provisioning status, retry eligibility, mapped groups, reconciled groups, and target users; the admin subscription detail page already exposes these supervision surfaces [Task 1]
- Catalog-driven service/provisioning topology is now stored on `commercial_offers` via `technical_service_references` and `provisioning_group_sam_account_names`, and the portal-side source of truth is `ClientServiceCatalogService` [Task 2]
- To keep admin and client service lists aligned, `AdminService` should project services through the same calculation path as the portal instead of maintaining a separate `customer_services`-style snapshot [Task 2][Task 3]
- The stable fix for migration-033 history drift is idempotent bookkeeping in both places: `MariaDbMigrationRunner` uses `INSERT IGNORE INTO schema_migrations (...)`, and `033_catalog_service_topology.sql` also writes `INSERT IGNORE INTO schema_migrations (migration_id, applied_at)` for manual-application cases [Task 2][Task 3]
- `AdminCustomerDetail` now includes `customerId`, which lets the admin service build a short-lived projection session and call `IClientServiceCatalogService.GetServicesAsync(...)` without reimplementing service logic [Task 3]
- The coherent release state for this feature set was published as commit `95d5f75` and tag `v0.37`; temp artifacts `.codex-tmp/` and `tmp/backup-avant-reparation-tz-20260709.sql` were intentionally left untracked [Task 4]
- Provisioning summaries must inspect effective AD group memberships, not a stale historical `AD_GROUP_SCOPE_INCOMPATIBLE`; when synchronized, report `PROVISIONING_SYNCHRONIZED` with the effective root and diagnostics. Fresh API `5410` and webportal `3101` showed the corrected French status on both admin surfaces. [Task 5]
- The focused fix was commit `7a2c153` (`fix(ad): align provisioning summary with effective group state`) and annotated tag `ad-provisioning-sync-2026-07-19`; only `SubscriptionProvisioningManager.cs`, `CustomerActiveDirectoryAdministrationService.cs`, the admin subscription page, and `AdminCustomerActiveDirectoryWorkbench.tsx` were included. [Task 5]

## Failures and how to do differently

- symptom: provisioning explanations sound plausible but miss the actual state after payment -> cause: the answer skipped the convergence point in `InvoiceIssuingService` and the billing trigger path -> fix: trace `ConfirmPaymentAsync(...)` -> `BilledSubscriptionPaymentTrigger` -> `SubscriptionProvisioningManager` before summarizing [Task 1]
- symptom: the admin customer page still shows fictitious services after catalog work -> cause: admin logic stayed tied to the old `customer_services` perspective -> fix: reuse the portal calculation path through `ClientServiceCatalogService` instead of patching counts locally [Task 2][Task 3]
- symptom: migration 033 exists in business tables but not in `schema_migrations` -> cause: only the data changes were applied or the runner/manual path mismatch was ignored -> fix: make both the runner write and the SQL migration history insert idempotent with `INSERT IGNORE` [Task 2][Task 3]
- symptom: a normal `dotnet build` fails during validation because the output DLL is locked -> cause: a running `dotnet.exe` keeps `Kermaria.ApiInternal.dll` open -> fix: validate with `-p:OutDir=...` to avoid the locked default output path [Task 2]
- symptom: a final commit risks bundling unrelated local artifacts -> cause: the worktree is already dirty -> fix: stage only the coherent feature set and explicitly exclude temp folders/backups instead of using a blanket `git add .` [Task 4]
- symptom: port `5000` is held by an inaccessible old API process -> cause: stale local runtime -> fix: validate the isolated stack on API `5410` plus webportal `3101` rather than killing an unknown process. [Task 5]

