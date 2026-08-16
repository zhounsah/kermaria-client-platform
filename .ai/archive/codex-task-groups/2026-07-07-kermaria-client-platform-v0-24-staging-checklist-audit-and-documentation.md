---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-07
---

# Task Group: kermaria-client-platform / V0.24 staging checklist, audit, and documentation

scope: V0.24 staging-recette planning and execution on SRV-01/SRV-02/SRV-07, including the live tracker, audit matrix, cross-doc updates, and follow-up-chip routing.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria V0.24 staging/audit/doc tasks on the same split-host topology; exact checklist counts, email addresses, and backup artifacts are time-specific evidence.

## Task 1: Run the first V0.24 staging pass and update the tracking docs, partial

### rollout_summary_files

- rollout_summaries/2026-07-06T16-21-53-4reu-v024_staging_validation_doc_update_and_intrusive_service_tes.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\06\rollout-2026-07-06T18-21-53-019f383c-7403-7af1-b718-f2b0bd6bfa3e.jsonl, updated_at=2026-07-06T17:07:05+00:00, thread_id=019f383c-7403-7af1-b718-f2b0bd6bfa3e, early validation plus intrusive tests and doc updates)

### keywords

- validate:staging, check:health, INTERNAL_API_URL, PUBLIC_PORTAL_URL, robots.txt, sitemap.xml, X-Robots-Tag, blocked_allowlist, correlation_id, npm audit, dotnet list --vulnerable

## Task 2: Complete the V0.24 recipe, security audit, and documentation set, success

### rollout_summary_files

- rollout_summaries/2026-07-07T15-05-27-NTd3-v0_24_staging_recipe_audit_docs.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform\.claude\worktrees\priceless-driscoll-a6928d, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T17-05-27-019f3d1c-d593-7ba2-8792-64403bb7daad.jsonl, updated_at=2026-07-07T15:05:32+00:00, thread_id=019f3d1c-d593-7ba2-8792-64403bb7daad, long-form recipe, audit, and docs evidence)
- rollout_summaries/2026-07-07T15-05-27-A5js-v024_staging_recette_audit_docs_stripe_smtp_hcaptcha.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform\.claude\worktrees\priceless-driscoll-a6928d, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T17-05-27-019f3d1c-d58f-7571-b571-99ac13e38c0a.jsonl, updated_at=2026-07-07T15:05:32+00:00, thread_id=019f3d1c-d58f-7571-b571-99ac13e38c0a, complementary follow-up chips for Stripe, SMTP, and hCaptcha)

### keywords

- split-host staging, TEST_WEB_RESTORE, check-secrets, SECRET_ROTATION.md, GUIDE_CLIENT_PAIEMENT.md, GUIDE_ADMIN.md, hCaptcha chip, SMTP chip, Stripe chip, restore:mariadb, audit_logs, MOCK-INV, MOCK-NUM

## User preferences

- when the user asked `Tu peux me faire une liste de tous les éléments à tester sur la v0.24 ?` and `Et je coche au fur et à mesure` -> provide an exhaustive checklist broken into small stable items with tracker ids preserved [Task 1]
- the user asked in French and wanted the tracker filled live with status/comment/date/operator -> keep future V0.24 operational checklists and progress updates in French unless asked otherwise [Task 1][Task 2]
- when the user said `À chaque scénario, coche le statut ([x] / [!] / [-]), ajoute un commentaire concis, la date et l'opérateur. Commits par lot cohérent` -> update `docs/V0.24_SUIVI.md` as the live source of truth while the work is happening, not afterward [Task 2]
- the user accepted practical RDP guidance and direct execution steps instead of long hypotheses during live staging work [Task 1][Task 2]
- the user accepted chip-based follow-up handling for long-running fixes once the main recipe/audit/doc stream was complete [Task 2]

## Reusable knowledge

- `docs/V0.24_SUIVI.md` is the authoritative live tracker, while `docs/V0.24_STABILISATION.md` defines the full scope and `docs/SIGNUP_OUVERTURE_RECETTE.md` captures the internal-signup opening procedure [Task 1]
- The critical split-host topology is SRV-01 = WEBPORTAL, SRV-02 = API-INTERNAL, SRV-07 = MariaDB; `INTERNAL_API_URL` on WEBPORTAL must point to `http://192.168.100.202:5000`, and `PUBLIC_PORTAL_URL` must point to `https://portail.home.bzh` for return URLs [Task 1][Task 2]
- `validate:staging` reads `process.env`, so both JSON configs and any staging-only variables not in JSON must be loaded into the current session before running it [Task 1][Task 2]
- The recipe validated: BPCE mock issuance, PayPal one-shot sandbox payment, PayPal monthly subscription activation/cancel/idempotence, vitrine `X-Robots-Tag` split, timezone behavior, and MariaDB restore into `TEST_WEB_RESTORE` with a temporary API pointed at the restored DB; unresolved deeper fixes were split into Stripe, SMTP, and hCaptcha chips instead of blocking the whole V0.24 stream [Task 2]
- The audit pattern that worked was: secrets matrix first, then `check:secrets`, `git grep`, `.env.example` coverage, `npm audit`, `dotnet list --vulnerable`, and log greps for proof-backed controls [Task 2]
- `README.md` is the canonical navigation entry for user-facing docs, `ROADMAP.md` carries coarse milestone references, and the detailed execution state stays in `V0.24_SUIVI.md` [Task 2]

## Failures and how to do differently

- symptom: `validate:staging` fails with confusing health/config issues -> cause: environment pollution (`DEMO_*`) or incomplete JSON/env loading -> fix: purge stray vars, load both configs, then add the missing staging-only variables before rerunning [Task 1][Task 2]
- symptom: WEBPORTAL readiness fails on staging -> cause: split-host config drift such as `INTERNAL_API_URL=http://localhost:5000` -> fix: treat localhost defaults as suspect first on this topology [Task 1][Task 2]
- symptom: restore tests fail early -> cause: target DB such as `TEST_WEB_RESTORE` was not created first -> fix: create the DB, restore, verify migrations/tables, then point the temporary API at it [Task 2]
- symptom: an audit note or follow-up doc breaks `check:secrets` -> cause: literal weak/test password strings were copied into docs -> fix: describe the rotation generically instead of keeping the literal [Task 2]
- symptom: live recipe state flips into risky modes during testing -> cause: signup/email/public toggles were left enabled after validation -> fix: revert staging to the safe baseline once the specific check is complete [Task 2]

