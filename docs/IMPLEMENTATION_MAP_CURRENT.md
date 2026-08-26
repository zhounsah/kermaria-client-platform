# Implementation Map - Current State
Last verified: 2026-08-26
Production release: `v2.0.0.2` (`e227f8e98640dfac939534bd7c9b3d05d78efb57`)
Purpose: fast handoff for a human or another AI agent. Read `CURRENT_STATE.md` first for production truth.
## Architecture boundary
```text
browser -> WEBPORTAL / BFF -> API-INTERNAL -> MariaDB
```
- WEBPORTAL: Next.js on SRV-12.
- API-INTERNAL: .NET on SRV-13.
- MariaDB production: SRV-06.
- Edge/TLS: SRV-11.
- Active Directory and provisioning are reached from API-INTERNAL only.
## Canonical production hosts
- `zachary-it.fr` - public site
- `dashboard.zachary-it.fr` - authenticated client portal
- `administration.zachary-it.fr` - administration portal
## Commercial model
Billing V2 / V2.1 is the sole commercial authority.
The legacy `commercial_offers` / legacy subscription / cart / recurring checkout authority was removed in the v2 cutover. Historical V0.35/V0.36 docs describe removed architecture and must not be used as current implementation instructions.
Current commercial building blocks:
- `billing_v2_services`
- service tiers
- immutable/versioned service prices
- presets/formulas
- commitments and payment options
- Billing V2 subscriptions/items/effective price components
- provider checkout/agreement state
- document snapshots
- provisioning projections
## Public commercial flow
Primary surfaces:
- `/services`
- `/services/[category]`
- `/formules`
- `/formules/[code]`
- `/tarifs`
- `/souscrire`
- `/signup`
The former `/panier` and `/configurer` commercial flows are not current authority.
## Administration catalog
Root: `/admin/catalog`
Main navigation:
- Services
- Formules
- Engagements
- Integrations
Routes:
- `/admin/catalog/services/new`
- `/admin/catalog/services/[id]`
- `/admin/catalog/formules/new`
- `/admin/catalog/formules/[id]`
- `/admin/catalog/engagements/new`
- `/admin/catalog/engagements/[id]`
- `/admin/catalog/integrations`
Important WEBPORTAL implementation:
- `apps/webportal/components/admin/catalog/CatalogHome.tsx`
- `ServiceCatalogEditor.tsx`
- `ServiceTiersPanel.tsx`
- `ServicePricingPanel.tsx`
- `FormulaCatalogEditor.tsx`
- `CommitmentCatalogEditor.tsx`
- `CatalogIntegrations.tsx`
- `AdminCatalogUi.tsx`
- `AdminCatalog.module.css`
- `useAdminCatalogCommand.ts`
- `apps/webportal/lib/admin-catalog-units.ts`
- `apps/webportal/lib/admin-catalog-presenters.ts`
- `apps/webportal/lib/billing-v2-catalog-commands.ts`
The old `AdminBillingV2Catalog.tsx` monolith was removed in `v2.0.0.2`.
## Pricing invariants
- Never edit an existing price version in place.
- Publish a new price version and close/supersede the applicable window.
- Historical price versions remain authoritative for historical commercial output.
- Browser/UI helpers may format/convert values but must not become an authoritative pricing engine.
- Formula preview uses a server projection.
- `A partir de` is presentation-only and excludes inactive tiers.
## Provider integrations
Stripe:
- Billing V2 can build checkout lines with inline `price_data`.
- A Stripe external price mapping is not a general checkout prerequisite.
PayPal:
- provider plans/mappings may be required by the configured PayPal rail.
- PayPal configuration was not changed by `v2.0.0.2`.
## Subscription cancellation
Current architecture:
```text
request -> policy -> local transaction(status + outbox + audit)
        -> dispatcher -> provider -> convergence
```
Term-end cancellation preserves paid entitlements until the contractual period end while blocking inappropriate new mutations.
## Commercial documents
Commercial documents retain independent snapshots and do not rely on a current catalog row to remain historically valid.
## Provisioning
Billing V2 service topology and subscription projections drive provisioning. Do not reintroduce offer-based legacy topology.
## Admin/client areas outside catalog
Administration includes customers, signups, service/support requests, payments, subscriptions, downloads, editorial/content, audit/activity, KoXo, sessions and Billing V2 operations/readiness.
Client portal includes dashboard, profile, subscriptions, documents/invoices, downloads, backups, solutions, support/service requests and notifications.
## Validation entry points
Important commands include:
```text
npm run lint:webportal
npm run typecheck:shared
npm run typecheck:webportal
npm run build:webportal
npm run test:catalog
npm run test:formules
npm run test:billing
npm --prefix apps/webportal run test:admin
npm --prefix apps/webportal run test:forms
```
API Release builds with .NET 10. The Windows production service requires an apphost executable; release publishing therefore uses `-r win-x64 --self-contained false -p:UseAppHost=true`.
## Production deployment truth
See:
- `CURRENT_STATE.md`
- `DEPLOYMENT.md`
- `OPERATIONS.md`
- `WEBPORTAL_SRV12_DEPLOYMENT.md`
- `releases/V2.0.0.2.md`
Current active commit on API and WEBPORTAL: `e227f8e98640dfac939534bd7c9b3d05d78efb57`.
## Known non-blocking debt
- Browser Back/Forward draft protection is intentionally not implemented with fragile history hacks.
- Many old V0.x/V1.x documents remain in the repository as historical implementation records. When they conflict with current docs/code, current docs/code win.