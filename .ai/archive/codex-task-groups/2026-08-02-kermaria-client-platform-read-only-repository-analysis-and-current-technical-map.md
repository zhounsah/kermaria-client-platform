---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-02
---

# Task Group: kermaria-client-platform / read-only repository analysis and current technical map

scope: Strictly read-only reconnaissance of the Kermaria checkout, its Git state, architecture, runtime boundaries, toolchain, and validation entrypoints.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for repository analysis only when the user explicitly requests no modification or deployment; recheck current branch, Git state, and versions before treating observed values as current.

## Task 1: Analyse en lecture seule du dépôt Kermaria, success

### rollout_summary_files

- rollout_summaries/2026-08-02T12-05-09-6ZXQ-kermaria_depot_readonly_analysis.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\02\rollout-2026-08-02T14-05-09-019fc25d-1a1e-7c60-b3ec-80b10d4f7d32.jsonl, updated_at=2026-08-02T12:07:02+00:00, thread_id=019fc25d-1a1e-7c60-b3ec-80b10d4f7d32, no files changed and no deployment)

### keywords

- sans modifier aucun fichier, aucun déploiement, chore/remise-a-plat-agentique, Next.js 16.2.9, React 19.2.7, .NET 10.0.301, Node >=24, apps/webportal, apps/api-internal, packages/shared, npm run validate

## User preferences

- when repository analysis is requested, the user said: “sans modifier aucun fichier” and “N’effectue aucune modification et aucun déploiement” -> remain strictly read-only; do not run deployment actions. [Task 1]

## Reusable knowledge

- Architecture: browser -> Next.js/BFF (`apps/webportal`) -> private ASP.NET Core API (`apps/api-internal`) -> MariaDB and internal integrations; the webportal must not access MariaDB, AD, NAS, RDS, VPN, or BPCE directly, while `packages/shared` holds non-sensitive TypeScript contracts. [Task 1]
- Observed toolchain: Next.js 16.2.9, React 19.2.7, TypeScript 6.0.3, ESLint 9.39.4, .NET SDK 10.0.301, `MySqlConnector` 2.6.0, Node `>=24`. Main checks include `lint:webportal`, `typecheck:shared`, `typecheck:webportal`, `build:web`, `build:api`, `test:api`, `check:web`, and `validate`. [Task 1]
- For a read-only state report, separately run `git branch --show-current`, `git log -1 --oneline`, and `git status --short`; the observed branch/dirty state is checkout-specific. [Task 1]

## Failures and how to do differently

- symptom: a PowerShell reconnaissance chain fails at `&&` -> cause: this PowerShell environment does not accept that operator -> fix: execute commands separately or join them with `;`. [Task 1]
- symptom: the `filesystem` MCP server is unavailable -> fix: use PowerShell `Get-Content` and `Get-ChildItem` directly. [Task 1]

