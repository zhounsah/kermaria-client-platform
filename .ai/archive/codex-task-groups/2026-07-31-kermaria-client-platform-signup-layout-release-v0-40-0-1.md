---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-31
---

# Task Group: kermaria-client-platform / signup layout release v0.40.0.1

scope: Professional French signup layout, BFF contract validation, and isolated publication of the signup lot to `main` from a dirty Kermaria checkout.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse the signup behavior and clean-worktree release procedure for similar Kermaria signup changes; re-check branch, base, dependencies, and remote refs before a later publication.

## Task 1: Reorganize signup layout and publish v0.40.0.1 on main, success

### rollout_summary_files

- rollout_summaries/2026-07-31T07-38-46-jBin-signup_v04001_publish_main.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\31\rollout-2026-07-31T09-38-46-019fb71c-7f98-7e42-8633-b7323fb10cef.jsonl, updated_at=2026-07-31T14:14:32+00:00, thread_id=019fb71c-7f98-7e42-8633-b7323fb10cef, validated isolated publication on main)

### keywords

- SignupForm.tsx, apps/webportal/app/api/signup/route.ts, userSize, customerType, Raison sociale, npm run test:signup, npm run typecheck:webportal, npm ci, git worktree, cherry-pick, index.lock, v0.40.0.1, 1e31315, origin/main

## User preferences

- when changing French signup copy, the user insisted on accents and a professional presentation -> retain accents and avoid technical or provisional labels such as "Bloc gauche / Bloc droit". [Task 1]
- when specifying the layout, the user wanted structure information and "Votre besoin" on the left, personal information on the right; for an individual hide "Raison sociale", while association/pro shows a user-count range. [Task 1]
- when requesting publication, the user wanted "commit, tag et push" and corrected the target with "Non met le dans le main." -> verify the target branch explicitly and publish on `main` when requested. [Task 1]
- when the worktree is mixed, publish only the requested file lot and leave unrelated changes untouched. [Task 1]

## Reusable knowledge

- The signup lot is `apps/webportal/components/SignupForm.tsx`, `apps/webportal/app/api/signup/route.ts`, `apps/webportal/app/signup/page.tsx`, and `apps/webportal/app/globals.css`. For `customerType === "individual"`, hide both company name and user-size; for professional/association, user-size is required and passed through BFF validation/message handling. [Task 1]
- In a fresh worktree, run `npm ci` before `npm run test:signup` and `npm run typecheck:webportal`. The recorded final checks passed. [Task 1]
- The isolated final release was commit `1e3131507875546cdb3cc2d6ecf7a9d626ee5f0e` (`fix(webportal): polish signup form layout`); `origin/main` and tag `v0.40.0.1` pointed to it at validation time. [Task 1]

## Failures and how to do differently

- symptom: a dirty/divergent local main risks an unrelated release -> cause: mixed changes and competing Git bases -> fix: create a clean worktree from the exact target base and stage only the allowed files; never use `git add .`. [Task 1]
- symptom: a cherry-pick seems resolved but TypeScript fails -> cause: residual `<<<<<<<`/`>>>>>>>` conflict markers in `route.ts` or `SignupForm.tsx` -> fix: search for markers, resolve, then rerun signup tests and typecheck before commit/push. [Task 1]
- symptom: amend is blocked by `index.lock` -> cause: an active Git process or orphaned lock -> fix: verify Git processes and the lock state before retrying. [Task 1]

