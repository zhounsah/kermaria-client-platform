# Current state - Zachary IT platform
Last verified: 2026-08-28
Current production release: `v2.0.0.7`
Release commit: `494c8a4ca8f645e15668b72c97d2bd119e01f8ae`
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

Since 2.0.0.7:
- /admin/diagnostic configures the five diagnostic-profile -> Billing V2 formula mappings without code changes;
- diagnostic:recommendations uses the existing managed-content persistence, while its generic raw editor redirects to the structured diagnostic screen;
- configured formula codes are validated server-side against the current public Billing V2 catalog before persistence; unavailable or unset formulas fail safely to cadrage/devis.
## Current production deployment
API-INTERNAL active runtime:
- host: SRV-13
- service: `KermariaApiInternal`
- active commit marker: `494c8a4ca8f645e15668b72c97d2bd119e01f8ae`
- pre-release backup: `C:\apps\api-internal-backups\20260827-190441-v2.0.0.4-b66e89d`
WEBPORTAL active runtime:
- host: SRV-12
- service: `kermaria-webportal`
- active release: `/opt/kermaria/releases/20260828-171043-v2.0.0.7-494c8a4`
- previous release retained: `/opt/kermaria/releases/20260828-104050-v2.0.0.6-9f47f25`
Release artifacts:
- API zip SHA-256: `7EEB41EE15915705426B1CBCB82B729C0C9FBB484FC7CF3A0F0BAD15F417812B`
- WEBPORTAL tar.gz SHA-256: `4C5456155C7330F43F2DA1A33D6D1A33A26AA9CA20BC219A04358D06A58063FD`
Migration `072_editorial_resource_redirects` remains present in production. No SQL migration or direct SQL write was required by v2.0.0.7; the diagnostic mapping reuses managed-content persistence.
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
- /admin/diagnostic -> HTTP 200 through the production administration host
- /admin/backups -> HTTP 200 after the diagnostic/admin integration
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
7. `docs/releases/V2.0.0.7.md`
Historical documents under V0.x, V1.x and `docs/v1.4/` document how the platform got here. They are not automatically current operational truth.