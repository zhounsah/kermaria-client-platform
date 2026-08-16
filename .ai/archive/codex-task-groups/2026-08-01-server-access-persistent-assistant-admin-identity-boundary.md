---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-01
---

# Task Group: server access / persistent assistant-admin identity boundary

scope: Safe alternatives when a user asks to create server credentials or a privileged persistent identity for the assistant.
applies_to: cwd=C:\Users\zhounsah\Documents\Codex\2026-08-01\est-ce-que-tu-peux-te; reuse_rule=reuse as a security boundary across server-administration tasks; do not treat server ownership as authorization to create a persistent assistant identity.

## Task 1: Create a dedicated admin identity on SRV-11/12, declined

### rollout_summary_files

- rollout_summaries/2026-08-01T12-28-24-Y6WU-refuse_persistent_assistant_admin_account.md (cwd=\\?\C:\Users\zhounsah\Documents\Codex\2026-08-01\est-ce-que-tu-peux-te, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\01\rollout-2026-08-01T14-28-24-019fbd4c-088c-77a1-b25c-cf1500da9a7c.jsonl, updated_at=2026-08-01T12:34:48+00:00, thread_id=019fbd4c-088c-77a1-b25c-cf1500da9a7c, persistent assistant account correctly declined)

### keywords

- persistent assistant identity, SRV-11, SRV-12, least privilege, MFA, logging, revocation, service account

## Reusable knowledge

- Do not create or retain a privileged persistent identity for the assistant. Safer alternatives are a nominative human account with least privilege, MFA and logging; temporary scoped access with revocation; or a tightly limited service account only for genuine automation. [Task 1]

## Failures and how to do differently

- symptom: server ownership is presented as a reason to create a generic assistant admin -> cause: ownership does not remove the risk of durable privileged credentials -> fix: keep the boundary and offer scoped, auditable alternatives. [Task 1]

