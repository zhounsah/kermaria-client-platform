---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-01
---

# Task Group: kermaria-client-platform / test_web selective migration and production network topology

scope: Decide what is worth moving from the `test_web` MariaDB database, deploy a web/API correction safely, and route infrastructure questions through the dedicated SRV-11/12/13 topology.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev (repo=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform); reuse_rule=reuse migration scope only after confirming the business value of the current data; recheck live topology and service state before any deployment.

## Task 1: Inventaire de `test_web` et migration sélective, success

### rollout_summary_files

- rollout_summaries/2026-08-01T10-47-44-bCgO-test_web_migration_inventory_and_kermaria_network_topology.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\01\rollout-2026-08-01T12-47-44-019fbcef-de70-73b3-a8e6-a21f9b2cca52.jsonl, updated_at=2026-08-01T20:37:43+00:00, thread_id=019fbcef-de70-73b3-a8e6-a21f9b2cca52, validated minimal migration scope)

### keywords

- test_web, commercial_offers, service_catalog, managed_content_entries, schema_migrations, --apply-migrations, npm run backup:mariadb, information_schema, ERROR 1054, created_at

## Task 2: Déploiement web/API, confidentialité, et topologie Internet, success

### rollout_summary_files

- rollout_summaries/2026-08-01T10-47-44-bCgO-test_web_migration_inventory_and_kermaria_network_topology.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\01\rollout-2026-08-01T12-47-44-019fbcef-de70-73b3-a8e6-a21f9b2cca52.jsonl, updated_at=2026-08-01T20:37:43+00:00, thread_id=019fbcef-de70-73b3-a8e6-a21f9b2cca52, build and public HTML verified)

### keywords

- FileLoadException, 0x80070020, KermariaApiInternal, kermaria-webportal, politique-confidentialite, SRV-11, SRV-12, SRV-13, 192.168.100.211, 192.168.100.212, 192.168.100.213, dashboard.zacharyhounsa.ovh

## User preferences

- when deciding a `test_web` migration, the user said: “les clients, abonnements, documents etc... n'a aucune valeur réelle. Seul les offres ont une vraie valeur.” -> do not infer migration scope from row counts; preserve only business-validated reference data. [Task 1]
- the user validated `commercial_offers`, `service_catalog`, and real managed content -> treat them as the minimal expected migration scope unless the user revises it. [Task 1]

## Reusable knowledge

- `test_web` was a stabilisation/recette database; the documented production target is a clean `kermaria` database. MariaDB migrations live at `apps/api-internal/Migrations/MariaDb/[0-9]*.sql`, run explicitly through `--apply-migrations`, and record history in `schema_migrations`. Back up with `npm run backup:mariadb`; never version a dump. [Task 1]
- Confirmed Internet path: Internet -> SRV-11 Nginx/TLS (`192.168.100.211`) -> private SRV-12 Next.js (`192.168.100.212:3000`) -> SRV-13 API (`192.168.100.213`) -> SQL. Do not expose SRV-12, SRV-13, or MariaDB directly. [Task 2]
- For release verification, check compiled artifacts, public readiness, and exact requested public HTML markers; the privacy page was verified to contain `Politique de confidentialité`, `Informations légales`, and `Version du 1er août 2026`. [Task 2]

## Failures and how to do differently

- symptom: SQL assumes a shared `created_at` column and returns `ERROR 1054` -> cause: timestamp schemas differ -> fix: inspect each table with `information_schema` or `SHOW COLUMNS`. [Task 1]
- symptom: API restart crashes with `System.IO.FileLoadException ... file is used by another process (0x80070020)` -> cause: binaries were copied while the process still held file handles -> fix: confirm full service/process stop, copy cold, then restart. [Task 2]

