---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-09
---

# Task Group: kermaria-client-platform / managed content docs and architecture handoff

scope: Documentation-grounded managed-content work for legal pages and pack sheets, especially when future agents need the real architecture, repo insertion points, or the right entry docs before making changes.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria tasks about managed content, legal pages, pack sheets, public/admin shell routing, or documentation refreshes; treat exact commit/tag values as historical evidence.

## Task 1: Explore managed-content architecture and repo insertion points, success

### rollout_summary_files

- rollout_summaries/2026-07-07T14-26-31-pKOM-v0_33_managed_content_docs.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T16-26-31-019f3cf9-2de8-72e2-94f1-a4ed6769888c.jsonl, updated_at=2026-07-09T16:16:18+00:00, thread_id=019f3cf9-2de8-72e2-94f1-a4ed6769888c, reconnaissance before doc edits)

### keywords

- managed content, legal:cgv, legal:mentions-legales, AppShell, public-route-config, admin/content, managed_content_entries, public pack sheets, PublicPackCard, PublicPackComparisonTable

## Task 2: Align docs with the real V0.33 managed-content implementation, success

### rollout_summary_files

- rollout_summaries/2026-07-07T14-26-31-pKOM-v0_33_managed_content_docs.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T16-26-31-019f3cf9-2de8-72e2-94f1-a4ed6769888c.jsonl, updated_at=2026-07-09T16:16:18+00:00, thread_id=019f3cf9-2de8-72e2-94f1-a4ed6769888c, central V0.33 doc plus cross-doc reconciliation)

### keywords

- V0.33_CONTENUS_ADMINISTRABLES.md, ARCHITECTURE.md, DATA_MODEL.md, GUIDE_ADMIN.md, V0.27_PUBLIC_VITRINE.md, ROADMAP.md, API_CONTRACT.md, DEPLOYMENT_WINDOWS.md, PRODUCTION_DEPLOYMENT.md, v0.33-managed-content-docs

## Task 3: Preserve a reprise checklist for future managed-content regressions, success

### rollout_summary_files

- rollout_summaries/2026-07-07T14-26-31-pKOM-v0_33_managed_content_docs.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T16-26-31-019f3cf9-2de8-72e2-94f1-a4ed6769888c.jsonl, updated_at=2026-07-09T16:16:18+00:00, thread_id=019f3cf9-2de8-72e2-94f1-a4ed6769888c, diagnostic checklist preserved in V0.33 docs)

### keywords

- reprise checklist, diagnostic, shared key registry, URL decoding, X-Data-Source, public_pack_catalog_content, managed_content_entries, PUBLIC_PACKS

## User preferences

- when the user said `Avant de coder : Commence par explorer le codebase...` -> start with reconnaissance before edits on similar repo-change requests [Task 1]
- when the user said `si une solution simple et robuste existe deja dans le projet, reutilise-la` -> prefer the existing managed-content/admin pattern instead of inventing a parallel CMS or file-based workaround [Task 1]
- when the user wanted content editable `sans avoir a editer les fichiers a la main` -> default to a persistent admin-editable model for similar content tasks [Task 1]
- when the user asked for one `pages / contenus administrables` logic plus visible `date de mise a jour` or version -> keep legal pages and pack sheets in one managed-content model and preserve public updated-at/version metadata [Task 1]
- the user wanted the work to stay understandable and maintainable later -> favor a central reference doc plus cross-links over scattered one-off notes [Task 2][Task 3]

## Reusable knowledge

- The repo already had the right insertion points for managed content: `apps/webportal/app/admin/content`, `apps/webportal/app/admin/content/[key]/page.tsx`, `apps/webportal/app/api/admin/content/[key]/route.ts`, matching internal API pieces, and the public pack catalog components [Task 1]
- The shell split is route-aware via `AppShell.tsx` and `public-route-config.ts`; `PublicShell` is selected only for public routes [Task 1]
- The durable mental model is: `PUBLIC_PACKS` = product manifest, `public_pack_catalog_content` = `/offres` marketing content, `managed_content_entries` = editable legal pages and pack sheets [Task 2][Task 3]
- Public pack sheets are not a copy of the vitrine; they combine the manifest, the commercial catalog, and Markdown editorial content [Task 2]
- `docs/V0.33_CONTENUS_ADMINISTRABLES.md` is the best single entry point for understanding the managed-content feature and now includes a reprise/diagnostic checklist [Task 2][Task 3]
- Admin previews should preserve context by opening public pages in a new tab [Task 2]
- The docs rollout was published as commit `4ad773a` and tag `v0.33-managed-content-docs`, which are useful historical anchors when tracing how the documentation was reconciled to the code [Task 2]

## Failures and how to do differently

- symptom: broad documentation edits fail unexpectedly -> cause: doc files drifted or had local modifications that broke large context-based patches -> fix: verify current file text first and patch in smaller targeted hunks [Task 1][Task 2]
- symptom: a docs-only commit appears to stage nothing in PowerShell -> cause: the command used `&&` in a PowerShell context -> fix: use separate commands or PowerShell separators like `;` [Task 2]
- symptom: docs work risks pulling unrelated repo changes into the commit -> cause: the branch already contains other work or temp backup files -> fix: stage only the intended docs files and leave temporary backups untracked [Task 2]

