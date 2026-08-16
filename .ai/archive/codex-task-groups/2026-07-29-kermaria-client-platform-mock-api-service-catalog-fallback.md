---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-29
---

# Task Group: kermaria-client-platform / mock API service catalog fallback

scope: Minimal restoration and verification of the mock `ClientServiceCatalogService` path while preserving the persistent MariaDB/catalog calculation.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for mock-versus-persistent service-catalog behavior; recheck the active branch and clean Git preconditions before applying, and treat the recorded HTTP 403 as a separate PortalService issue.

## Task 1: Restore the mock catalog fallback, partial

### rollout_summary_files

- rollout_summaries/2026-07-29T18-09-07-DRwE-restore_mock_service_catalog_fallback.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\29\rollout-2026-07-29T20-09-07-019faf10-e470-7820-b8c9-de4775b457d4.jsonl, updated_at=2026-07-29T19:09:27+00:00, thread_id=019faf10-e470-7820-b8c9-de4775b457d4, build/diff valid; suite blocked by unrelated 403)

### keywords

- ClientServiceCatalogService, MockPortalData.Services, IsPersistent, GetServicesAsync, sqlConfiguration.IsPersistent, npm.cmd run test:api, npm.cmd run build:api, HTTP 403, PortalService, X-Data-Source: mock, svc-personal-hosting-001

## User preferences

- before a scoped repair, the user required immediate stop if the branch, index, or worktree was non-conforming -> check the Git root, branch, index, and worktree first, then change only the authorized group [Task 1]
- the user requested “un seul agent d’écriture”, no frontend/infrastructure/documentation changes, and no commit/push/deployment -> keep the repair isolated and leave publication/external actions untouched [Task 1]

## Reusable knowledge

- `ClientServiceCatalogService` is the portal and administration projection source of truth. The minimal fallback is `using Kermaria.ApiInternal;` plus, at the start of `GetServicesAsync`, return `MockPortalData.Services` only when both `_subscriptions.IsPersistent` and `_commercialRepository.IsPersistent` are false. The `&&` preserves the persistent path as soon as either repository is persistent. [Task 1]
- Mock repositories expose `IsPersistent=false`; MariaDB repositories expose `true`; both follow `sqlConfiguration.IsPersistent`. The strengthened mock test checks exact ID/status pairs: `svc-personal-hosting-001|active`, `svc-backup-001|active`, `svc-vpn-001|pending`, `svc-rds-001|suspended`, `svc-support-001|active`, while the MariaDB canary excludes `svc-personal-hosting-001`. [Task 1]
- The existing mock smoke already checks HTTP 200, `X-Data-Source: mock`, five services, and three active services. `npm.cmd run build:api` completed with 0 errors/0 warnings and `git diff --check` passed. [Task 1]

## Failures and how to do differently

- symptom: `npm.cmd run test:api` compiles but fails at `Un service hors client devait être refusé avec HTTP 403.` near `tests/api-internal/Program.cs:1080` -> cause: separate `PortalService` authorization behavior, not the catalog fallback -> fix: record it as out of scope and do not repair it in a catalog-only task without explicit authorization. [Task 1]

