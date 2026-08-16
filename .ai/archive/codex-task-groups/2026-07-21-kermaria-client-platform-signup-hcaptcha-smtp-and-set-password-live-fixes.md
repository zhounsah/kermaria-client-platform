---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-21
---

# Task Group: kermaria-client-platform / signup, hCaptcha, SMTP, and set-password live fixes

scope: Live signup, hCaptcha, SMTP, and set-password work on the Kermaria webportal/API stack, especially when the issue touches runtime config, ARR/proxy IP handling, or Windows deployment/runbook details.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria signup/email/token-validation tasks on staging/live; exact mailbox names, service states, and commit hashes are historical evidence.

## Task 1: Restore SMTP live send, open internal signup, and deploy config/doc updates, success

### rollout_summary_files

- rollout_summaries/2026-07-07T15-05-27-iICg-smtp_ovh_resiliation_fix_signup_recette_opened_deployed.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev\kermaria-client-platform\.claude\worktrees\funny-sammet-ec5801, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T17-05-27-019f3d1c-d57e-74e0-beef-979fec4dc424.jsonl, updated_at=2026-07-07T15:05:32+00:00, thread_id=019f3d1c-d57e-74e0-beef-979fec4dc424, OVH mailbox root cause, internal signup opening, and staging deploy)

### keywords

- SMTP, ssl0.ovh.net, STARTTLS, contact@zacharyhounsa.ovh, SIGNUP_ENABLED, EMAIL_LIVE_ALLOWLIST_ONLY, @home.bzh, build-webportal-config.ps1, build-api-config.ps1, SIGNUP_OUVERTURE_RECETTE.md, PUBLIC_PORTAL_URL

## Task 2: Validate set-password tokens on GET, deploy the fix, and patch the runbook, success

### rollout_summary_files

- rollout_summaries/2026-07-07T15-05-27-gEbL-set_password_ux_fix_staging_deploy_runbook_push_cleanup.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform\.claude\worktrees\friendly-archimedes-93ab6d, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T17-05-27-019f3d1c-d57c-7c40-a510-ae0b3de35f59.jsonl, updated_at=2026-07-07T15:05:32+00:00, thread_id=019f3d1c-d57c-7c40-a510-ae0b3de35f59, GET non destructif, staging deploy, runbook correction, push, and backup cleanup)

### keywords

- set-password, signup/verify, GET validation, TOKEN_INVALID, TOKEN_EXPIRED, verify-signup-contract.mjs, start-webportal.ps1, logs ACL, DEPLOYMENT_WINDOWS.md, V0.26-2b

## Task 3: Support native HTML set-password fallback without raw JSON errors, success

### rollout_summary_files

- rollout_summaries/2026-07-21T12-34-25-GxM2-kermaria_set_password_and_v039_deployment.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\21\rollout-2026-07-21T14-34-30-019f84ab-955d-7681-b996-eb13107528f6.jsonl, updated_at=2026-07-21T16:12:27+00:00, thread_id=019f84ab-955d-7681-b996-eb13107528f6, native form compatibility and contract/runtime checks)

### keywords

- /api/set-password, INVALID_REQUEST, application/x-www-form-urlencoded, requestBffJson, NextResponse.redirect, 303 See Other, getPortalPublicUrlFromHeaders, verify-signup-contract.mjs, GET /internal/signup/set-password/validate

## User preferences

- when debugging live issues, the user provided direct repro evidence and expected concrete diagnosis rather than speculative code changes [Task 1]
- when a browser reproduction shows raw `/api/set-password` `INVALID_REQUEST` JSON -> reproduce the exact request mode first; preserve anti-replay and repair the BFF/presentation boundary. [Task 3]
- when the user corrected the SMTP context with `mon plan MX est résilié... C'est le même mot de passe` -> treat mailbox identity changes as the first fix axis before changing transport code [Task 1]
- when the user said `Faut qu'on règle l'erreur du Captcha là` and kept steering with live tests -> prioritize live verification over theory for signup/captcha incidents [Task 2]
- when the user said `On peut le faire et après on commit` -> for this workflow, deploy/verify can come before commit when the goal is operational recovery [Task 2]
- when the user said `Ce n'est donc PAS une faille — juste une mauvaise UX` and `Ne rien changer au comportement backend d'anti-rejeu` -> preserve backend consumption semantics and solve set-password issues at the GET/presentation layer when possible [Task 2]
- when the user said `Fait tout ce que tu as à faire` -> treat that as permission to finish the remaining operational work end-to-end, including deploy verification, docs, push, runbook updates, and cleanup [Task 2]

## Reusable knowledge

- This repo’s live email path uses `System.Net.Mail`; for OVH `ssl0.ovh.net:587`, STARTTLS can succeed while AUTH fails, so direct `curl.exe --ssl-reqd smtp://...` is a reliable way to separate mailbox/auth issues from application-code issues [Task 1]
- Internal signup opening requires more than `SIGNUP_ENABLED=true`: hCaptcha keys must be present, the allowlist must permit the target recipients, and `PUBLIC_PORTAL_URL` must point at the real portal host for verification/set-password links [Task 1]
- `build-webportal-config.ps1` writes `\\KERMARIA-SRV-01\C$\ProgramData\Kermaria\webportal.config.json` and `build-api-config.ps1` writes `\\KERMARIA-SRV-02\C$\ProgramData\Kermaria\api-internal.config.json` [Task 1]
- `/signup/verify` is the right pattern to mirror for “validate at GET/render, not after submit”; the backend already enforces anti-replay because `password_setup_token_hash` is nulled on consumption, so `FindApprovedByPasswordHashAsync` can power a read-only GET validation path [Task 2]
- The Windows rename-swap runbook for WEBPORTAL must preserve `start-webportal.ps1` and a writable `logs\` directory with the service ACL before restarting `KermariaWebportal` [Task 2]
- `verify-signup-contract.mjs` is the focused validation gate for signup-flow contract changes when repo-wide `tsc` noise is unrelated [Task 2]
- `SetPasswordForm` normally posts JSON through `requestBffJson`, but its native `<form action="/api/set-password" method="post">` fallback posts `application/x-www-form-urlencoded`; the BFF must support both body modes, recover the hidden token, and redirect native submissions with `303 See Other` to `/set-password?status=success` or `?error=...`. [Task 3]
- Use `getPortalPublicUrlFromHeaders(request.headers)` for redirects so the incoming `127.0.0.1` host is preserved rather than becoming `localhost`. `verify-signup-contract.mjs` passed 36 checks; native POST returned 303 and invalid GET rendered HTTP 200 without a password field. [Task 3]
- Related skill: skills/kermaria-windows-staging-deploy/SKILL.md [Task 1][Task 2]

## Failures and how to do differently

- symptom: SMTP errors suggest TLS transport trouble -> cause: the actual break can be mailbox/auth drift instead of `SMTP_USE_STARTTLS` behavior -> fix: probe the mailbox directly with `curl.exe` before changing code [Task 1]
- symptom: local HTTPS smoke tests to `https://portail.home.bzh/signup` fail once -> cause: client-side connection noise can mask healthy remote services -> fix: confirm with server-side service status, config, and logs before declaring the deploy broken [Task 1]
- symptom: staging webportal swap fails after a standalone copy -> cause: missing wrapper files or writable `logs\` directory/ACL in the payload -> fix: stage `start-webportal.ps1`, `logs\`, and the service ACL explicitly before swapping live [Task 2]
- symptom: repo-wide `tsc --noEmit` explodes with ambient Node-type errors -> cause: pre-existing global type noise unrelated to the touched signup files -> fix: use targeted lint/contract checks or a focused build instead [Task 2]
- symptom: a native set-password form displays raw `INVALID_REQUEST` JSON -> cause: the BFF accepts only the React JSON request while browser fallback sends form-urlencoded -> fix: parse form bodies and use an HTTP 303 presentation redirect; keep GET token validation non-destructive and POST as the sole token-consuming operation. A full browser/mail-link walkthrough was still not performed. [Task 3]

