---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-30
---

# Task Group: Windows Dev workspace / local-only environment secret management

scope: Local RDC-07 environment-file changes and the safe handling boundary for secrets; no server mutation is implied.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev; reuse_rule=reuse only for local setup files; never infer credential equality or apply local values to remote servers.

## Task 1: Update local RDC-07 environment file without touching servers, partial

### rollout_summary_files

- rollout_summaries/2026-07-29T10-09-54-QAma-r740xd_srv11_12_13_deployment_zabbix_and_local_secret_update.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\29\rollout-2026-07-29T12-09-54-019fad5a-2650-7e90-982b-8c1b5e1af2ee.jsonl, updated_at=2026-07-30T16:25:41+00:00, thread_id=019fad5a-2650-7e90-982b-8c1b5e1af2ee, local-only change; unsafe credential exposure)

### keywords

- kermaria-client-platform.local.env.ps1, RDC-07, local-only, sans pour autant toucher aux serveurs, SQL_PASSWORD, AD_SERVICE_ACCOUNT_PASSWORD, SERVICE_AUTH_TOKEN, [REDACTED_SECRET]

## User preferences

- when the user asks for a change `sans pour autant toucher aux serveurs`, modify only `C:\Users\zhounsah\Documents\Dev\kermaria-client-platform.local.env.ps1` and do not make remote server changes. [Task 1]

## Reusable knowledge

- This rollout evidenced a file-system-only local update; it did not evidence remote mutation. Verify setup by variable names, modes, and paths, never by printing values. [Task 1]

## Failures and how to do differently

- symptom: plaintext credentials appear in conversational or tool output -> cause: secrets were copied/reused during local env editing -> fix: never retain or echo them; redact as `[REDACTED_SECRET]`, request secure injection/prompt/vault use, and recommend rotation for exposed credentials. [Task 1]
- symptom: two unrelated credential variables are synchronized -> cause: an assumed shared password without service proof -> fix: do not equate `SQL_PASSWORD` and `AD_SERVICE_ACCOUNT_PASSWORD` without explicit validation; a hash is not proof the credential is correct. [Task 1]

