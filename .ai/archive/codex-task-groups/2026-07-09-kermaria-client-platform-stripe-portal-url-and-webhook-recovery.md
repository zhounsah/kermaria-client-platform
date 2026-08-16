---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-09
---

# Task Group: kermaria-client-platform / Stripe portal URL and webhook recovery

scope: Stripe and portal-return troubleshooting for the canonical portal URL, staging config drift, webhook replay, and the recovery runbook around V0.35.2.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria payment-return or Stripe webhook recovery tasks when `PUBLIC_PORTAL_URL`, `WEBPORTAL_BASE_URL`, migrations, or replay behavior are in scope; exact event ids are historical evidence.

## Task 1: Fix canonical portal URL handling for Stripe and PayPal returns, success

### rollout_summary_files

- rollout_summaries/2026-07-06T20-52-03-QPku-stripe_portal_url_and_webhook_recovery.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\06\rollout-2026-07-06T22-52-03-019f3933-cc67-7220-94b7-4f2d0445fd62.jsonl, updated_at=2026-07-09T16:06:31+00:00, thread_id=019f3933-cc67-7220-94b7-4f2d0445fd62, canonical portal URL fix)

### keywords

- PUBLIC_PORTAL_URL, WEBPORTAL_BASE_URL, public-routes.ts, getPortalPublicUrl, portail.home.bzh, localhost:3000, success_url, cancel_url, return_url, test:payments-stripe

## Task 2: Diagnose Stripe webhook failures and document recovery/replay, success

### rollout_summary_files

- rollout_summaries/2026-07-06T20-52-03-QPku-stripe_portal_url_and_webhook_recovery.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\06\rollout-2026-07-06T22-52-03-019f3933-cc67-7220-94b7-4f2d0445fd62.jsonl, updated_at=2026-07-09T16:06:31+00:00, thread_id=019f3933-cc67-7220-94b7-4f2d0445fd62, recovery runbook and replay proof)

### keywords

- V0.35.2_STRIPE_PORTAL_RETURN_AND_WEBHOOK_RECOVERY.md, AD_REQUIRED_OU_ROOT, schema_migrations, 022_webhook_resource_id_length, invoice.paid, invoice.payment_succeeded, curl.exe --data-binary, evt_1TqJvyPjVmQIehZau0CZhslp, v0.35.2

## User preferences

- when the user asked `Je mets quoi comme valeur alors ? Mes deux noms de domaine ?` -> give the exact canonical value to set, not a vague set of options [Task 1]
- when the user asked `Tu peux l'intégrer dans les variables ?` -> update the repo/local env directly once the safe value is clear [Task 1]
- when the user reported `Toujours pas j'ai une erreur...` and provided logs -> continue from the runtime evidence until the exact failure and recovery path are identified [Task 2]

## Reusable knowledge

- In this repo, `PUBLIC_PORTAL_URL` is a single canonical portal URL and should point to `https://portail.home.bzh`, not the `www.*` vitrine host and not a multi-domain list [Task 1]
- `WEBPORTAL_BASE_URL` and `PUBLIC_PORTAL_URL` should be updated together to keep return URLs, metadata, and helpers aligned [Task 1]
- `getPortalPublicUrl(request)` is the shared helper for portal return URLs; avoid route-local localhost fallbacks [Task 1]
- The recovery runbook now lives in `docs/V0.35.2_STRIPE_PORTAL_RETURN_AND_WEBHOOK_RECOVERY.md` and records the canonical URL, `AD_REQUIRED_OU_ROOT` requirement when `AD_INTEGRATION_MODE` is `read_only` or `controlled_write`, the `022_webhook_resource_id_length` migration, `schema_migrations` drift repair, and the Stripe replay procedure via `curl.exe --data-binary` [Task 2]
- The webhook path now handles both `invoice.paid` and `invoice.payment_succeeded`, reads the subscription id from either `data.object.subscription` or `data.object.parent.subscription_details.subscription`, and can safely ignore duplicate event replays [Task 2]

## Failures and how to do differently

- symptom: Stripe or PayPal returns go to `localhost:3000` -> cause: stale local env or portal URL drift -> fix: set one canonical `https://portail.home.bzh` value in both `WEBPORTAL_BASE_URL` and `PUBLIC_PORTAL_URL` [Task 1]
- symptom: a Stripe fix is mixed with unrelated local work -> cause: the worktree is already dirty -> fix: isolate only the URL helper and recovery docs before staging or committing [Task 2]
- symptom: the first diff/status pass is noisy after staging changes -> cause: overlapping add/status commands on a dirty tree -> fix: re-run add and status in sequence and verify only intended files are staged [Task 2]

