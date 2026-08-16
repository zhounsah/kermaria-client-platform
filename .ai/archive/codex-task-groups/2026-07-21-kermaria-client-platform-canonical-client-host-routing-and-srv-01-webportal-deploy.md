---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-21
---

# Task Group: kermaria-client-platform / canonical client-host routing and SRV-01 webportal deploy

scope: Cross-host client login routing (`www` -> `dashboard` / `administration`) and the associated SRV-01 standalone webportal deployment; use for canonical-host bugs or a quick, explicitly unverified rollout.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for this checkout's `*.zacharyhounsa.ovh` / `*.home.bzh` route mapping and SRV-01 webportal deployment; verify browser behavior and the current dirty-worktree scope before relying on the rollout.

## Task 1: Correct vitrine-to-client host routing after login, success

### rollout_summary_files

- rollout_summaries/2026-07-21T16-16-05-PPyx-kermaria_client_login_host_routing_deploy_srv01.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\21\rollout-2026-07-21T18-16-10-019f8576-88e8-7ff3-bda7-f0226b4e3d21.jsonl, updated_at=2026-07-21T16:31:34+00:00, thread_id=019f8576-88e8-7ff3-bda7-f0226b4e3d21, code fix validated by typecheck/build; browser flow still needs confirmation)

### keywords

- www.zacharyhounsa.ovh, dashboard.zacharyhounsa.ovh, administration.zacharyhounsa.ovh, PortalArea, PORTAL_HOST_MAPPINGS, resolvePortalAreaUrl, resolvePortalRoleUrl, LoginForm, /api/auth/login

## Task 2: Deploy the standalone webportal to SRV-01, partial

### rollout_summary_files

- rollout_summaries/2026-07-21T16-16-05-PPyx-kermaria_client_login_host_routing_deploy_srv01.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\21\rollout-2026-07-21T18-16-10-019f8576-88e8-7ff3-bda7-f0226b4e3d21.jsonl, updated_at=2026-07-21T16:31:34+00:00, thread_id=019f8576-88e8-7ff3-bda7-f0226b4e3d21, service running after a user-requested no-smoke deployment)

### keywords

- KERMARIA-SRV-01.home.bzh, KermariaWebportal, webportal-staging, start-webportal.ps1, .next/static, public, logs, HOME\\svc_api_portal_ad, NSSM, robocopy

## User preferences

- when the user said that `*.zacharyhounsa.ovh` remains canonical but login must land on `dashboard.zacharyhounsa.ovh` -> treat it as cross-zone navigation, not a relative-path routing bug [Task 1]
- when the user said `Déploie le rapidement sans vérification sur SRV-01` -> a no-smoke deployment is acceptable only as an explicit exception; report the missing functional confirmation clearly [Task 2]

## Reusable knowledge

- Validated mapping: `www.zacharyhounsa.ovh` = public, `dashboard.zacharyhounsa.ovh` = client, `administration.zacharyhounsa.ovh` = admin; the corresponding `*.home.bzh` hosts are aliases. Relative `/login` and `/dashboard` retain the current host, so role changes need `resolvePortalAreaUrl()` / `resolvePortalRoleUrl()` or a full navigation. [Task 1]
- The fix centralizes host mapping in `apps/webportal/lib/public-route-config.ts`; canonicalization covers the login page, `LoginForm` post-auth `window.location.assign(...)`, browser submissions to `POST /api/auth/login`, and existing sessions from the public home. `npm run typecheck` in `apps/webportal` and root `npm run build:webportal` passed. [Task 1]
- A standalone deploy must explicitly carry `.next/static`, `public`, `start-webportal.ps1`, and `logs` in addition to the standalone bundle; set `HOME\\svc_api_portal_ad:(OI)(CI)M` on `logs`, swap from staging, and restart NSSM. [Task 2]

## Failures and how to do differently

- symptom: TypeScript rejects `LoginForm` access to the auth result -> cause: `AuthState` is a union -> fix: add the explicit `if (!result.authenticated)` guard before authenticated-only fields. [Task 1]
- symptom: a deploy says `KermariaWebportal` is running but its user outcome is unknown -> cause: no functional browser/HTTP smoke was run, and `robocopy` exit code `1` was not independently inspected -> fix: verify staged files and run `www/login -> dashboard/login -> dashboard/dashboard` plus a post-deploy HTTP smoke when the user allows it. [Task 1][Task 2]
- symptom: a focused routing deploy includes unrelated changes -> cause: the checkout is very dirty and the deploy packages the current webportal tree -> fix: inspect and report deploy scope; use a clean or explicitly staged artifact for isolated releases. [Task 2]

