---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-03
---

# Task Group: Windows Dev workspace cleanup and E2E-copy removal

scope: Safe space recovery in `C:\Users\zhounsah\Documents\Dev`, including cache/artifact cleanup and deletion of an identified project-copy directory after explicit confirmation.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev; reuse_rule=reuse for similarly scoped Windows workspace cleanup only after exact-path validation and user authorization for material source-like directories.

## Task 1: Remove regenerable Dev caches and artifacts, success

### rollout_summary_files

- rollout_summaries/2026-07-19T13-16-37-UeoA-dev_workspace_cleanup_and_remove_chrome_e2e_copy.md (cwd=C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\19\rollout-2026-07-19T15-16-42-019f7a85-8290-7cb1-8fd3-ed73e4077e8c.jsonl, updated_at=2026-07-19T13:22:22+00:00, thread_id=019f7a85-8290-7cb1-8fd3-ed73e4077e8c, 3.45 GB cache/artifact cleanup with active project dependencies preserved)

### keywords

- Resolve-Path, .next, .npm-cache, out, .tmp, tmp, .codex-tmp, bin, obj, node_modules, dotnet locked DLL, Un élément de canal vide n’est pas autorisé

## Task 2: Identify then delete `kermaria-client-platform.chrome-e2e`, success

### rollout_summary_files

- rollout_summaries/2026-07-19T13-16-37-UeoA-dev_workspace_cleanup_and_remove_chrome_e2e_copy.md (cwd=C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\19\rollout-2026-07-19T15-16-42-019f7a85-8290-7cb1-8fd3-ed73e4077e8c.jsonl, updated_at=2026-07-19T13:22:22+00:00, thread_id=019f7a85-8290-7cb1-8fd3-ed73e4077e8c, removed only after explicit `Supprime-le` confirmation)

### keywords

- kermaria-client-platform.chrome-e2e, Playwright, package-lock.json, no .git, Resolve-Path, Remove-Item -LiteralPath, 61.4 MB

## Task 3: Inventory and archive source-like copies without deletion, partial

### rollout_summary_files

- rollout_summaries/2026-08-03T07-34-06-6dB3-dev_workspace_cleanup_archive_deletion_blocked.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\03\rollout-2026-08-03T09-34-06-019fc68b-5162-7b23-b206-236f6c6b4dad.jsonl, updated_at=2026-08-03T07:47:22+00:00, thread_id=019fc68b-5162-7b23-b206-236f6c6b4dad, archives created; session policy blocked deletion)

### keywords

- _archives_cleanup_20260803, Compress-Archive, Remove-Item, blocked by policy, _artifacts, _deploy_v040, graphify-out, worktrees, 2.40 GB

## User preferences

- when the user asked to `nettoyer le dossier pour libérer de l’espace (supprimer le cache, dossiers temporaires, etc...)` -> inventory and measure first, delete only regenerable artifacts, and preserve source and active-project dependencies. [Task 1]
- when an E2E-looking copy contains recent code differences -> report that risk before deletion and wait for explicit confirmation; `Supprime-le` authorized this exact removal. [Task 2]

## Reusable knowledge

- Before recursive deletion, `Resolve-Path`, verify the resolved path stays beneath the intended workspace, measure it, then remove the literal validated path. A `.chrome-e2e` directory without `.git` but with a near-complete project tree, Playwright lockfile evidence, and test artifacts is likely a working snapshot, not an autonomous repository. [Task 1][Task 2]
- Do not kill `dotnet` processes merely to remove locked `apps\api-internal\bin` DLLs; locked binaries may be from active local APIs. `git status` must target a project subdirectory because `Dev` itself is not a Git repository. [Task 1]

## Failures and how to do differently

- symptom: PowerShell says `Un élément de canal vide n’est pas autorisé` after a loop -> cause: a pipeline was placed directly after the loop/block -> fix: assign `$rows = foreach (...) { ... }` first, then pipe `$rows`. [Task 1]
