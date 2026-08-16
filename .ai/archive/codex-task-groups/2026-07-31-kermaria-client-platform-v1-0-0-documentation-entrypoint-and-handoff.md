---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-31
---

# Task Group: kermaria-client-platform / V1.0.0 documentation entrypoint and handoff

scope: Current-state-first, repository-wide documentation for Kermaria, with detailed V0.xx traceability and explicit onboarding for humans and AI agents.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse for documentation architecture, version-truth, or repository handoff work in this checkout; re-check tags, manifests, and the current document set before asserting a later release state.

## Task 1: Implement complete Kermaria platform documentation for v1.0.0, partial

### rollout_summary_files

- rollout_summaries/2026-07-31T14-59-40-DZPY-kermaria_v1_0_0_documentation_handoff.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\31\rollout-2026-07-31T16-59-40-019fb8b0-29a9-7e52-875b-4639ff19fe62.jsonl, updated_at=2026-07-31T15:11:00+00:00, thread_id=019fb8b0-29a9-7e52-875b-4639ff19fe62, current-state entrypoint and remaining DATA_MODEL.md gap)

### keywords

- V1.0.0_DOCUMENTATION.md, V1.0.0_FUNCTIONAL_REFERENCE.md, IMPLEMENTATION_MAP_CURRENT.md, DATA_MODEL.md, v1.0.0, README.md, browser -> WEBPORTAL / BFF -> API-INTERNAL -> MariaDB, V0.26_SELF_SERVICE_SIGNUP.md, V0.36_PANIER_UNIFIE_ABONNEMENTS_FACTURES.md

## User preferences

- when scoping documentation, the user chose "Plateforme complète" -> cover public site, client area, admin, BFF, API-INTERNAL, data, operations, security, and integrations by default, not only webportal pages. [Task 1]
- when choosing history, the user selected "Historique détaillé" -> retain V0.xx documents as traceability/context while making current state primary. [Task 1]
- when the user asked for documentation where "une personne physique ou un agent IA" can find answers -> include onboarding order, question-to-file routing, source files, commands, architecture boundaries, and takeover pitfalls. [Task 1]
- when the user approved with "PLEASE IMPLEMENT THIS PLAN" -> create the agreed repository files and cross-links rather than returning recommendations only. [Task 1]

## Reusable knowledge

- `docs/V1.0.0_DOCUMENTATION.md` is the canonical entrypoint; `docs/V1.0.0_FUNCTIONAL_REFERENCE.md` covers public/client/admin journeys, signup, payments/subscriptions, managed content, downloads, AD/KoXo, and feature-to-history mapping. [Task 1]
- Version truth for this release is Git tag `v1.0.0` at `7d473480e697cd72c05e56d63c212ebc997f59d7`, plus repository state; root and `apps/webportal/package.json` still say `0.1.0`, so manifests alone are not authoritative. [Task 1]
- Preserve the architecture boundary `browser -> WEBPORTAL / BFF -> API-INTERNAL -> MariaDB`; WEBPORTAL must not directly access MariaDB, AD, NAS, RDS, VPN, or BPCE. [Task 1]
- Navigation was added to `README.md`, `docs/IMPLEMENTATION_MAP_CURRENT.md`, `ARCHITECTURE.md`, `API_CONTRACT.md`, `DEPLOYMENT.md`, `OPERATIONS.md`, `SECURITY.md`, and `ROADMAP.md`; `git diff --check` and the targeted Markdown-link check passed. [Task 1]

## Failures and how to do differently

- symptom: a broad patch to `DATA_MODEL.md` does not apply -> cause: legacy/mismatched encoding makes literal context unreliable -> fix: use encoding-aware PowerShell/.NET rewriting or line-index insertion. [Task 1]
- symptom: the 1.0.0 documentation appears fully cross-linked -> cause: `DATA_MODEL.md` did not receive its direct 1.0.0 navigation banner -> fix: treat that banner as the remaining follow-up gap. [Task 1]
- symptom: documentation work includes unrelated functional changes -> cause: the starting worktree is dirty -> fix: stage/review only the explicit documentation file list. [Task 1]

