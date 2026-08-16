---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-01
---

# Task Group: kermaria-client-platform / production footer version and signup width release

scope: Versioned Kermaria webportal publication, visible footer version, and signup layout fixes that require direct production rendering verification.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse the clean-worktree and SRV-12 release procedure for similar webportal work; recheck current release symlink and live HTML/CSS before calling a visual fix complete.

## Task 1: Deploy v1.0.0.4 with footer version and signup width fix, success

### rollout_summary_files

- rollout_summaries/2026-07-31T13-59-38-wZv6-kermaria_v1004_footer_version_signup_width_deployment.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\31\rollout-2026-07-31T15-59-39-019fb879-34d1-75d2-9ac2-0b541a02944c.jsonl, updated_at=2026-08-01T10:14:46+00:00, thread_id=019fb879-34d1-75d2-9ac2-0b541a02944c, `Version v1.0.0.4` verified on live pages)

### keywords

- v1.0.0.4, PublicShell.tsx, Version v${appPackage.version}, signup-page, max-width: 720px, max-width: 1320px, /opt/kermaria/releases, /opt/kermaria/webportal, kermaria-webportal, DEPLOY_OK

## User preferences

- when the user said “Il manque la version en dans le footer et aussi je ne vois pas la correction…” -> verify the exact requested marker and observable layout on production, not merely a build or healthcheck. [Task 1]

## Reusable knowledge

- Use a clean worktree based on `origin/main` when the main checkout is dirty. On SRV-12, releases live at `/opt/kermaria/releases/<timestamp>` and `/opt/kermaria/webportal` is the active symlink; deploy Next standalone output with `.next/static` and `public`, then restart `kermaria-webportal`. [Task 1]
- `PublicShell.tsx` can import root `package.json` and render `Version v${appPackage.version}`. The signup-wide module was being overridden by global `.signup-page { max-width: 720px; }`; an explicit `.page { max-width: 1320px; width: min(1320px, calc(100vw - 40px)); }` fixed it. [Task 1]

## Failures and how to do differently

- symptom: CSS source changed but the signup remains narrow -> cause: a global selector overrides the module -> fix: inspect live CSS and identify the overriding selector. [Task 1]
- symptom: upload/deploy script fails with `set: pipefail: invalid option name` -> cause: CRLF on Linux script -> fix: use LF. Test TCP/SSH before upload; in PowerShell run commands separately rather than chaining with `&&`. [Task 1]

