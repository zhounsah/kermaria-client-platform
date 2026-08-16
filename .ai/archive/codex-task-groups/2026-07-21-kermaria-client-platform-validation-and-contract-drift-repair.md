---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-21
---

# Task Group: kermaria-client-platform / validation and contract drift repair

scope: Conservative validation, contract-triage, and QA-matrix work: bring `npm run validate` back to green, classify local versus environment-only evidence, and avoid claiming unproduced test artifacts as delivered.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria validate/contract-maintenance tasks on the same repo; treat `result*.txt` logs and exact dirty-file states as rollout-specific evidence.

## Task 1: Bring `npm run validate` back to green by fixing docs and stale contract scripts, success

### rollout_summary_files

- rollout_summaries/2026-07-06T13-32-17-mrru-kermaria_validate_green_obsolete_contracts_secret_redaction.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\06\rollout-2026-07-06T15-32-17-019f37a1-2b05-79d0-9598-01368d3ca764.jsonl, updated_at=2026-07-06T15:47:05+00:00, thread_id=019f37a1-2b05-79d0-9598-01368d3ca764, full green pipeline after doc redaction and contract realignment)

### keywords

- npm run validate, result1.txt, result2.txt, check:secrets, verify-admin-activity-contract.mjs, verify-commercial-foundation-contract.mjs, recentActivities, PayButton, next-env.d.ts, NU1900

## Task 2: Audit repo truth versus memory, fast-forward the worktree onto `main`, and isolate a pre-existing `check:secrets` blocker, partial

### rollout_summary_files

- rollout_summaries/2026-07-07T15-05-27-lImt-kermaria_roadmap_tests_memory_realign_main_validate_blocked.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform\.claude\worktrees\objective-elbakyan-c059db, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T17-05-27-019f3d1c-d57b-7572-b022-0f07bdaacde9.jsonl, updated_at=2026-07-07T15:05:32+00:00, thread_id=019f3d1c-d57b-7572-b022-0f07bdaacde9, repo-truth arbitration, contract-test realignment, fast-forward onto main, and validate blocker)

### keywords

- v0.19..HEAD, fast-forward, origin/main, verify-bpce-invoicing-contract.mjs, verify-subscriptions-contract.mjs, roadmap-current, check:secrets, [REDACTED_SECRET], ff-only, worktree, main

## Task 3: Produce a reviewed Kermaria Excel validation matrix, success

### rollout_summary_files

- rollout_summaries/2026-07-19T13-25-22-aUKG-kermaria_classeur_validation_tests_revu.md (cwd=C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\19\rollout-2026-07-19T15-25-27-019f7a8d-8252-70c0-b7d4-4b11f33d231e.jsonl, updated_at=2026-07-21T21:44:27+00:00, thread_id=019f7a8d-8252-70c0-b7d4-4b11f33d231e, reviewed `.xlsx` delivered; no staging/live execution in this rollout)

### keywords

- Kermaria_plan_validation_projet_2026-07-21_revu.xlsx, Controle qualite, docs/V0.24_SUIVI.md, docs/IMPLEMENTATION_MAP_CURRENT.md, verify-*.mjs, automated local, staging, manual, intrusive/live

## User preferences

- when the user asked to `vérifier` and later to `corriger ensemble`, they wanted evidence-based verification before edits and conservative changes rather than a broad refactor [Task 1]
- the user accepted `test obsolète` versus `régression produit` classification -> classify validate failures before changing code [Task 1]
- when the user asked `Maintenant, il faut tester quoi ?` -> end with a practical prioritized next test step grounded in the actual pipeline state [Task 1]
- when the user accepted leaving `apps/webportal/next-env.d.ts` alone -> exclude pre-existing generated noise from the intentional patch set [Task 1]
- when the user asked to `tranche explicitement` in case of divergence and `N'invente aucune validation staging` -> prefer repo docs, git history, and dated proof over memory-only claims, and keep “verified” separate from “still to verify” [Task 2]
- when the user asked for a clean report onto `main` and said `Si validate n'est pas faisable de façon fiable, explique-le en une phrase` -> stop clearly at the first trustworthy blocker rather than forcing a noisy continuation [Task 2]

- when the user asks for an `.xlsx` project test series -> deliver a real, directly usable workbook in French, not a recommendation list; separate automated local, staging, manual, and intrusive scenarios. [Task 3]

## Reusable knowledge

- The validate pipeline includes `check:secrets`, both TS typechecks, `build:web`, `build:api`, `test:api`, and the Node-based contract suites such as `test:activity` and `test:commercial` [Task 1]
- `result1.txt` / `result2.txt` are better truth sources than memory when reconstructing which stage actually failed or passed [Task 1]
- `app/admin/page.tsx` now links to `/admin/activity`, while `app/admin/activity/page.tsx` owns `recentActivities`; contract tests should assert the real owner file [Task 1]
- `app/commercial-documents/[id]/page.tsx` legitimately exposes payment UI via `PayButton`, `isPayPalConfigured()`, and `isStripeConfigured()`; modern commercial contracts should assert the current invariant instead of forbidding payment strings globally [Task 1]
- `NU1900` from NuGet vulnerability metadata retrieval was warning-only in the successful run [Task 1]
- `docs/V0.24_SUIVI.md` is stronger evidence than external memory when deciding whether a staging recipe item was actually executed; in this audit it overruled stale external claims and showed that V0.24 was still mostly unvalidated apart from `V0.26-2b` [Task 2]
- A clean report from a worktree to the main checkout can be a simple `git merge --ff-only` when the worktree commit is a direct descendant; in this rollout the fast-forward preserved the exact hash on `main` and kept the patch set to three files [Task 2]
- `npm run validate` begins with `check:secrets`, so a documented password literal can block the whole run before any build/test stage executes [Task 2]
- When `test:bpce` or `test:subscriptions` fail after product evolution, check whether the contract is anchored to a legacy UI location or API name before treating it as a runtime regression; `GetByExternalIdAsync` and `app/admin/catalog/[id]/page.tsx` were the current truth in this pass [Task 2]

- For a project test workbook, start with `AGENTS.md`, `README.md`, `docs/IMPLEMENTATION_MAP_CURRENT.md`, `docs/V0.24_SUIVI.md`, `docs/V0.24_STABILISATION.md`, root/webportal `package.json`, V0.36-V0.39 docs, and contract scripts. Preserve `browser -> WEBPORTAL / BFF -> API-INTERNAL -> MariaDB`: WEBPORTAL never calls MariaDB, AD, SMTP, BPCE, or other internal integrations directly. [Task 3]
- The delivered workbook is `C:\Users\zhounsah\Documents\Dev\outputs\019f7a8d-8252-70c0-b7d4-4b11f33d231e\Kermaria_plan_validation_projet_2026-07-21_revu.xlsx`; it includes synthesis, coverage, test catalogue, and `Controle qualite` for KO, open P0, and incomplete-proof rows. `docs/V0.24_SUIVI.md` remains the live tracker and staging/live claims still need dated proof. [Task 3]

## Failures and how to do differently

- symptom: the first validate diagnosis is incomplete -> cause: the log stopped at the first blocker and the later failures were still hidden -> fix: inspect the actual tail or rerun after each fix to reveal the next stage [Task 1]
- symptom: `check:secrets` fails on a tracking doc -> cause: a literal sensitive-looking value is present in the documentation -> fix: keep the operational meaning but replace the literal with generic wording [Task 1]
- symptom: a contract suite rejects real current UI behavior -> cause: the test is anchored to an older product version -> fix: inspect the current page/component first and move the assertion to the actual owner of the invariant [Task 1]
- symptom: memory, docs, and git history disagree about what was already validated -> cause: external memory over-asserted staging completion -> fix: arbitrate explicitly from repo docs and git evidence, and do not mark staging work done without dated proof [Task 2]
- symptom: shell output makes `validate` look like a general failure when only the first guardrail ran -> cause: `check:secrets` stops the pipeline immediately -> fix: explain that early blocker clearly instead of implying later suites were also run [Task 2]

- symptom: a workbook is mistaken for proof that staging/live validation happened -> cause: the matrix inventory is confused with execution evidence -> fix: label existing proof separately from scenarios to replay, and do not run destructive/live tests merely to fill it. [Task 3]

