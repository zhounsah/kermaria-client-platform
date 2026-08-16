---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-07
---

# Task Group: kermaria-client-platform / PayPal subscriptions, provisioning, and contract maintenance

scope: Subscription implementation, webhook/billing flow, provisioning handoff, and contract-test maintenance around the PayPal/Stripe subscription surfaces.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria subscription, PayPal, webhook, provisioning, or related contract-test work; exact branch names and commit hashes are historical anchors.

## Task 1: Implement V0.22 PayPal subscriptions phases A-F and document the test recipe, success

### rollout_summary_files

- rollout_summaries/2026-07-07T15-05-37-NGdZ-v022_subscriptions_paypal_phases_docs_pr_followup.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T17-05-37-019f3d1c-fd1d-7a30-b84d-a8f8cdeaf213.jsonl, updated_at=2026-07-07T15:05:37+00:00, thread_id=019f3d1c-fd1d-7a30-b84d-a8f8cdeaf213, full implementation plus doc/gotcha capture)

### keywords

- V0.22, PAYPAL_MODE, paypal_plan_id_sandbox, paypal_plan_id_live, PAYPAL_WEBHOOK_VERIFY, subscriptions, webhook, BILLING.SUBSCRIPTION.ACTIVATED, PAYMENT.SALE.COMPLETED, JsonPropertyName, docs/V0.22_SUBSCRIPTIONS.md

## Task 2: Fix provisioning retry UX and pause with a V0.31 remaining-tests handoff, partial/success

### rollout_summary_files

- rollout_summaries/2026-07-06T16-41-46-tCCs-v031_subscription_provisioning_retry_fix_and_md_handoff.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\06\rollout-2026-07-06T18-41-46-019f384e-a6ad-7f30-9ca3-73a3509167b0.jsonl, updated_at=2026-07-07T08:25:41+00:00, thread_id=019f384e-a6ad-7f30-9ca3-73a3509167b0, provisioning retry fix plus resume note)

### keywords

- V0.31_TESTS_RESTANTS.md, SubscriptionService, LdapActiveDirectoryService, AD_GROUP_MEMBER_ALREADY_PRESENT, PROVISIONING_UNCHANGED, AdminReconcileProvisioningButton, useTransition, GG_VPN, GG_RDS, GG_Radio

## Task 3: Realign subscriptions contract assertions after the admin catalog split, success

### rollout_summary_files

- rollout_summaries/2026-07-07T15-05-37-TNfC-subscriptions_contract_paypal_ids_detail_page_and_repo_renam.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev\kermaria-client-platform\.claude\worktrees\compassionate-nobel-b9bb87, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T17-05-37-019f3d1c-fcdf-7190-8dbb-9601e4e75f04.jsonl, updated_at=2026-07-07T15:05:37+00:00, thread_id=019f3d1c-fcdf-7190-8dbb-9601e4e75f04, detail-page assertions plus repository rename fix)
- rollout_summaries/2026-07-07T15-05-37-KFG3-subscriptions_contract_paypal_plan_ids_form_assertions.md (cwd=\?\C:\Users\zhounsah\Documents\Dev\kermaria-client-platform\.claude\worktrees\competent-feistel-47f338\apps\webportal, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T17-05-37-019f3d1c-fce5-7c00-8fba-478c618b5143.jsonl, updated_at=2026-07-07T15:05:37+00:00, thread_id=019f3d1c-fce5-7c00-8fba-478c618b5143, earlier contract-fix attempt on moved PayPal ID assertions)

### keywords

- verify-subscriptions-contract.mjs, paypalPlanIdSandbox, paypalPlanIdLive, app/admin/catalog/[id]/page.tsx, AdminCatalogOfferForm.tsx, GetByExternalIdAsync, test:subscriptions, test:payments-stripe

## User preferences

- for long subscription rollouts, the user accepted phased, commit-sized delivery with validation between phases rather than one opaque batch [Task 1]
- the user wanted durable docs and manual test recipes after each complex integration phase, not just code changes [Task 1]
- when the user said `Note ça dans un .md, on fera la suite après` -> pause cleanly with a markdown handoff instead of continuing deeper into provisioning work [Task 2]
- when fixing subscription contract tests, the user wanted the assertion moved to where the UI really renders today and asked to review nearby assertions too, not just the first failing line [Task 3]
- when touching subscription contracts, preserve adjacent suite behavior such as `npm run test:payments-stripe`, not just `test:subscriptions` [Task 3]

## Reusable knowledge

- The repo’s subscription implementation uses MariaDB migrations in `apps/api-internal/Migrations/MariaDb/`, records applied ids in `schema_migrations`, and prefers `UTC_TIMESTAMP(6)` / `DateTime.UtcNow` for timestamps [Task 1]
- Sandbox and live PayPal plan ids are distinct and the repo now resolves them by `PAYPAL_MODE`; a single `paypalPlanId` field is not sufficient once both modes exist [Task 1]
- `PAYPAL_WEBHOOK_VERIFY=false` is the easiest local webhook test mode; when tests return `401`, verify the running BFF actually saw the env change and was restarted [Task 1]
- The webhook path stores raw payloads in `paypal_webhook_events.raw_payload`, and common failure shields are: `ECONNREFUSED` usually means API-INTERNAL is not listening on the expected port, a reused `event_id` will hit idempotence, and `PAYMENT.SALE.COMPLETED` can fail if the billing-document insert shape is wrong [Task 1]
- The existing AD service layer is already idempotent and surfaces no-op codes like `AD_GROUP_MEMBER_ALREADY_PRESENT` / `AD_GROUP_MEMBER_ALREADY_ABSENT`; future provisioning work should reuse those primitives [Task 2]
- The admin subscription page already has the natural supervision surfaces: provisioning status plus retry, and the current V0.31 resume order is documented in `docs/V0.31_TESTS_RESTANTS.md` [Task 2]
- In the current admin catalog UI, PayPal plan ids render on `app/admin/catalog/[id]/page.tsx` for monthly offers, not on the list page; the repository lookup abstraction is now `GetByExternalIdAsync(rail, externalId)` rather than `GetByPayPalIdAsync` [Task 3]

## Failures and how to do differently

- symptom: local webhook tests return `401` or `ECONNREFUSED` -> cause: either verification mode/env drift or API-INTERNAL not listening where the BFF expects -> fix: confirm the running process env and port first before changing webhook code [Task 1]
- symptom: PayPal/TS contracts drift after a sandbox/live split or API rename -> cause: some consumers still assume one `paypalPlanId` field or PayPal-specific lookup naming -> fix: trace every consumer to active-mode resolution and the current repository abstraction [Task 1][Task 3]
- symptom: worktree cleanup or deletion fails on Windows -> cause: the running session still holds the directory -> fix: remove the git worktree entry and clean the physical directory later from another shell/session [Task 1][Task 2]
- symptom: the provisioning retry button stays stuck after success -> cause: UI state and `router.refresh()` are not coordinated -> fix: use `useTransition`, keep busy state through refresh, and clear submission state in `finally` [Task 2]
- symptom: `test:subscriptions` still fails after moving the first PayPal assertion -> cause: the failure may be a second stale assertion such as the repository method rename -> fix: review the surrounding contract block and rerun adjacent suites from the correct directory [Task 3]

