---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-13
---

# Task Group: kermaria-client-platform / version inventory and memory concordance

scope: Cross-source version lookup for the Kermaria repo, especially when the user wants repo docs, Git tags, Codex memory, and local Claude memory compared explicitly.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria version-history, doc-truth, or "does version X exist?" work; treat exact tag inventories and Claude-memory paths as time-specific evidence that may need rechecking.

## Task 1: Build cross-source version concordance, success

### rollout_summary_files

- rollout_summaries/2026-07-13T19-40-14-g9qB-kermaria_version_concordance_and_v034_gap_check.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\13\rollout-2026-07-13T21-40-19-019f5cfe-8ea9-7441-8bd0-01cb70f6c515.jsonl, updated_at=2026-07-13T21:09:30+00:00, thread_id=019f5cfe-8ea9-7441-8bd0-01cb70f6c515, repo docs vs tags vs Codex memory vs Claude memory concordance)

### keywords

- version concordance, Git tags, docs/ROADMAP.md, docs/IMPLEMENTATION_MAP_CURRENT.md, MEMORY.md, .claude, V0.33, V0.35, V0.36, tableau

## Task 2: Verify V0.34 is absent in verified sources, success

### rollout_summary_files

- rollout_summaries/2026-07-13T19-40-14-g9qB-kermaria_version_concordance_and_v034_gap_check.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\13\rollout-2026-07-13T21-40-19-019f5cfe-8ea9-7441-8bd0-01cb70f6c515.jsonl, updated_at=2026-07-13T21:09:30+00:00, thread_id=019f5cfe-8ea9-7441-8bd0-01cb70f6c515, clean negative lookup across repo, tags, Codex memory, and Claude memory)

### keywords

- V0.34, v0.34, git tag --list, rg -n, ROADMAP.md, version gap, negative lookup, .claude

## User preferences

- when the user asked for `| version | fonctionnalités rajoutés | tag git | ... |`, they wanted a compact, copy-pastable matrix by default for version work [Task 1]
- when the user said `Regarde dans la mémoire Claude. Peut-être que tu auras un indice.` -> future version-inventory work should corroborate across repo docs, Git tags, Codex memory, and local Claude memory instead of trusting one source [Task 1][Task 2]
- when the user narrowed to `V0.34`, they wanted the gap called out explicitly, not hidden inside a broader concordance [Task 2]

## Reusable knowledge

- `docs/ROADMAP.md` and `docs/IMPLEMENTATION_MAP_CURRENT.md` are the main repo-side version anchors for quick concordance work [Task 1]
- A reliable first pass for Kermaria version inventory is: enumerate `git for-each-ref refs/tags` / `git tag --list`, list `docs/V0*`, then compare against Codex memory and the relevant Claude memory files [Task 1]
- local Claude memory currently reflects the V0.33 managed-content family and the V0.36 checkout/docs family more strongly than the intermediate versions, so it is useful as corroboration but not as a complete version index [Task 1]
- `V0.34` was not found in repo docs, Git tags, Codex memory, or the searched Claude memory; the documented repo sequence jumps from `V0.33` to `V0.35` [Task 2]

## Failures and how to do differently

- symptom: searching `C:\Users\zhounsah\.claude` produces too much noise to answer a version question quickly -> cause: the tree contains many unrelated sessions and project artifacts -> fix: restrict the search to version markers plus known memory files first [Task 1]
- symptom: versions appear inconsistent across docs and tags -> cause: this repo mixes doc-only versions with patch-style or feature-suffixed tags -> fix: present an explicit source-vs-source concordance instead of forcing a false one-to-one mapping [Task 1]
- symptom: the user asks whether a missing version exists anywhere useful -> cause: a concordance table can hide the negative result -> fix: state the negative lookup explicitly as "not found in repo docs, tags, Codex memory, or Claude memory" [Task 2]

