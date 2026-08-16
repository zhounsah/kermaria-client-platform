---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-31
---

# Task Group: kermaria-client-platform / V0.40 KoXo integration reconnaissance

scope: Repository-first analysis and safe preparation for the unfinished V0.40 KoXo synchronization chain; use before resuming V0.40 implementation.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for V0.40 KoXo/signup/AD work in this checkout; treat it as analysis only until implementation and test evidence exists.

## Task 1: Analyze V0.40 KoXo integration before modification, partial

### rollout_summary_files

- rollout_summaries/2026-07-30T19-33-09-f64U-v040_koxo_analysis_and_portfolio_repair.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\30\rollout-2026-07-30T21-33-09-019fb484-2f44-7fe3-a3d1-2c9befc678d0.jsonl, updated_at=2026-07-31T07:23:55+00:00, thread_id=019fb484-2f44-7fe3-a3d1-2c9befc678d0, reconnaissance only; V0.40 was not delivered)

### keywords

- V0.40, docs/v0.40/PROMPT.MD, KoXo, X-Service-Auth, ServiceAuthenticationMiddleware.cs, SignupService.cs, MariaDbSignupRepository.cs, 034_v038_identity_alignment.sql, SERVICE_AUTH_TOKEN, scheduled task

## User preferences

- when beginning V0.40 work, the user required: `Analyse ensuite tout le dépôt avant toute modification` and a final report covering files, architecture, environment variables, tests, results, remaining configuration, and risks -> inspect the full governing spec and relevant code paths before editing. [Task 1]
- when implementing this KoXo lot, the user explicitly prohibited production deployment, real secrets, real data changes, and creation of the real scheduled task -> use mocks, temporary directories, dry-run behavior, and documentation unless later authorized. [Task 1]

## Reusable knowledge

- Start from `docs/v0.40/PROMPT.MD`, then inspect `docs/IMPLEMENTATION_MAP_CURRENT.md`, the V0.38 KoXo/AD documents, signup code, migration `034_v038_identity_alignment.sql`, environment example, internal authentication middleware, admin signup UI, and existing tests. [Task 1]
- Main implementation entry points are `apps/api-internal/Program.cs`, `Services/SignupService.cs`, `Data/Repositories/MariaDbSignupRepository.cs`, `Infrastructure/ServiceAuthenticationMiddleware.cs`, `apps/webportal/lib/internal-api.ts`, and admin signup pages/components. [Task 1]
- This 2026-07 V0.40 analysis predates the 2026-08 SRV-21 production webhook evidence. Kermaria remains Kermaria-first with AD creation at `set-password` and asynchronous KoXo handling, but use the newer `KoXo production webhook synchronization` block for current receiver/API/PowerShell evidence; the earlier scheduled-task chain was not delivered in this rollout. [Task 1]
- Internal API auth is `X-Service-Auth`, with SHA-256 hashing and constant-time comparison; it fails closed outside Development. `.env.example` contains placeholders only and must not receive real credentials. [Task 1]

## Failures and how to do differently

- symptom: V0.40 is described as delivered -> cause: the rollout pivoted to portfolio deployment troubleshooting before implementation or the requested test report -> fix: treat this evidence as analysis/progress and resume from the governing spec with a new implementation and verification run. [Task 1]
- symptom: broad search is noisy or fails under PowerShell -> cause: wildcard syntax/large `rg` scope -> fix: use explicit paths or PowerShell-compatible patterns. [Task 1]

