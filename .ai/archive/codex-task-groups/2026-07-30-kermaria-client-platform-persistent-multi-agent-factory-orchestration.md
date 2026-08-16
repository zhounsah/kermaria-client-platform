---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-30
---

# Task Group: kermaria-client-platform / persistent multi-agent factory orchestration

scope: The `.codex/factory` infrastructure that persists phase state, enforces Git/QA gates, and supports autonomous Kermaria work without changing application code.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for this factory on branch `chore/remise-a-plat-agentique` after checking the current repository root, branch, and `.codex/factory/STATE.json`; exact phase/commit state is checkout-specific.

## Task 1: Build the persistent multi-agent factory, success

### rollout_summary_files

- rollout_summaries/2026-07-29T19-48-42-PGx0-persistent_multi_agent_factory_orchestrator.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\29\rollout-2026-07-29T21-48-42-019faf6c-100f-78a1-bdce-ca59db28458e.jsonl, updated_at=2026-07-29T20:12:09+00:00, thread_id=019faf6c-100f-78a1-bdce-ca59db28458e, factory-only infrastructure validated)

### keywords

- .codex/factory, STATE.json, ROADMAP.md, PROCESS.md, HUMAN_GATES.md, check-git-state.ps1, validate-phase.ps1, validate-global.ps1, update-state.ps1, QA_NO_PROGRESS_LIMIT, P04, PortalService, HTTP 403

## Task 2: Execute factory phases P04 through P10 RE_QA, paused at HUMAN_GATE

### rollout_summary_files

- rollout_summaries/2026-07-29T20-15-56-elbS-factory_p04_p10_paused_human_gate.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\29\rollout-2026-07-29T22-15-56-019faf84-fd1f-7e00-a5eb-d6b8df07cf94.jsonl, updated_at=2026-07-30T22:03:01+00:00, thread_id=019faf84-fd1f-7e00-a5eb-d6b8df07cf94, latest factory checkpoint; resume only after reconciliation)

### keywords

- HUMAN_GATE, HG-BUSINESS, P10, RE_QA, P10-CONCURRENT-PUBLIC-CONTENT-20260730, v0.39.1, d756592, v0.40, 391844a, f063e4f, PublicPackCard, CompletePhase, PortalAccessDeniedException

## User preferences

- when creating orchestration infrastructure, the user asked to “ne créer que l’infrastructure de l’usine” and no commit -> keep a strict boundary between factory files and application code; propose, but do not create, the commit [Task 1]
- when using `backup/snapshot-avant-remise-a-plat-2026-07-29`, the user required it to be a reference only, never globally merged or restored -> inspect grouped diffs and reimplement the minimal current-code change [Task 1]
- the user wants autonomous resumption without this conversation and human gates only for indispensable decisions -> persist state, make gates explicit, and automate the sequence after validation [Task 1]

## Reusable knowledge

- The real Git root is `C:\Users\zhounsah\Documents\Dev\kermaria-client-platform`; the parent `C:\Users\zhounsah\Documents\Dev` is not a usable Git root. Start with `git rev-parse --show-toplevel`. [Task 1]
- The factory consists of `.codex/factory/{ROADMAP.md,PROCESS.md,STATE.json,HUMAN_GATES.md,DECISIONS.md,BLOCKERS.md,PHASE_TEMPLATE.md}`, P00–P26 phase definitions, nine roles under `.codex/agents/`, and four PowerShell scripts. Initial validated state was `currentPhase: P04`, `currentStep: PRECHECK`; P04 covers the separate `PortalService` HTTP 403 refusal. [Task 1]
- `STATE.json` is a runtime checkpoint: it may remain modified between phase commits and must not be included in an application commit. The process is `Produire → Vérifier → Corriger → Revérifier → Commit local → Phase suivante`; block after three consecutive no-progress cycles (`QA_NO_PROGRESS_LIMIT`). [Task 1]
- Start with `validate-global.ps1 -FactoryOnly`, then `update-state.ps1 -Action StartPhase`. For a resume, run `check-git-state.ps1 -Mode Resume` first and run `update-state.ps1 -Action Resume` only when `interruption.active` is true. Remote operations (`push`, `merge`, `rebase`, `cherry-pick`, tag, deployment) remain human gates. [Task 1]
- At the July 30 pause, `runStatus=HUMAN_GATE`, `currentPhase=P10`, `currentStep=RE_QA`, blocker `HG-BUSINESS` / `P10-CONCURRENT-PUBLIC-CONTENT-20260730`; 12 of 29 entries were DONE (P00–P09 including P06A, plus P10A). Last factory-validated commit: `f063e4f`; P10 must not be treated as committed. [Task 2]
- `CompletePhase` requires the exact phase commit message, only allowlisted files in the commit, and only `STATE.json` left dirty afterward. P04’s one-line `PortalValidationException` -> `PortalAccessDeniedException` fix passed `npm.cmd run test:api`, `test:workflow`, `build:api`, and `git diff --check`, then committed as `3d2b0df`. [Task 2]
- Before resuming P10, reconcile a clean branch/worktree against separately published `v0.39.1 -> d756592` on `origin/main` and `v0.40 -> 391844a` on `origin/chore/remise-a-plat-agentique`; then prioritize P10–P14 and defer P15–P26 unless their scope remains justified. [Task 2]

## Failures and how to do differently

- symptom: Git inspection returns `fatal: not a git repository` -> cause: commands ran from the parent `Dev` folder -> fix: locate and verify the repository root before inspecting or changing state. [Task 1]
- symptom: exploratory PowerShell searches are truncated or hide errors -> cause: output is too broad and exit codes are not handled -> fix: use targeted queries and explicit PowerShell return-code guards. [Task 1]
- symptom: a factory role lookup fails under `.codex/factory/roles` -> cause: roles actually live under `.codex/agents/` -> fix: use `.codex/agents/`. [Task 2]
- symptom: P10 static checks pass but UI behavior regresses -> cause: state-machine edge cases are not covered -> fix: test unavailable historical upfront selections, “Passer au mensuel”, dynamic prop refresh, and A→B→A override resurrection, not only regex assertions. [Task 2]
- symptom: concurrent edits/tags appear while P10 is paused -> cause: `STATE.json` alone does not capture external Git changes -> fix: do not resume, commit, stash, reset, or restore; inspect HEAD, remote refs, tags, index, worktree, and phase-allowlist overlap before explicit reconciliation. A published tag does not make the factory phase complete. [Task 2]

