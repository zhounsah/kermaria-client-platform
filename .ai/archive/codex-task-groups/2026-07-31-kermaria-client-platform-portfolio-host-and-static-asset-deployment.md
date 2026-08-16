---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-31
---

# Task Group: kermaria-client-platform / portfolio host and static-asset deployment

scope: Diagnose and repair `portfolio.zacharyhounsa.ovh` across the SRV-11 Nginx edge and SRV-12 Next standalone release, including static PDF routing.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for this production portfolio topology; recheck live vhost, certificate, release symlink, and asset availability before assuming the public state persists.

## Task 1: Restore portfolio host, conducteur.pdf, and public-route handling, partial

### rollout_summary_files

- rollout_summaries/2026-07-30T19-33-09-f64U-v040_koxo_analysis_and_portfolio_repair.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\30\rollout-2026-07-30T21-33-09-019fb484-2f44-7fe3-a3d1-2c9befc678d0.jsonl, updated_at=2026-07-31T07:23:55+00:00, thread_id=019fb484-2f44-7fe3-a3d1-2c9befc678d0, portfolio home and conducteur.pdf verified; script.pdf unresolved)

### keywords

- portfolio.zacharyhounsa.ovh, SRV-11, SRV-12, Nginx, /portfolio/, public-route-config.ts, conducteur.pdf, script.pdf, /opt/kermaria/releases, kermaria-webportal, WEBPORTAL_SRV12_DEPLOYMENT.md

## Reusable knowledge

- TLS/Nginx terminates on SRV-11 (`192.168.100.211`); the Next standalone webportal runs on SRV-12 (`192.168.100.212:3000`). The portfolio host needs its own Nginx vhost mapping `/` to `/portfolio/index.html` and other paths to `/portfolio/`. [Task 1]
- Portfolio source assets live in `apps/webportal/public/portfolio/`. Deployment requires both the file in the rebuilt release and `"/portfolio"` in `apps/webportal/lib/public-route-config.ts`; copying a PDF into the active release alone is insufficient. [Task 1]
- The verified release was `/opt/kermaria/releases/20260731-072334-portfolio-fix`, activated through `/opt/kermaria/webportal` with systemd service `kermaria-webportal`. `conducteur.pdf`, portfolio home, and `skills.html` returned 200; `conducteur.pdf` had `Content-Type: application/pdf`. [Task 1]
- `projects/radio-saint-vincent.html` links to `../conducteur.pdf` and `../script.pdf`; `docs/WEBPORTAL_SRV12_DEPLOYMENT.md` is the deployment/rollback runbook. [Task 1]

## Failures and how to do differently

- symptom: host serves the main portal or an application 404 -> cause: `portfolio.zacharyhounsa.ovh` is absent from the SRV-11 Nginx vhost -> fix: add and validate the dedicated vhost, then reload only after `nginx -t`. [Task 1]
- symptom: remote Nginx/certbot commands break -> cause: quoting or CRLF line endings -> fix: upload LF-encoded scripts, keep remote scripts simple, and make explicit backups. [Task 1]
- symptom: `script.pdf` is requested but unavailable -> cause: it was absent from repo, Git history, worktrees, SRV-11, SRV-12, and user-profile search -> fix: do not invent or substitute it; request the original file. [Task 1]

