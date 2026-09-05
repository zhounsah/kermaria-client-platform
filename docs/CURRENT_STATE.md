# Current state - Zachary IT platform
Last verified: 2026-09-05
Current production release: `v2.0.2.7`
Release commit: `954c2910a9f9f7ac8fd9893da97948791e70502f`
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

Since v2.0.0.7:
- /admin/diagnostic configures the five diagnostic-profile -> Billing V2 formula mappings without code changes;
- diagnostic:recommendations uses the existing managed-content persistence, while its generic raw editor redirects to the structured diagnostic screen;
- configured formula codes are validated server-side against the current public Billing V2 catalog before persistence; unavailable or unset formulas fail safely to cadrage/devis.
## Current production deployment
API-INTERNAL active runtime:
- host: SRV-13
- service: `KermariaApiInternal`
- active application commit: `8b448933114a1cbe1a1e0404d5ed338b27378595` (`v2.0.2.6`; unchanged by the WEBPORTAL-only `v2.0.2.7` deployment)
- rollback copy: `C:\apps\api-internal-old-20260905-171911`
- executable SHA-256: `E40174C580FB265C5C22E23AD829E641B53FD0CA6AF6D6F7F444D7855F80F5AC`
WEBPORTAL active runtime:
- host: SRV-12
- service: `kermaria-webportal`
- active release: `/opt/kermaria/releases/20260905-164625-v2.0.2.7-954c291`
- release commit: `954c2910a9f9f7ac8fd9893da97948791e70502f`
- rollback release retained: `/opt/kermaria/releases/20260905-154724-v2.0.2.6-8b44893`
- artifact SHA-256: `8B41EBEB4415881FA8FC28DBE82ABFAC456DB8792D8E65EA968749A5BB820CB4`
- `.next/cache`: `kermaria-web:kermaria-web`, mode `750`
MariaDB production schema remains at `093_public_contact_identity_sync`; `v2.0.2.7` contains no SQL migration.
## Production smoke test - 2026-09-05
Verified after deployment of `v2.0.2.7`:
- WEBPORTAL service -> active on SRV-12;
- WEBPORTAL local `/`, `/api/health/live` and `/api/health/ready` -> 200;
- public `https://zachary-it.fr/`, `/formules`, `/services/vps` and `/diagnostic` -> 200;
- dashboard login, live and readiness endpoints -> 200;
- administration login -> 200;
- deployed artifact hash matches the locally built tagged artifact exactly;
- deployed manifest identifies `v2.0.2.7` / `954c2910a9f9f7ac8fd9893da97948791e70502f`;
- a mutation admin without CSRF token returns `403 CSRF_FORBIDDEN`;
- systemd restart shows the expected old-process exit `143`, followed immediately by a successful start;
- API-INTERNAL and MariaDB were deliberately not redeployed because this release changes only WEBPORTAL/BFF security behavior.
Security scope of `v2.0.2.7`:
- authenticated client mutations now use the same double-submit CSRF model as the protected admin surfaces;
- direct authenticated mutation routes that bypass the common portal BFF are explicitly guarded;
- browser mutation helpers attach the CSRF token automatically while public unauthenticated endpoints remain excluded;
- the previous WEBPORTAL release is retained for immediate symlink rollback.
Known follow-up after `v2.0.2.7`: `/api/formules/souscrire` currently parses and validates the checkout payload before its CSRF guard. No internal mutation is reachable before the guard, but the guard should be moved earlier so an authenticated request without a valid CSRF token fails consistently with `403` before business-payload validation.

## Documentation order
For current work, read in this order:
1. `docs/CURRENT_STATE.md` (this file)
2. `docs/IMPLEMENTATION_MAP_CURRENT.md`
3. `docs/BILLING_V2_ONLY.md`
4. `docs/OPERATIONS.md`
5. `docs/DEPLOYMENT.md`
6. `docs/GUIDE_ADMIN.md`
7. `docs/releases/V2.0.2.7.md`
Historical documents under V0.x, V1.x and `docs/v1.4/` document how the platform got here. They are not automatically current operational truth.
