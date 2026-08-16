---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-07
---

# Task Group: kermaria-client-platform / public offers comparison-table self-service editor

scope: Route public `/offres` comparison-table content requests to the correct Kermaria admin editor without unnecessary source edits or deployment.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse for public-pack presentation/comparison content; distinguish it from billable catalog and pricing administration.

## Task 1: Locate the editable source for the `/offres` comparison table, success

### rollout_summary_files

- rollout_summaries/2026-08-07T15-00-37-x1np-locate_offres_comparison_table_editor.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\07\rollout-2026-08-07T17-00-37-019fdcbd-8a6e-79c0-b0dc-f2f206a25f17.jsonl, updated_at=2026-08-07T15:06:21+00:00, thread_id=019fdcbd-8a6e-79c0-b0dc-f2f206a25f17, user found the desired field)

### keywords

- /offres, /admin/public-pack-catalog, PublicPackComparisonTable, AdminPublicPackCatalogForm.tsx, PATCH /api/admin/public-pack-catalog, /admin/catalog, MariaDB, no-redeploy

## User preferences

- when site content is editable, the user said they wanted to "le modifier moi-même" -> identify the self-service admin route instead of proposing direct code edits. [Task 1]

## Reusable knowledge

- `/offres` renders `PublicPackComparisonTable`; edit labels, order, per-pack values/kinds/text, and add/remove rows in `/admin/public-pack-catalog` through `AdminPublicPackCatalogForm.tsx`. [Task 1]
- These public-presentation edits persist via `PATCH /api/admin/public-pack-catalog` to the API/MariaDB and appear on `/offres` without redeployment. `/admin/catalog` instead owns prices, setup fees, and billable variants. [Task 1]

## Failures and how to do differently

- symptom: a repository-wide search returns too much because many worktrees/releases exist -> fix: start in `kermaria-client-platform/apps/webportal` and use `/admin/public-pack-catalog` as the direct routing handle. [Task 1]

