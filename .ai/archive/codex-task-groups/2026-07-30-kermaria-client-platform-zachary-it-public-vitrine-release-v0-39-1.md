---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-30
---

# Task Group: kermaria-client-platform / Zachary IT public-vitrine release v0.39.1

scope: Reassuring French public-vitrine messaging, pack-aware signup continuity, and isolated publication of this webportal lot to `main`.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse the messaging and clean-worktree release procedure for similarly scoped Kermaria webportal releases; commit, tag, worktree, and base-branch failures are time-specific and must be rechecked.

## Task 1: Publish Zachary IT public vitrine and signup continuity as v0.39.1, success

### rollout_summary_files

- rollout_summaries/2026-07-30T15-09-06-TG1W-zachary_it_vitrine_v0391_publish.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\30\rollout-2026-07-30T17-09-06-019fb392-6f9d-7493-abf4-70cbe78b5ac8.jsonl, updated_at=2026-07-30T19:30:18+00:00, thread_id=019fb392-6f9d-7493-abf4-70cbe78b5ac8, isolated main publication)

### keywords

- Zachary IT, remote backup, digital emergency folder, PublicPackOverviewGrid, active-offer validation, set-password, test:commercial, test:signup, test:forms, test:managed-content, d756592, v0.39.1, origin/main, Turbopack, node_modules junction

## User preferences

- when refreshing French public messaging, the stated objective required a reassuring, educational, non-alarmist presentation and prohibited unsupported claims -> preserve existing commercial mappings and do not invent certifications, encryption, retention, replication, or infrastructure guarantees [Task 1]
- when the user asked “commit, push et tag dans main” -> perform the actual publication from a coherent validated file set and report exact commit/tag/push references [Task 1]

## Reusable knowledge

- The v0.39.1 lot updated homepage/offers/contact messaging, `PublicPackOverviewGrid`, pack selection/contact handling, active-offer validation, and signup/set-password continuity while preserving prices and technical mappings. Targeted checks passed: `npm run test:commercial`, `npm run test:signup`, `npm run test:forms`, and `npm run test:managed-content`. [Task 1]
- For a dirty branch with unrelated commits, use a clean temporary worktree based on `main`, stage only the explicit release file list, and use a patch tag if the prior version tag already exists. The isolated 19-file lot was published as `d756592` (`feat(webportal): strengthen public vitrine and signup continuity`) and annotated `v0.39.1` on `origin/main`. [Task 1]

## Failures and how to do differently

- symptom: full lint/typecheck/build cannot provide clean proof in an external worktree -> cause: pre-existing main errors, missing Node typings, or Turbopack rejecting an out-of-root `node_modules` junction -> fix: report these limits as base-branch/environment issues, retain targeted contract evidence, and prefer a real external worktree with dependencies inside its filesystem root. [Task 1]
- symptom: the local browser shows an empty catalog -> cause: missing runtime catalog data -> fix: do not claim visual verification of populated pack grids/comparisons from that run. [Task 1]

