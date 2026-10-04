# Current state - Zachary IT platform
Last verified: 2026-10-04
Current production release: `v2.0.3.2`
Release commit: `62edcb7dc272cb3e42effc34ec8f3001fe437b2f`
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
- active application release: `v2.0.3.2` / `62edcb7dc272cb3e42effc34ec8f3001fe437b2f`
- rollback copy: `C:\apps\api-internal-old-v2.0.3.2`
- application DLL SHA-256: `2C59A29F94281FB578655AE67A05369A777638CC04E340B3C0E4EDD89854D088`
WEBPORTAL active runtime:
- host: SRV-12
- service: `kermaria-webportal`
- active release: `/opt/kermaria/releases/v2.0.3.2-62edcb7-prod`
- release commit: `62edcb7dc272cb3e42effc34ec8f3001fe437b2f`
- rollback release retained: `/opt/kermaria/releases/20261002-0920-v2.0.3.1-d366d46`
- artifact SHA-256: `E1EA6CF0A9B08B0D491D0C5DD1C30F38A1E9D678FD97C5B1BDD85A52267721CC`
- `.next/cache`: owned by `kermaria-web:kermaria-web`, created during deployment
MariaDB production schema includes `096_primary_identity_bootstrap` and
`097_billing_v2_provisioning_managed_memberships`, both already recorded on
2026-09-29. No production migration was executed for v2.0.3.2. Migration098
was applied only to kermaria_dev earlier in the DEV recipe. Runtime config
hashes were preserved for both environments; see docs/releases/V2.0.3.2.md.

## Verification v2.0.3.2 - 2026-10-04

DEV then PROD API and WebPortal are healthy. Canonical home, dashboard login,
administration login and readiness return HTTP200 from the operator network.
This is not evidence that the failed main WAN is reachable from the Internet.
KoXo quality replacement remains manual by explicit decision; DEV quality
changes refresh CSV without launching KoXo. Native identity creation retains
its existing workflow. Noe's cancellation, denied VPN/RDS access and preserved
personal data are recorded in docs/DEV_E2E_NOE_VALBRUME.md.
## Production smoke test - 2026-09-05
Verified after deployment of `v2.0.2.8`:
- WEBPORTAL service -> active on SRV-12;
- WEBPORTAL local `/`, `/api/health/live` and `/api/health/ready` -> 200;
- public `https://zachary-it.fr/`, `/formules`, `/services/vps` and `/diagnostic` -> 200;
- dashboard login, live and readiness endpoints -> 200;
- administration login -> 200;
- deployed artifact hash matches the locally built tagged artifact exactly;
- deployed manifest identifies `v2.0.2.8` / `f4e0235941e1424ddcf2183e0e09f5ad98fd6104`;
- authenticated checkout with a session cookie but no CSRF token returns `403 CSRF_FORBIDDEN` before payload parsing;
- the same checkout with a valid CSRF token and invalid `{}` payload returns `400 INVALID_REQUEST`, proving the guard runs first;
- systemd restart shows the expected old-process exit `143`, followed immediately by a successful start;
- API-INTERNAL and MariaDB were deliberately not redeployed because this release changes only WEBPORTAL/BFF security behavior.
Security scope of `v2.0.2.8`:
- authenticated client mutations now use the same double-submit CSRF model as the protected admin surfaces;
- direct authenticated mutation routes that bypass the common portal BFF are explicitly guarded;
- browser mutation helpers attach the CSRF token automatically while public unauthenticated endpoints remain excluded;
- the `/api/formules/souscrire` session and CSRF guards now run before JSON parsing and Billing V2 payload validation;
- a regression contract enforces this ordering;
- the previous WEBPORTAL release is retained for immediate symlink rollback.


## Documentation order
For current work, read in this order:
1. `docs/CURRENT_STATE.md` (this file)
2. `docs/IMPLEMENTATION_MAP_CURRENT.md`
3. `docs/BILLING_V2_ONLY.md`
4. `docs/OPERATIONS.md`
5. `docs/DEPLOYMENT.md`
6. `docs/GUIDE_ADMIN.md`
7. `docs/releases/V2.0.2.8.md`
Historical documents under V0.x, V1.x and `docs/v1.4/` document how the platform got here. They are not automatically current operational truth.
