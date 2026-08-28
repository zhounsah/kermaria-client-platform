# Current state - Zachary IT platform
Last verified: 2026-08-28
Current production release: `v2.0.0.6`
Release commit: `9f47f2558bb5a2cc4b2b0f880f3ea97456cae1b2`
This document is the primary entry point for the current platform state. Older V0.x/V1.x documents remain useful as implementation history, but they must not override this file, the current code, or the current deployment runbooks.
## Production topology
```text
Internet
  -> SRV-11 edge / TLS
     -> SRV-12 WEBPORTAL (Next.js, systemd, 192.168.100.212:3000)
        -> SRV-13 API-INTERNAL (.NET, Windows service, 192.168.100.213:5000)
           -> SRV-06 MariaDB
           -> Active Directory / provisioning integrations
```
Canonical hosts:
- public: `https://zachary-it.fr`
- client portal: `https://dashboard.zachary-it.fr`
- administration: `https://administration.zachary-it.fr`
Hard boundary:
```text
browser -> WEBPORTAL / BFF -> API-INTERNAL -> MariaDB
```
WEBPORTAL must not access MariaDB directly. API-INTERNAL remains private.
## Commercial authority
Billing V2 / V2.1 is the sole commercial authority in production.
The legacy commercial model was removed by migrations 070/071 during the `v2.0.0.0` cutover on 2026-08-25. The platform no longer uses the former cart/configurator/legacy subscription catalog as a competing authority.
Native commercial concepts include:
- services;
- service tiers;
- versioned service prices;
- presets/formulas;
- commitments and payment-mode discounts;
- Billing V2 subscriptions and effective price components;
- provider checkout/agreement state;
- provisioning projections.
Prices are immutable versions. A price is replaced by a new version; historical versions remain authoritative for the documents/contracts they produced.
## Admin catalog
Since `v2.0.0.2`, `/admin/catalog` is a business-oriented administration surface rather than a single technical form.
Main sections:
- Services
- Formules
- Engagements
- Integrations
Dedicated routes:
- `/admin/catalog/services/new`
- `/admin/catalog/services/[id]?tab=essential|tiers|pricing|commercialization`
- `/admin/catalog/formules/new`
- `/admin/catalog/formules/[id]?tab=essential|composition|preview`
- `/admin/catalog/engagements/new`
- `/admin/catalog/engagements/[id]?tab=essential|payments`
- `/admin/catalog/integrations`
Important behavior:
- tariffs are managed from the service/tier context, not as a top-level catalog section;
- VAT and discounts are entered as percentages and converted to Billing V2 basis points by the BFF/frontend helpers;
- formula price previews come from the server projection, never from an authoritative browser-side sum;
- unsaved drafts are guarded for internal navigation and the main autonomous subforms;
- inactive tiers do not lower the admin "A partir de" price;
- provider mapping remains advanced configuration; Stripe `price_data` inline does not require a pre-created Stripe price mapping.
Known non-blocking UX debt: browser Back/Forward navigation is not fully interceptable for unsaved drafts. Do not add fragile `popstate` history hacks without a dedicated design/review.
## Public services landing
Since `v2.0.0.5`, public `/services` is a problem-to-solution router before the technical catalog:
- six customer-need entry points route to the relevant service or educational resource;
- the four service universes remain the second navigation level;
- the hero keeps the audit action and no longer injects `Comparer les formules`;
- the public renderer accepts the legacy `storefront:services` JSON through a deterministic transition fallback; the next normal authenticated CMS save persists the strict `problemEntries` shape.
The client-portal `/services` route remains a separate authenticated surface and stays `noindex, nofollow` on portal hosts.
## Priority services and adaptive diagnostic
Since `v2.0.0.6`:
- six priority service pages use a customer-oriented renderer while Billing keeps authority over formula availability;
- `/services/domaines-messagerie` routes visitors from concrete messaging/domain problems instead of internal product vocabulary;
- `/diagnostic` accepts bounded contexts: `backup`, `remote-access`, `network`, `messaging`, `domain-dns`, `server`, `web-hosting`;
- unknown or missing diagnostic contexts fall back to the general orientation flow;
- service CTAs pass the relevant diagnostic context without changing Billing authority or creating a second pricing source.
## Current production deployment
API-INTERNAL active runtime:
- host: SRV-13
- service: `KermariaApiInternal`
- active commit marker: `a6eefcd09833bb7e2384f5a7694e47a4e6621cd1`
- pre-release backup: `C:\apps\api-internal-backups\20260827-190441-v2.0.0.4-b66e89d`
WEBPORTAL active runtime:
- host: SRV-12
- service: `kermaria-webportal`
- active release: `/opt/kermaria/releases/20260828-104050-v2.0.0.6-9f47f25`
- previous release retained: `/opt/kermaria/releases/20260827-191145-v2.0.0.5-a6eefcd`
Release artifacts:
- API zip SHA-256: `7F87D6B816B65AC9AC27F82BCF2F61107B77DF766F09EDD30117700271DE4E00`
- WEBPORTAL tar.gz SHA-256: `C0B93D3C84F5D9696FC8F774C5397D90D94E612E144284823AFB54E1C1F9580A`
Migration `072_editorial_resource_redirects` is present in production and was already applied before the v2.0.0.4 runtime cutover. No SQL write was required during the cutover.
## Production smoke test - 2026-08-28
Verified after deployment:
- API `/health` -> 200
- API `/health/live` -> 200
- API `/health/ready` -> 200
- MariaDB check -> healthy
- AD check -> `controlled_write`
- WEBPORTAL readiness from SRV-12 -> 200
- public dashboard readiness -> 200
- adaptive diagnostic general route -> 200;
- all seven bounded diagnostic contexts -> 200;
- invalid diagnostic context -> safe general fallback, 200;
- six priority service pages -> 200 with contextual diagnostic links;
- `/services/domaines-messagerie` -> 200 with customer-oriented renderer;
- checked service canonicals unchanged and no mojibake detected.
- `https://zachary-it.fr/` -> 200
- `/formules` -> 200
- `/tarifs` -> 200
- `/services` -> 200
- administration catalog routes -> HTTP 200 through the production host
- footer -> `Version v2.0.0`
- SRV-12 journal -> no warning/error entries for the service after restart
A direct RDC-07 request to `192.168.100.212:3000` can occasionally time out even while SRV-12-local and public readiness are healthy. Treat this as a network-path observation, not as application readiness truth.
## Documentation order
For current work, read in this order:
1. `docs/CURRENT_STATE.md` (this file)
2. `docs/IMPLEMENTATION_MAP_CURRENT.md`
3. `docs/BILLING_V2_ONLY.md`
4. `docs/OPERATIONS.md`
5. `docs/DEPLOYMENT.md`
6. `docs/GUIDE_ADMIN.md`
7. `docs/releases/V2.0.0.5.md`
Historical documents under V0.x, V1.x and `docs/v1.4/` document how the platform got here. They are not automatically current operational truth.