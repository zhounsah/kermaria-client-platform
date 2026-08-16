---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-09
---

# Task Group: kermaria-client-platform / checkout, public packs, and implementation handoff

scope: Current-state documentation and release handoff for public packs, managed commercial presentation, billed recurring checkout, and the unified checkout docs published around V0.36.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria checkout, pack, pricing, and documentation-takeover tasks; treat exact commit/tag values as historical anchors.

## Task 1: Release public packs, managed-content, and billed recurring checkout docs, success

### rollout_summary_files

- rollout_summaries/2026-07-07T08-40-15-D9U0-public_packs_managed_content_billed_recurring_checkout.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T10-40-15-019f3bbc-2cc2-7a92-9735-ab8d1f61d2ac.jsonl, updated_at=2026-07-09T16:14:44+00:00, thread_id=019f3bbc-2cc2-7a92-9735-ab8d1f61d2ac, repo-current-state release docs and validation)

### keywords

- Packs.xlsx, V0.32_PUBLIC_PACKS.md, V0.33_CONTENUS_ADMINISTRABLES.md, V0.36_PANIER_UNIFIE_ABONNEMENTS_FACTURES.md, IMPLEMENTATION_MAP_CURRENT.md, commercial_offers, external_reference, recurring checkout, test:cart, test:payments, v0.36

## Task 2: Publish V0.36 unified checkout and staging-validation handoff, success

### rollout_summary_files

- rollout_summaries/2026-07-08T10-47-55-jU3Y-kermaria_v036_checkout_unifie_docs_staging_handoff.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\08\rollout-2026-07-08T12-47-55-019f4157-697f-7f70-8600-1fa58a8f040e.jsonl, updated_at=2026-07-09T16:15:47+00:00, thread_id=019f4157-697f-7f70-8600-1fa58a8f040e, unified checkout and validation-truth handoff)

### keywords

- V0.36, checkout unifie, recurring_checkout, rail=billing, bank transfer, validate-staging.mjs, controlled_write, V0.24_ANOMALIES.md, v0.36.1, README.md, ROADMAP.md, API_CONTRACT.md

## User preferences

- when the user asked `Avant de coder : Commence par explorer le codebase et le fichier Packs.xlsx` -> start with repo reconnaissance plus source-of-truth workbook inspection before editing pack/checkout behavior [Task 1]
- when the user asked to keep compatibility with the existing provisionnement / back-office / checkout architecture -> preserve the current technical mapping and extend it instead of inventing a parallel model [Task 1]
- when the goal is takeover speed, the user values a concrete entry document and implementation map over chat-only explanations [Task 1][Task 2]
- when a release tag already exists, publish a patch tag instead of moving tag history [Task 2]
- when closing this kind of work, include exact proof artifacts such as validation commands, commit hashes, pushes, and tags [Task 1][Task 2]

## Reusable knowledge

- `Packs.xlsx` already contains the business labels, pricing normalization, and pack structure; it is a reliable truth source for public-pack work [Task 1]
- The current doc set for fast takeover is: `docs/IMPLEMENTATION_MAP_CURRENT.md`, `docs/V0.32_PUBLIC_PACKS.md`, `docs/V0.33_CONTENUS_ADMINISTRABLES.md`, and `docs/V0.36_PANIER_UNIFIE_ABONNEMENTS_FACTURES.md`; `README.md` is the top-level index to them [Task 1]
- Public pack presentation is layered over `commercial_offers`, while stable technical identity and billing behavior are keyed off `external_reference` and the existing subscription/provisioning architecture [Task 1]
- The unified checkout is documented as two business tunnels kept separate under the hood: one-shot cart and billed recurring subscriptions via `recurring_checkout` and `rail=billing` [Task 2]
- In the current docs, bank transfer is a recorded payment-rail choice on the commercial document, not immediate capture; the admin payment-marking action is the business trigger once funds are actually received [Task 2]
- `validate-staging.mjs` was realigned so `AD_INTEGRATION_MODE=controlled_write` is accepted in staging and `ALLOW_LOCAL_INTERNAL_API_URL` is no longer required; older failures on those checks can be tooling drift rather than live-environment truth [Task 2]
- Known non-regression noise: `build:api` / `dotnet build` can complete with existing Windows/AD `CA1416` warnings only [Task 1][Task 2]

## Failures and how to do differently

- symptom: broad doc patches fail or apply against the wrong text -> cause: encoding drift and stale context in long files -> fix: read the exact current file text and patch smaller verified blocks [Task 1][Task 2]
- symptom: a release doc sweep accidentally pulls unrelated local artifacts -> cause: the worktree already contains extra files such as temp SQL backups -> fix: inspect git status carefully and explicitly exclude unrelated artifacts like `tmp/backup-avant-reparation-tz-20260709.sql` [Task 1][Task 2]
- symptom: takeover work duplicates docs that already exist -> cause: assuming the repo is missing the handoff instead of checking the staged/current files -> fix: inspect current doc inventory and index state before recreating documents [Task 2]

