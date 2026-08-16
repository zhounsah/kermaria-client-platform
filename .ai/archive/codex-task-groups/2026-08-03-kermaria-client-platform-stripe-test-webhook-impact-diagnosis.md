---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-03
---

# Task Group: kermaria-client-platform / Stripe test webhook impact diagnosis

scope: Assess a Stripe webhook alert without conflating the application deployment environment with the Stripe account mode.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse for Stripe webhook investigations; recheck current Dashboard endpoints/configuration and do not treat unsigned probing as delivery proof.

## Task 1: Distinguish Stripe test alert from production application impact, partial

### rollout_summary_files

- rollout_summaries/2026-08-03T19-19-40-Ax32-stripe_test_webhook_vs_production_impact.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\03\rollout-2026-08-03T21-19-41-019fc911-488a-7061-9307-03276f7df300.jsonl, updated_at=2026-08-03T19:24:35+00:00, thread_id=019fc911-488a-7061-9307-03276f7df300, alert classified; no signed event retest)

### keywords

- Stripe test, STRIPE_WEBHOOK_SECRET, SIGNATURE_INVALID, Missing Stripe-Signature header, INTERNAL_API_URL, X-Service-Auth, 401, 502, 503, invoice.payment_succeeded

## User preferences

- when the user corrected "Mais on est en production, plus en test..." and then said a Stripe-test-only alert is "pas grave" -> distinguish the Stripe test/live account from the deployed application environment before assessing impact. [Task 1]

## Reusable knowledge

- The public route validates Stripe signature then forwards to `INTERNAL_API_URL/internal/webhooks/stripe` with `X-Service-Auth` and 30-second timeout. `401 SIGNATURE_INVALID` with `Missing Stripe-Signature header` proves public reachability and signature enforcement, not a successful webhook. [Task 1]
- One `STRIPE_WEBHOOK_SECRET` means test and live destinations with different secrets need coherent configuration or code change. The API handles payment/invoice/subscription events idempotently and can replay previously failed events. [Task 1]

## Failures and how to do differently

- symptom: Stripe email about a test account is reported as a production outage -> cause: test/live mode was not separated from application hosting -> fix: first inspect the Dashboard mode and whether a live endpoint exists; a test-only failure limits expected impact to test events/payments. [Task 1]
- symptom: exact cause is claimed from an unsigned probe -> cause: no signed event or secret comparison was performed -> fix: retain the result as partial and verify real signed delivery/config separately, without exposing secrets. [Task 1]

