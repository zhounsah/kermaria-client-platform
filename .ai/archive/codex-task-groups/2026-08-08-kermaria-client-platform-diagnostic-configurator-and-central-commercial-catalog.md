---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-08
---

# Task Group: kermaria-client-platform / diagnostic configurator and central commercial catalog

scope: Public diagnostic/configurator work that must resolve against the canonical commercial catalog and validate all price-affecting selections server-side.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse for public-pack/catalog/signup configurator work; recheck active offers and current migrations before treating release data as current.

## Task 1: Implement diagnostic and configurator with central catalog, success

### rollout_summary_files

- rollout_summaries/2026-08-07T22-18-54-WYak-zachary_it_diagnostic_configurator_v1_1_13_deploy.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\08\rollout-2026-08-08T00-18-54-019fde4e-cddf-7bd2-af95-2ecd9cba76b0.jsonl, updated_at=2026-08-08T12:26:32+00:00, thread_id=019fde4e-cddf-7bd2-af95-2ecd9cba76b0, v1.1.13 implementation and deployment validated)

### keywords

- diagnostic, configurateur, public-packs, CommercialOfferSummary, externalReference, /diagnostic, /configurer, /api/configurer/resolve, 042_signup_catalog_configuration, 043_fiscal_regime_franchise_base

## User preferences

- the user explicitly asked to "ne pas créer une seconde logique de catalogue parallèle" -> reuse `packages/shared/src/index.ts`, `apps/webportal/lib/public-packs.ts`, and the real commercial catalog. [Task 1]
- the user required that the frontend never be the financial source of truth -> recalculate and validate configuration server-side before signup/order; reject arbitrary frontend parameters. [Task 1]
- the user wanted a complete, tested, documented implementation -> report routes, touched files, validation commands, and actual results. [Task 1]

## Reusable knowledge

- Public manifests and variants live in `packages/shared/src/index.ts`; resolve public offers against active `CommercialOfferSummary` using `externalReference`. The existing commercial fields cover price, setup fee, VAT, commitment, billing interval, payment mode, and `publicPackCode`. [Task 1]
- `apps/webportal/lib/public-packs.ts` provides `resolvePackSelection`, `selectionFromSearchParams`, and `selectionToQueryString`; use the server BFF in `apps/webportal/lib/internal-api.ts`, not direct browser calls to the internal API. [Task 1]
- Added routes are `/diagnostic`, `/configurer`, and `/api/configurer/resolve`; migrations are `apps/api-internal/Migrations/MariaDb/042_signup_catalog_configuration.sql` and `043_fiscal_regime_franchise_base.sql`. Historical release anchors: commit `b7980c4`, tag `v1.1.13`. [Task 1]
- Focused validation that passed: `npm run typecheck:webportal`, `npm run test:diagnostic-configurator`, `npm run lint:webportal`, `npm run check:secrets`, and `git diff --check`. [Task 1]

## Failures and how to do differently

- symptom: the browser starts calling the internal API or independently calculating a sale -> cause: bypassing the established BFF/catalog boundary -> fix: route through `internal-api.ts` and make server resolution authoritative. [Task 1]

