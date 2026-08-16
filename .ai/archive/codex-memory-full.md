# Task Group: kermaria-client-platform / editorial platform, public SEO navigation, and v1.3.3.4 canonicalisation release

scope: Analyze, extend, and deploy the administrable Wiki/SEO/FAQ engine; ensure published SEO pages are reachable from public navigation as well as indexed/sitemap-safe.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=\\?\C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse for this monorepo's editorial CMS, Wiki routing, public SEO navigation, or SRV-12 web release work; revalidate active release, public routes, and production state before treating them as current.

## Task 1: Analyze the existing editorial architecture, success

### rollout_summary_files

- rollout_summaries/2026-08-09T05-05-13-Ter1-kermaria_editorial_platform_seo_navigation_hotfix_v1_3_3_1.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\09\rollout-2026-08-09T07-05-13-019fe4e9-280d-75c3-b454-27b50fc1d51b.jsonl, updated_at=2026-08-10T21:59:41+00:00, thread_id=019fe4e9-280d-75c3-b454-27b50fc1d51b, read-only architecture analysis before v1.3 editorial work)

### keywords

- managed_content_entries, ManagedContentService, MariaDbManagedContentRepository, IEditorialService, MariaDbEditorialRepository, /internal/public/editorial, /internal/admin/editorial, getPublicData, internal_admin

## Task 2: Implement and deliver the Wiki/SEO/FAQ editorial platform, success

### rollout_summary_files

- rollout_summaries/2026-08-09T05-05-13-Ter1-kermaria_editorial_platform_seo_navigation_hotfix_v1_3_3_1.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\09\rollout-2026-08-09T07-05-13-019fe4e9-280d-75c3-b454-27b50fc1d51b.jsonl, updated_at=2026-08-10T21:59:41+00:00, thread_id=019fe4e9-280d-75c3-b454-27b50fc1d51b, v1.3 editorial engine and SRV-12 deployment validated)

### keywords

- /admin/editorial, app/[slug]/page.tsx, react-markdown, publicPath, status, noIndex, revisions, redirections, scripts/pack-webportal-release.ps1, v1.3.3, kermaria-webportal

- Related skill: skills/kermaria-srv12-srv13-runtime-deploy/SKILL.md

## Task 3: Hotfix public SEO navigation and release v1.3.3.1, success

### rollout_summary_files

- rollout_summaries/2026-08-09T05-05-13-Ter1-kermaria_editorial_platform_seo_navigation_hotfix_v1_3_3_1.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\09\rollout-2026-08-09T07-05-13-019fe4e9-280d-75c3-b454-27b50fc1d51b.jsonl, updated_at=2026-08-10T21:59:41+00:00, thread_id=019fe4e9-280d-75c3-b454-27b50fc1d51b, public `/ressources` hub, Services navigation, and v1.3.3.1 release verified)

### keywords

- /ressources, Services, /solutions, noindex, getPublicEditorialSitemap(), contentType === "seo_page", displayVersion, v1.3.3.1, 884294028c95c9c40b17bdd80c9773ef35078360, Pack grand public

## Task 4: Implement and deploy dashboard/www canonicalisation and SEO release v1.3.3.4, success

### rollout_summary_files

- rollout_summaries/2026-08-11T08-49-56-tEGx-kermaria_seo_canonicalisation_deploiement_v1_3_3_4.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\11\rollout-2026-08-11T10-49-56-019ff003-9e36-7480-ae64-b09abc04be55.jsonl, updated_at=2026-08-11T12:27:03+00:00, thread_id=019ff003-9e36-7480-ae64-b09abc04be55, production-validated host routing, metadata, sitemap, favicon, and real 404)

### keywords

- www.zacharyhounsa.ovh, dashboard.zacharyhounsa.ovh, administration.zacharyhounsa.ovh, public-route-config.ts, proxy.ts, public-metadata.ts, robots.txt, sitemap.xml, readInternalJsonOrNull, PORTAL_DATA_NOT_FOUND, v1.3.3.4, 7ea23f3, SHA256

- Related skill: skills/kermaria-srv12-srv13-runtime-deploy/SKILL.md

## Task 5: Correct the real HTTP 404 for a missing editorial slug, success

### rollout_summary_files

- rollout_summaries/2026-08-11T08-49-56-tEGx-kermaria_seo_canonicalisation_deploiement_v1_3_3_4.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\11\rollout-2026-08-11T10-49-56-019ff003-9e36-7480-ae64-b09abc04be55.jsonl, updated_at=2026-08-11T12:27:03+00:00, thread_id=019ff003-9e36-7480-ae64-b09abc04be55, API empty-body diagnosis and 404 correction)

### keywords

- readInternalJsonOrNull, response.text(), status=200 bytes=0, PORTAL_DATA_NOT_FOUND, notFound(), codex-missing-seo-check, HTTP 404, 03925ff, 7ea23f3

## User preferences

- when starting editorial work, the user asked to "commencer par une analyse en lecture seule" and generate "aucun faux contenu" -> map the existing architecture first; create only the engine and strictly necessary fixtures, never marketing filler. [Task 1][Task 2]
- when extending the CMS, the user wants to reuse the existing administration and authentication -> retain the Next BFF/internal API/session/CSRF, `internal_admin`, and audit flow instead of a second framework. [Task 1]
- when the user corrected "Mais les pages SEO ne sont pas dans le header ?" -> verify each delivered editorial feature has a coherent public-navigation entrypoint, not merely a direct URL or sitemap presence. [Task 3]
- when the user requested "Hotfix 1.3.3.1" -> preserve the requested display label while keeping npm `version` valid SemVer through a separate `displayVersion`. [Task 3]
- when the user asked "Commit, tag push comme d'habitude hein ;)" -> for this repository's implementation/release requests, complete the circuit: explicit intended-file commit, annotated tag, push `main` and tag, deployment, and production verification rather than stopping after local tests. [Task 4]
- when the user requested the complete SEO plan -> execute the changes, tests, publication, and deployment end-to-end rather than returning recommendations only. [Task 4]


## Reusable knowledge

- The editorial model covers Wiki, SEO pages, and FAQ with statuses, slugs, categories, placements/scopes, revisions, redirects, and SEO metadata. Public Wiki routes are `/wiki`, `/wiki/article/[slug]`, and `/wiki/categorie/[slug]`; admin CRUD/publication/archive/revision/category work is under `/admin/editorial`. [Task 2]
- Preserve the established boundary: admin mutations go through Next.js BFF with session and CSRF, then the internal API; the public frontend does not access MariaDB directly. `internal_admin` mutations use `IAuditService`/`audit_logs`; migration `045_editorial_platform.sql` lives in `apps/api-internal/Migrations/MariaDb/`. [Task 1][Task 2]

- `managed_content_entries`/`ManagedContentService` is historical Markdown content with a closed key registry; the dynamic engine is `IEditorialService`/`MariaDbEditorialRepository`, public `/internal/public/editorial/*`, admin `/internal/admin/editorial/*`, `app/[slug]/page.tsx`, and the public editorial sitemap. Do not treat the former as a complete dynamic CMS. [Task 1]
- `/solutions` is a client portal and `noindex`; `/ressources` is the public SEO hub. It reuses `getPublicEditorialSitemap()` and includes only `contentType === "seo_page"`, `!noIndex`, and `publicPath`, so no API or migration is needed. [Task 3]
- Canonical host mapping: `www.zacharyhounsa.ovh` is the public vitrine; `dashboard.zacharyhounsa.ovh` is client; `administration.zacharyhounsa.ovh` is admin. Centralize host-aware behavior in `apps/webportal/lib/public-route-config.ts` and `proxy.ts`: vitrine paths on dashboard/admin return 301 to `www`, while `/login`, `/dashboard`, `/set-password`, API, and private zones remain local. `dashboard/robots.txt` returns `Disallow: /` without a sitemap and `dashboard/sitemap.xml` is 404. [Task 4]
- Public SEO metadata belongs in `lib/public-metadata.ts`; v1.3.3.4 also added the French 404, a real favicon, corrected public titles/OG/canonical, and removed Wiki URLs from the `www` sitemap. [Task 4]
- Current v1.3.3.4 proof: `npm --prefix apps/webportal run test:seo`, `npm run typecheck:webportal`, and `npm run build:webportal` passed; commit `7ea23f3eca384db849b4a4bfacedd40ea2efc2a5` and annotated tag `v1.3.3.4` were pushed. Artifact hash was `0D5B267B6E291231E1A736EB0A1C23D61B92A706DADF0FBAAE1D1BDD82C13342`; active SRV-12 release was `/opt/kermaria/releases/20260811-122606-v1.3.3.4`, with active service and healthy internal readiness. Public checks proved dashboard `/` and `/offres` 301 to `www`, dashboard `/login` 200 noindex, dashboard robots/sitemap behavior, a missing `www` route 404, favicon 200 `image/x-icon`, `wiki_count=0`, and displayed version `v1.3.3.4`. [Task 4]
- The editorial redirects endpoint can return `200` with an empty body for no redirect, while the SEO endpoint returns `404` with `PORTAL_DATA_NOT_FOUND` for a missing page. `readInternalJsonOrNull` must use `response.text()` and return `null` for an empty body before `JSON.parse`, letting the proxy produce the actual public 404. [Task 5]

## Failures and how to do differently

- symptom: a marker-based package script fails without producing an archive -> cause: the expected marker is not in the file actually inspected -> fix: use a marker confirmed in that file before packaging. [Task 2]
- symptom: a deployment script reports a false readiness failure -> cause: Windows line endings leave `\r` in a temporary path -> fix: after a switch, verify with simple independent SSH commands. [Task 2]
- symptom: scripted/serialized Next output appears to retain forbidden text -> cause: strings in JSON or `script`/`style` are not visible page content -> fix: remove `script`/`style` before checking visible HTML. [Task 3]
- symptom: PowerShell verification fails around typographic apostrophes or Git `^{}` refs -> cause: quoting/PowerShell interpretation -> fix: use protected double-quoted strings or ASCII markers and verify peeled remote tags with `git ls-remote`. [Task 2][Task 3]

- symptom: private readiness is healthy but the UI release is wrong -> cause: readiness does not prove the active web symlink or public route -> fix: also verify active symlink, public HTML/version, and the exact requested URL. [Task 2][Task 3]
- symptom: `notFound()` renders the 404 page but a missing editorial route still returns HTTP 200 -> cause: the redirect API contract is `200` plus empty body, not JSON/404 -> fix: detect empty `response.text()` as no redirect in `proxy.ts`, then verify the real HTTP status of a missing public slug. [Task 5]
- symptom: `pack-webportal-release.ps1` rejects `-ExpectedSourceText` -> cause: it inspects only `apps/webportal/app/page.tsx` -> fix: provide an actual home-page text marker, not a `proxy.ts` symbol. [Task 4]
- symptom: remote deployment heredocs or env loading behave unexpectedly -> cause: PowerShell injects CRLF and `/etc/kermaria/webportal.env` has BOM/CRLF/quoted values -> fix: pipe remote Bash through `tr -d '\r' | bash -s` and do not `source` that env file without cleaning. Query SRV-12 on `192.168.100.212:3000`, not loopback. [Task 4]

# Task Group: SRV-11 Nextcloud Nginx vhost audit and activation

scope: Read-only Nginx/TLS audit followed by a bounded, transactionally validated activation of `nextcloud.home.bzh` through the existing SRV-11 HAProxy edge.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev; reuse_rule=reuse only for SRV-11 Nginx/HAProxy topology changes; re-audit loaded config, certificates, listeners, and backend before applying another vhost change.

## Task 1: Audit the SRV-11 Nginx/TLS architecture, success

### rollout_summary_files

- rollout_summaries/2026-08-04T13-56-54-KDlB-srv11_nextcloud_nginx_vhost_audit_and_apply.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\04\rollout-2026-08-04T15-56-54-019fcd10-20e5-7882-9747-a5190896cff7.jsonl, updated_at=2026-08-04T14:16:02+00:00, thread_id=019fcd10-20e5-7882-9747-a5190896cff7, read-only audit before user-approved application)

### keywords

- KERMARIA-SRV-11, Nginx, HAProxy, nextcloud.home.bzh, send-proxy-v2, 127.0.0.1:8443, home-bzh-clean-fullchain.pem, cloudflared, Plink

## Task 2: Activate and verify the Nextcloud vhost, success

### rollout_summary_files

- rollout_summaries/2026-08-04T13-56-54-KDlB-srv11_nextcloud_nginx_vhost_audit_and_apply.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\04\rollout-2026-08-04T15-56-54-019fcd10-20e5-7882-9747-a5190896cff7.jsonl, updated_at=2026-08-04T14:16:02+00:00, thread_id=019fcd10-20e5-7882-9747-a5190896cff7, nginx test/reload and end-to-end HTTP/HTTPS proof)

### keywords

- /etc/nginx/sites-available/nextcloud.home.bzh, /etc/nginx/sites-enabled/nextcloud.home.bzh, proxy_protocol, proxy_pass, 192.168.100.209:8080, nginx -t, HSTS, CardDAV, CalDAV

## User preferences

- when infrastructure changes are requested, the user said: "Avant toute modification, réalise uniquement un audit en lecture seule" -> inventory and report the verifiable state before modifying anything. [Task 1]
- when auditing TLS, the user required that private-key contents never be displayed, while paths, permissions, and public certificates are checked -> keep that separation. [Task 1]
- when approving the vhost change, the user required a backup, `nginx -t` before reload, automatic restore on test failure, then HTTP/HTTPS/log checks; they also said "Ne modifie pas HAProxy" and "Ne modifie pas cloudflared" -> use this bounded transactional workflow and preserve excluded systems. [Task 2]

## Reusable knowledge

- Active edge topology was Internet -> HAProxy `:443` -> Nginx `127.0.0.1:8443` with `send-proxy-v2`; HTTP `:80` reaches Nginx directly. Do not configure Nginx to listen directly on `443` in this topology. [Task 1]
- The wildcard Let's Encrypt certificate `/etc/ssl/kermaria/home-bzh-clean-fullchain.pem` covered `nextcloud.home.bzh`; its key had mode `600`, while the public certificate had mode `644`. `cloudflared` had no Nextcloud route, so it was intentionally unchanged. [Task 1]
- The working vhost uses `listen 127.0.0.1:8443 ssl proxy_protocol`, `set_real_ip_from 127.0.0.1`, `real_ip_header proxy_protocol`, `http2 on`, and `proxy_pass http://192.168.100.209:8080`; it includes ACME handling, HTTP-to-HTTPS redirect, 10G uploads, disabled buffering, 3600s proxy timeouts, `X-Forwarded-*`, HSTS without `includeSubDomains`/`preload`, and CardDAV/CalDAV redirects. [Task 2]
- Validation proved `nginx -t`, reload success, local HTTP `301`, HAProxy HTTPS `HTTP/2 302` to `/login` with HSTS, expected backend redirect, and no recent Nginx/Nextcloud errors. [Task 2]

## Failures and how to do differently

- symptom: complex inline remote commands fail or quote incorrectly -> cause: nested shell/Plink quoting -> fix: send a temporary script with Plink `-m`; write remote shell scripts as ASCII to avoid `bash: ... ï»¿set: command not found` from an UTF-8 BOM. [Task 1]
- symptom: an initial HTTPS response seems to be the wrong app or lacks HSTS -> cause: a single request is insufficient routing proof -> fix: cross-check `nginx -T`, SNI via `openssl s_client`, direct backend, and repeated curl through HAProxy before concluding the vhost is wrong. [Task 2]

# Task Group: kermaria-client-platform / public isolated client-space demo

scope: Build and release a realistic public client-space demonstration that is visibly DEMO, read-only, entirely fictitious, and isolated from production services.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse for public Next.js demo experiences only; preserve isolation from production APIs, customer data, billing, and authentication.

## Task 1: Build and deploy the public client-space demo, success

### rollout_summary_files

- rollout_summaries/2026-08-08T15-08-31-fTgK-kermaria_public_client_demo_v1_2_1.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\08\rollout-2026-08-08T17-08-31-019fe1eb-21f2-7a02-95ea-b04240850702.jsonl, updated_at=2026-08-08T18:54:09+00:00, thread_id=019fe1eb-21f2-7a02-95ea-b04240850702, deployed and public-ready as v1.2.1)

### keywords

- decouvrir-espace-client, demo-client-space, mock-data, DemoClientSpace.tsx, PublicShell.tsx, v1.2.1, efd8c7e, check:web, test:seo, SRV-12

- Related skill: skills/kermaria-srv12-srv13-runtime-deploy/SKILL.md

## User preferences

- when building a public demo, the user required a realistic experience using only fictitious data, clearly marked DEMO, read-only, and isolated from production -> keep data local/mock and make the isolation visible to visitors. [Task 1]
- when correcting visible French copy, the user said: "Par contre, tu mets jamais les accents..." -> use proper French accents in UI, metadata, breadcrumbs, and CTA labels; do not impose ASCII-only text. [Task 1]
- when asking where the demo was offered, the user made clear that a direct URL was insufficient -> expose public demos in the commercial/public navigation, not only in a sitemap. [Task 1]

## Reusable knowledge

- Centralized demo data is `apps/webportal/lib/demo-client-space/data.ts`; interactive local behavior is `apps/webportal/components/DemoClientSpace.tsx`; the public catch-all route is `apps/webportal/app/decouvrir-espace-client/[[...section]]/page.tsx`. Its subpaths cover services, abonnement, factures, stockage, sauvegardes, utilisateurs, assistance, securite, activite, and profil. [Task 1]
- The demo must make no calls to production APIs, authentication, billing, backup, or customer services. Put `Démo espace client` in both `apps/webportal/components/PublicShell.tsx` header and footer; public route registration is in `apps/webportal/lib/public-route-config.ts`. [Task 1]
- Release evidence: commit `efd8c7e637cc7741747f0fe60e47b48d2e041351`, tag `v1.2.1`, active SRV-12 release `/opt/kermaria/releases/20260808-185326-v1.2.1`; `npm run check:secrets`, `npm run check:web`, and `npm run test:seo` passed. Verify the public URL and absence of technical names such as `Veeam`, `KoXo`, `Stripe`, `PayPal`, `Nextcloud`, and `VPN` from checked demo surfaces. [Task 1]

## Failures and how to do differently

- symptom: a large patch containing accents fails to match -> cause: terminal encoding/special characters prevent exact matching -> fix: patch smaller files/regions and inspect UTF-8/raw bytes before replacing accented lines. [Task 1]
- symptom: an internal readiness curl briefly fails immediately after the service swap -> cause: startup timing -> fix: do not call the deployment failed from that probe alone; confirm later external/public readiness and requested rendered markers. [Task 1]

# Task Group: kermaria-client-platform / Veeam backup status and release handoff

scope: Customer-safe Veeam protection status from internal collector through API/MariaDB to the client portal, plus precise release status across SRV-13, SRV-16, and SRV-12.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse for Veeam/customer backup-status work in this Kermaria topology; revalidate business mapping, collector state, and public release before treating status as current.

## Task 1: Implement Veeam collection and business mapping, success

### rollout_summary_files

- rollout_summaries/2026-08-08T11-59-26-9Qt6-veeam_backup_status_v1_1_14_partial_deployment.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\08\rollout-2026-08-08T13-59-26-019fe13e-07a5-70a1-9aa7-3e8293392104.jsonl, updated_at=2026-08-08T15:06:53+00:00, thread_id=019fe13e-07a5-70a1-9aa7-3e8293392104, API/collector implementation and mapping validated)

### keywords

- Veeam, KoXoDATA, backup_jobs, backup_runs, protection_status, 044_veeam_backup_status, X-Service-Auth, Invoke-VeeamBackupCollection.ps1, test:backups

## Task 2: Commit, tag, and deploy v1.1.14, partial

### rollout_summary_files

- rollout_summaries/2026-08-08T11-59-26-9Qt6-veeam_backup_status_v1_1_14_partial_deployment.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\08\rollout-2026-08-08T13-59-26-019fe13e-07a5-70a1-9aa7-3e8293392104.jsonl, updated_at=2026-08-08T15:06:53+00:00, thread_id=019fe13e-07a5-70a1-9aa7-3e8293392104, SRV-13/SRV-16 deployed; SRV-12 blocked by SSH access)

### keywords

- v1.1.14, 69d6060, SRV-13, SRV-16, SRV-12, kermaria-webportal, kermaria_ai_admin, /backups, 404, Kermaria Veeam Backup Collector

- Related skill: skills/kermaria-srv12-srv13-runtime-deploy/SKILL.md

## User preferences

- when modifying the backup flow, the user required a full analysis of the existing project and reuse of `Customer`/`Service`/tickets models -> inspect the real architecture before introducing abstractions or parallel workflows. [Task 1]
- after an initial SRV-13-based test, the user corrected the business perimeter to KoXoDATA -> never associate an available Veeam job merely for convenience; confirm that it represents the service actually sold to the client. [Task 1]
- the user accepted `Sauvegarde des donnees metier KoXo` without internal details -> keep hostnames, SMB paths, repositories, and technical errors on administration-only surfaces. [Task 1]

## Reusable knowledge

- Kermaria flow: Veeam collector -> private API -> MariaDB -> webportal. `backup_jobs` stores current status and `backup_runs` stores history; the stable Veeam session key makes ingestion idempotent. The portal reads the database, never Veeam directly. [Task 1]
- Reuse the existing support-request workflow for restore requests with customer/service linkage and repository-side `customer_id` checks. Ingestion requires `X-Service-Auth`; admin routes additionally require an authenticated portal session. [Task 1]
- Validated test mapping: `CLI-XS6GCP` -> `Sauvegarde des donnees metier KoXo` -> Veeam job `KoXoDATA`. It reported `protected`, `success`, 77 seconds, seven-day retention, and `0` bytes; do not turn a zero/missing source volume into a positive business metric. [Task 1]
- Release v1.1.14: `69d6060`, tag `v1.1.14`, migration `044_veeam_backup_status` applied and API running on SRV-13. SRV-16 collector lives in `C:\ProgramData\Kermaria\VeeamCollector`; scheduled task `Kermaria Veeam Backup Collector` runs as SYSTEM every 30 minutes and logged `jobs=11 envoyes=11 echecs=0`. `npm run validate` and `npm run test:backups` passed. [Task 1][Task 2]
- The prepared SRV-12 archive is `C:\Users\zhounsah\Documents\Dev\_artifacts\kermaria-webportal-v1.1.14.tar.gz`, built from `69d60600d3fb05f2336b8519c83c54c7b86e4037`; it includes `/backups`, `/backups/[id]`, and `/admin/backups`. [Task 2]

## Failures and how to do differently

- symptom: a technically valid job mapping produces the wrong customer status -> cause: the selected Veeam job does not match the real sold service -> fix: remove stale state and recollect after the business mapping is corrected. [Task 1]
- symptom: an admin endpoint returns unauthorized despite `X-Service-Auth` -> cause: admin routes also require a portal admin session -> fix: test with both required authentication layers. [Task 1]
- symptom: API and collector are deployed but public `/backups` returns `404` and the site shows v1.1.13 -> cause: SRV-12 was not deployed because the local `kermaria_ai_admin` key was refused and no SSH agent key was loaded -> fix: obtain the accepted SRV-12 key, transfer/hash-check the archive, create `apps/webportal/.next/cache` owned by `kermaria-web`, switch `/opt/kermaria/webportal`, restart `kermaria-webportal`, then prove version and `/backups` publicly. Do not call the webportal deployed before those checks. [Task 2]

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

# Task Group: kermaria-client-platform / public offers comparison-table self-service editor

scope: Route public `/offres` comparison-table content requests to the correct Kermaria admin editor without unnecessary source edits or deployment.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse for public-pack presentation/comparison content; distinguish it from billable catalog and pricing administration.

## Task 1: Locate the editable source for the `/offres` comparison table, success

### rollout_summary_files

- rollout_summaries/2026-08-07T15-00-37-x1np-locate_offres_comparison_table_editor.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\07\rollout-2026-08-07T17-00-37-019fdcbd-8a6e-79c0-b0dc-f2f206a25f17.jsonl, updated_at=2026-08-07T15:06:21+00:00, thread_id=019fdcbd-8a6e-79c0-b0dc-f2f206a25f17, user found the desired field)

### keywords

- /offres, /admin/public-pack-catalog, PublicPackComparisonTable, AdminPublicPackCatalogForm.tsx, PATCH /api/admin/public-pack-catalog, /admin/catalog, MariaDB, no-redeploy

## User preferences

- when site content is editable, the user said they wanted to "le modifier moi-même" -> identify the self-service admin route instead of proposing direct code edits. [Task 1]

## Reusable knowledge

- `/offres` renders `PublicPackComparisonTable`; edit labels, order, per-pack values/kinds/text, and add/remove rows in `/admin/public-pack-catalog` through `AdminPublicPackCatalogForm.tsx`. [Task 1]
- These public-presentation edits persist via `PATCH /api/admin/public-pack-catalog` to the API/MariaDB and appear on `/offres` without redeployment. `/admin/catalog` instead owns prices, setup fees, and billable variants. [Task 1]

## Failures and how to do differently

- symptom: a repository-wide search returns too much because many worktrees/releases exist -> fix: start in `kermaria-client-platform/apps/webportal` and use `/admin/public-pack-catalog` as the direct routing handle. [Task 1]

# Task Group: kermaria-client-platform / KoXo production synchronization, AD provisioning, and release 1.0.0.8

scope: Verify the password-set KoXo trigger, update SRV-13 child-domain AD mappings, apply bounded project ACLs, and report the precise release state.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse only for the dedicated SRV-12/SRV-13/SRV-21 Kermaria flow; revalidate live configuration, receiver/log evidence, and Git refs before treating production/release state as current.

## Task 1: Verify the SRV-13-to-SRV-21 KoXo webhook chain, code/config/network verified

### rollout_summary_files

- rollout_summaries/2026-08-02T20-23-15-QVWs-koxo_production_chain_ad_group_mapping_and_release_1008.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\02\rollout-2026-08-02T22-23-15-019fc425-22f2-7dc3-82e6-5f3307cc40ec.jsonl, updated_at=2026-08-10T09:17:20+00:00, thread_id=019fc425-22f2-7dc3-82e6-5f3307cc40ec, full end-to-end replay remains unverified)

### keywords

- KoXo, SRV-21, webhook, 8042, koxo_pending, set-password, SERVICE_AUTH_REQUIRED, SERVICE_AUTH_TOKEN, KOXO_SYNC_TIMEOUT_SECONDS, /Synchro=CLIENTS.xml, Fin de l'opération

- Related skill: skills/kermaria-koxo-webhook-sync/SKILL.md

## Task 2: Remap production provisioning groups to child-domain DNs, success

### rollout_summary_files

- rollout_summaries/2026-08-02T20-23-15-QVWs-koxo_production_chain_ad_group_mapping_and_release_1008.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\02\rollout-2026-08-02T22-23-15-019fc425-22f2-7dc3-82e6-5f3307cc40ec.jsonl, updated_at=2026-08-10T09:17:20+00:00, thread_id=019fc425-22f2-7dc3-82e6-5f3307cc40ec, SRV-13 config/restart/readiness verified)

### keywords

- AD_PROVISIONING_GROUP_DNS, GG_RDS, GG_VPN, GG_NextCloud, AD_ALLOWED_ROOTS, AD_CLIENTS_OU_DN, AD_REQUIRED_OU_ROOT, SubscriptionProvisioningRuntimeConfiguration, controlled_write

## Task 3: Grant only HOME\\zhounsah project-directory access on SRV-13, success

### rollout_summary_files

- rollout_summaries/2026-08-02T20-23-15-QVWs-koxo_production_chain_ad_group_mapping_and_release_1008.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\02\rollout-2026-08-02T22-23-15-019fc425-22f2-7dc3-82e6-5f3307cc40ec.jsonl, updated_at=2026-08-10T09:17:20+00:00, thread_id=019fc425-22f2-7dc3-82e6-5f3307cc40ec, inherited Full Control verified on bounded project roots)

### keywords

- HOME\\zhounsah, icacls, (OI)(CI)(F), C:\apps\api-internal, C:\ProgramData\Kermaria, D:\Kermaria, KoXoExchange

## Task 4: Commit, tag, and reconcile 1.0.0.8 release state, success

### rollout_summary_files

- rollout_summaries/2026-08-02T20-23-15-QVWs-koxo_production_chain_ad_group_mapping_and_release_1008.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\08\02\rollout-2026-08-02T22-23-15-019fc425-22f2-7dc3-82e6-5f3307cc40ec.jsonl, updated_at=2026-08-10T09:17:20+00:00, thread_id=019fc425-22f2-7dc3-82e6-5f3307cc40ec, corrected remote tag and branch containment checked)

### keywords

- 1.0.0.8, 6efa2ec, release: 1.0.0.8, chore/remise-a-plat-agentique, git show-ref --tags, git ls-remote --tags origin, git branch --contains, next-env.d.ts

## User preferences

- when the user asked whether `SRV-13 -> SRV-21` was really usable -> state code, live configuration, network, and real POST evidence separately. [Task 1]
- when the user supplied exact child-domain DNs -> preserve logical aliases `GG_RDS`, `GG_VPN`, and `GG_NextCloud` and change only their runtime DN overrides; identify an appropriate admin settings surface as a future option without claiming one was implemented. [Task 2]
- when the user clarified "Uniquement mon compte utilisateur" -> grant only the named account, preserve SYSTEM/Administrators rights, and inspect the actual project root rather than recursively changing a whole drive. [Task 3]
- when the user asked to publish only "tes modifications de cette conversation" -> stage explicit paths and report remaining worktree edits, tag target, remote refs, and `main` containment separately. [Task 4]

## Reusable knowledge

- Expected path is `webportal SRV-12 -> api-internal SRV-13 -> webhook privé SRV-21 -> PowerShell -> KoXoAdm.exe`; `SignupService` triggers `password_set` only for `koxo_pending` after AD synchronization. [Task 1]
- `KoxoSyncWebhookTriggerService` uses a dedicated bearer-authenticated HTTP client and logs failure without failing password setup. SRV-13 was healthy and TCP to SRV-21:8042 returned `TCP_OK`; no fresh signed POST was replayed. [Task 1]
- The verified architecture is `webportal/SRV-12 -> api-internal/SRV-13 -> private webhook/SRV-21 -> PowerShell -> KoXoAdm`; active AD-linked portal users are exported through private `/api/internal/koxo/users`. `SignupService` invokes `KoxoSyncWebhookTriggerService` with `password_set` after AD provisioning leaves `koxo_pending`. The named client is `koxo-sync-webhook`; failures are logged without failing password setup. [Task 1]
- SRV-13 readiness was HTTP 200 with MariaDB healthy and AD `controlled_write`; live webhook configuration was present (redacted), allowed insecure HTTP, used a 10-second timeout, and SRV-13-to-SRV-21:8042 returned `TCP_OK`. [Task 1]
- Map aliases with `AD_PROVISIONING_GROUP_DNS__GG_RDS`, `__GG_VPN`, and `__GG_NextCloud`. When groups live under `CN=Users` outside the client OU, retain `AD_CLIENTS_OU_DN` exactly once in `AD_ALLOWED_ROOTS`, include both the client OU and group root, and use the common domain root for `AD_REQUIRED_OU_ROOT`; these AD variables are API-only, not SRV-12/webportal settings. [Task 2]
- `HOME\\zhounsah:(OI)(CI)(F)` was verified at `C:\apps\api-internal`, `C:\ProgramData\Kermaria`, `D:\Kermaria`, and `D:\Kermaria\KoXoExchange`; relevant D: project areas are Downloads, KoXoExchange, and Logs. [Task 3]
- Release `1.0.0.8` ultimately pointed to `6efa2ecae11918abad4e390d626626e4b1f6168f` (`release: 1.0.0.8`) on origin, but the commit was on `chore/remise-a-plat-agentique`, not `main`/`origin/main` at the later check. The release staged only `package.json`, `package-lock.json`, and the two KoXo receiver/invocation scripts; `apps/webportal/next-env.d.ts` is generated route-type plumbing, not business logic. [Task 4]

## Failures and how to do differently

- symptom: code/config/TCP is treated as end-to-end proof -> cause: no fresh signed POST was made -> fix: require `202 queued`, receiver/script logs, and fresh KoXo/CSV output before declaring the flow usable. [Task 1]
- symptom: the scheduled-task installer appears periodic -> cause: its `Interval` parameter registers a `-Once` trigger at five minutes -> fix: treat it as one-shot until implementation changes. [Task 1]
- symptom: a same-session check fails after `KermariaApiInternal` restart -> cause: the restart drops the remoting session -> fix: check again from the controller or a fresh remote session. [Task 2]
- symptom: remote `icacls`/ACL work fails or backup is claimed without evidence -> cause: nested quoting and unverified `icacls /save` -> fix: use `& icacls.exe` with native PowerShell argument arrays, then create and separately verify the ACL export. [Task 3]
- symptom: release tag or claimed branch is wrong -> cause: tag propagation/race or unverified containment -> fix: run `git rev-parse <tag>`, `git show <tag>`, `git ls-remote --tags origin <tag>`, `git branch --contains <commit>`, and `git branch -r --contains <commit>` sequentially. [Task 4]

# Task Group: kermaria-client-platform / staged SRV-13 then SRV-12 V1.1 deployment

scope: Verified API-first deployment across SRV-13 Windows/.NET and SRV-12 Ubuntu/Next, with staging swaps, migration/AD checks, and public readiness proof.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse for the dedicated SRV-12/SRV-13 release topology; recheck current archives, hashes, active release, and service state.

## Task 1: Deploy V1.1.0 API then webportal through staging, success

### rollout_summary_files

- rollout_summaries/2026-08-03T16-07-52-YSYY-kermaria_v1_1_0_srv13_srv12_deployment.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\03\rollout-2026-08-03T18-07-52-019fc861-ad2c-7d50-8e67-dbb3143dbd1c.jsonl, updated_at=2026-08-03T16:27:15+00:00, thread_id=019fc861-ad2c-7d50-8e67-dbb3143dbd1c, API-first staged deployment; no demo functional test)
- rollout_summaries/2026-08-03T14-40-24-K3oy-deploy_lot3_v1_1_srv13.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\03\rollout-2026-08-03T16-40-24-019fc811-9938-7450-9881-457a5534818e.jsonl, updated_at=2026-08-03T14:47:21+00:00, thread_id=019fc811-9938-7450-9881-457a5534818e, SRV-13 Lot 3 configuration safeguards)

### keywords

- V1.1.0, SHA256, C:\kmw\out, KermariaApiInternal, 031_backup_policy_public_copy_refresh, AD_ALLOWED_GROUPS, api-internal-staging, kermaria-webportal, MODULE_NOT_FOUND, /opt/kermaria/releases

- Related skill: skills/kermaria-srv12-srv13-runtime-deploy/SKILL.md

## Task 2: Deploy tagged v1.1.13 to SRV-13 then SRV-12, success

### rollout_summary_files

- rollout_summaries/2026-08-07T22-18-54-WYak-zachary_it_diagnostic_configurator_v1_1_13_deploy.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\08\rollout-2026-08-08T00-18-54-019fde4e-cddf-7bd2-af95-2ecd9cba76b0.jsonl, updated_at=2026-08-08T12:26:32+00:00, thread_id=019fde4e-cddf-7bd2-af95-2ecd9cba76b0, API and webportal deployed from the exact tag)

### keywords

- v1.1.13, b7980c4, SRV-13, SRV-12, Plink, KermariaApiInternal, kermaria-webportal, 042_signup_catalog_configuration, 043_fiscal_regime_franchise_base, /diagnostic

## User preferences

- when deploying V1.1.0, the user required SHA verification, staging-only swaps, SRV-13 before SRV-12, migration 031, and no demo-account functional test -> follow that exact technical/deployment boundary. [Task 1]
- when deploying v1.1.13, the user said that uncommitted files came from another Codex session -> preserve the concurrent worktree; build only from the validated tag and do not globally clean, stash, reset, or include unrelated Backup/Veeam files. [Task 2]

## Reusable knowledge

- SRV-13 is Windows/.NET: copy and hash-check the archive on-host, extract to `C:\apps\api-internal-staging`, preserve a timestamped live-directory backup, run the approved migration, start `KermariaApiInternal`, check `/health/ready`, and grep logs for `AD_CONFIGURATION_INVALID` and `AD_TARGET_OUTSIDE_ALLOWED_ROOTS`. [Task 1]
- SRV-12 is Ubuntu/Next: normalize Windows `\\` ZIP paths before extraction, require `apps/webportal/server.js` before switching `/opt/kermaria/webportal`, restart `kermaria-webportal`, wait/retry for `192.168.100.212:3000`, then verify private and public readiness. [Task 1]
- For AD groups, reread the raw `AD_ALLOWED_GROUPS` value and test `-split ','` before restart; the expected config has the clients required root, KoXoAdm/Groupes_TEST allowed roots, `TEST_SITE_WEB`, and the three `GG_DEMO_*` groups. [Task 1]
- For a tagged release, build from the exact tag. SRV-13 uses `KermariaApiInternal` and `http://192.168.100.213:5000/health/ready`; SRV-12 uses `kermaria-webportal`, active symlink `/opt/kermaria/webportal`, and `192.168.100.212:3000`. Transfer a webportal `.tar.gz` containing standalone output, `.next/static`, and `public`; create `.next/cache` owned by `kermaria-web` before restarting systemd. [Task 2]
- v1.1.13 evidence: migrations 042/043 applied via `--apply-migrations` with `ExitCode 0`; SRV-13 readiness HTTP 200 with MariaDB healthy and AD `controlled_write`; SRV-12/public readiness HTTP 200 with configuration/API healthy; `/diagnostic` returned HTTP 200 and showed `Version v1.1.13`. [Task 2]

## Failures and how to do differently

- symptom: remote PowerShell returns no summary or fails on complex inline code -> cause: quoting/policy -> fix: use a temporary local script and independently inspect the host before assuming a deployment changed anything. [Task 1]
- symptom: Linux extraction yields literal Windows paths and `MODULE_NOT_FOUND` -> cause: backslashes and directory entries were not normalized -> fix: normalize separators, handle directories explicitly, then prove `server.js` exists. Generate remote Bash with LF, not CRLF (`set: pipefail: invalid option name`). [Task 1]
- symptom: public site becomes healthy but footer remains `Version v1.0.0.6` -> cause: visible marker was not incremented -> fix: treat that as a separate version-marker follow-up, not evidence that the V1.1 deployment failed. [Task 1]
- symptom: the first curl after restart fails -> cause: the service is not ready yet -> fix: wait and retry before deciding deployment failure, then check the requested public route. [Task 2]
- symptom: remote PowerShell/Plink commands become fragile -> cause: long nested quoting or uncertain SSH tooling -> fix: use an explicitly verified Plink host fingerprint, short commands, and LF remote scripts; the v1.1.13 session did not have usable OpenSSH/Pageant access. [Task 2]

# Task Group: kermaria-client-platform / public backup policy, privacy recovery, and forced release

scope: Public legal/pack content correctness, its persistent managed-content boundary, production privacy recovery, and explicit versioned Git release from a dirty checkout.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse for Kermaria public legal/content and related API release work; recheck database-managed content and current version/tag state.

## Task 1: Align public backup policy and managed content, success

### rollout_summary_files

- rollout_summaries/2026-08-03T12-47-38-wuhQ-kermaria_backup_policy_public_content_harmonization.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\03\rollout-2026-08-03T14-47-38-019fc7aa-5a53-7703-9904-f957b09bf24a.jsonl, updated_at=2026-08-03T13:25:56+00:00, thread_id=019fc7aa-5a53-7703-9904-f957b09bf24a, source/tests validated; production database not changed)

### keywords

- SAVE-PERSO, getPublicPackBackupPolicySummary, managed_content_entries, 031_backup_policy_public_copy_refresh, MANAGED_CONTENT_BACKUP_COPY_UPDATE.sql, 31 jours, /cgv, politique-confidentialite

## Task 2: Restore production privacy page and force 1.0.0.7 release, success

### rollout_summary_files

- rollout_summaries/2026-08-03T12-15-00-EHXJ-kermaria_privacy_fix_and_forced_1_0_0_7_release.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\03\rollout-2026-08-03T14-15-00-019fc78c-7b36-7a20-aa53-c79940df4773.jsonl, updated_at=2026-08-03T15:04:52+00:00, thread_id=019fc78c-7b36-7a20-aa53-c79940df4773, public proof and Git push; full suite not rerun)

### keywords

- legal:politique-confidentialite, ManagedContentService, SeedContent, api-internal-old-20260803-1436-privacyfix, ee79775, 1.0.0.7, v1.0.0.7, chore/remise-a-plat-agentique

## User preferences

- when public policy content changes, the user asked to "inspecter d'abord l'existant", publish no unverified commitment, and finish with files/tests/remaining validations -> inspect first, use a focused diff, and label unknowns. [Task 1]
- the confirmed public backup wording is daily backup, rolling 31-day retention, possible loss since the last successful backup, and points dependent on effective success; do not publish server/topology/RAID/technology details or contradictory 30-day/monthly-restoration text. [Task 1]
- when the user said "1.0.0.7 quand même." and "Commit, tag et push en 1.0.0.7" -> honor the explicit version override while warning it is retrograde; complete both tag variants and branch push. [Task 2]

## Reusable knowledge

- Public packs use `SAVE-PERSO` for backup inclusion; shared presentation text is `getPublicPackBackupPolicySummary` in `packages/shared/src/index.ts`. `/cgv` and `/politique-confidentialite` use `getPublicManagedContent(...)`; persistent `managed_content_entries` overrides SeedContent, so seeds alone do not publish an update. [Task 1]
- Use `031_backup_policy_public_copy_refresh.sql` for guarded `commercial_offers` correction and `docs/MANAGED_CONTENT_BACKUP_COPY_UPDATE.sql` to inspect/update legal content without blind overwrite; production SQL was not applied in this rollout. `test:commercial`, `check:web`, and `git diff --check` passed. [Task 1]
- A missing `legal:politique-confidentialite` in production can be an outdated API deployment: `ManagedContentService` seeds from `AppContext.BaseDirectory\\SeedContent`, copied by `Kermaria.ApiInternal.csproj`. Validate service/readiness plus exact page markers on apex and `www`. [Task 2]
- In a dirty worktree, stage an explicit product-file list and exclude unrelated `.codex/factory/*` and generated `next-env.d.ts`. Before version edits, reread package manifests, `HEAD`, version history, and tags. [Task 2]

## Failures and how to do differently

- symptom: changed seed content does not appear publicly -> cause: an existing database-managed entry wins -> fix: back up/review the target DB and apply only a guarded update. [Task 1]
- symptom: UNC API swap seems complete but active binary is unchanged -> cause: first directory rename did not actually replace live folder -> fix: prove the rename method, then verify directory name, executable timestamp, service status, readiness, and both public URLs. [Task 2]
- symptom: version patch expects 1.0.0.6 -> cause: checkout already advanced to 1.0.0.8 -> fix: reread live manifests immediately before applying the patch; do not claim full tests if only Git push was verified. [Task 2]

# Task Group: Kermaria demo-account product ideation and agent-role boundary

scope: Preserve the user’s stated division between product brainstorming and this agent’s server-deployment/production-operations role.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse as a collaboration boundary, not as an implementation design or validation claim.

## Task 1: Discuss personalized demo accounts, partial

### rollout_summary_files

- rollout_summaries/2026-08-03T08-31-36-WYyJ-comptes_demo_personnalises_et_repartition_claude_deploiement.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\03\rollout-2026-08-03T10-31-36-019fc6bf-f3c1-7723-b218-ed599771ac39.jsonl, updated_at=2026-08-03T08:42:58+00:00, thread_id=019fc6bf-f3c1-7723-b218-ed599771ac39, brainstorming only)

### keywords

- comptes de démonstration personnalisés, Claude, déploiements serveurs, signup, offres, managed content, templates de démonstration

## User preferences

- the user said "je vais le faire avec Claude. Je te réserve pour les déploiements serveurs." -> do not take over product framing by default; prioritize server deployments, production verification, and sensitive operations here. [Task 1]

## Reusable knowledge

- Personalized demo accounts are an unvalidated product idea; reusable templates parameterized by client context are a possible future direction, but current code must be re-examined before implementation. [Task 1]

## Failures and how to do differently

- symptom: brainstorming proposals are treated as adopted requirements -> cause: no implementation or technical validation occurred -> fix: keep demo tiers/fields as exploratory only until the user chooses to resume the work here. [Task 1]

# Task Group: kermaria-client-platform / Stripe test webhook impact diagnosis

scope: Assess a Stripe webhook alert without conflating the application deployment environment with the Stripe account mode.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse for Stripe webhook investigations; recheck current Dashboard endpoints/configuration and do not treat unsigned probing as delivery proof.

## Task 1: Distinguish Stripe test alert from production application impact, partial

### rollout_summary_files

- rollout_summaries/2026-08-03T19-19-40-Ax32-stripe_test_webhook_vs_production_impact.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\03\rollout-2026-08-03T21-19-41-019fc911-488a-7061-9307-03276f7df300.jsonl, updated_at=2026-08-03T19:24:35+00:00, thread_id=019fc911-488a-7061-9307-03276f7df300, alert classified; no signed event retest)

### keywords

- Stripe test, STRIPE_WEBHOOK_SECRET, SIGNATURE_INVALID, Missing Stripe-Signature header, INTERNAL_API_URL, X-Service-Auth, 401, 502, 503, invoice.payment_succeeded

## User preferences

- when the user corrected "Mais on est en production, plus en test..." and then said a Stripe-test-only alert is "pas grave" -> distinguish the Stripe test/live account from the deployed application environment before assessing impact. [Task 1]

## Reusable knowledge

- The public route validates Stripe signature then forwards to `INTERNAL_API_URL/internal/webhooks/stripe` with `X-Service-Auth` and 30-second timeout. `401 SIGNATURE_INVALID` with `Missing Stripe-Signature header` proves public reachability and signature enforcement, not a successful webhook. [Task 1]
- One `STRIPE_WEBHOOK_SECRET` means test and live destinations with different secrets need coherent configuration or code change. The API handles payment/invoice/subscription events idempotently and can replay previously failed events. [Task 1]

## Failures and how to do differently

- symptom: Stripe email about a test account is reported as a production outage -> cause: test/live mode was not separated from application hosting -> fix: first inspect the Dashboard mode and whether a live endpoint exists; a test-only failure limits expected impact to test events/payments. [Task 1]
- symptom: exact cause is claimed from an unsigned probe -> cause: no signed event or secret comparison was performed -> fix: retain the result as partial and verify real signed delivery/config separately, without exposing secrets. [Task 1]

# Task Group: kermaria-client-platform / read-only repository analysis and current technical map

scope: Strictly read-only reconnaissance of the Kermaria checkout, its Git state, architecture, runtime boundaries, toolchain, and validation entrypoints.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for repository analysis only when the user explicitly requests no modification or deployment; recheck current branch, Git state, and versions before treating observed values as current.

## Task 1: Analyse en lecture seule du dépôt Kermaria, success

### rollout_summary_files

- rollout_summaries/2026-08-02T12-05-09-6ZXQ-kermaria_depot_readonly_analysis.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\02\rollout-2026-08-02T14-05-09-019fc25d-1a1e-7c60-b3ec-80b10d4f7d32.jsonl, updated_at=2026-08-02T12:07:02+00:00, thread_id=019fc25d-1a1e-7c60-b3ec-80b10d4f7d32, no files changed and no deployment)

### keywords

- sans modifier aucun fichier, aucun déploiement, chore/remise-a-plat-agentique, Next.js 16.2.9, React 19.2.7, .NET 10.0.301, Node >=24, apps/webportal, apps/api-internal, packages/shared, npm run validate

## User preferences

- when repository analysis is requested, the user said: “sans modifier aucun fichier” and “N’effectue aucune modification et aucun déploiement” -> remain strictly read-only; do not run deployment actions. [Task 1]

## Reusable knowledge

- Architecture: browser -> Next.js/BFF (`apps/webportal`) -> private ASP.NET Core API (`apps/api-internal`) -> MariaDB and internal integrations; the webportal must not access MariaDB, AD, NAS, RDS, VPN, or BPCE directly, while `packages/shared` holds non-sensitive TypeScript contracts. [Task 1]
- Observed toolchain: Next.js 16.2.9, React 19.2.7, TypeScript 6.0.3, ESLint 9.39.4, .NET SDK 10.0.301, `MySqlConnector` 2.6.0, Node `>=24`. Main checks include `lint:webportal`, `typecheck:shared`, `typecheck:webportal`, `build:web`, `build:api`, `test:api`, `check:web`, and `validate`. [Task 1]
- For a read-only state report, separately run `git branch --show-current`, `git log -1 --oneline`, and `git status --short`; the observed branch/dirty state is checkout-specific. [Task 1]

## Failures and how to do differently

- symptom: a PowerShell reconnaissance chain fails at `&&` -> cause: this PowerShell environment does not accept that operator -> fix: execute commands separately or join them with `;`. [Task 1]
- symptom: the `filesystem` MCP server is unavailable -> fix: use PowerShell `Get-Content` and `Get-ChildItem` directly. [Task 1]

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

# Task Group: kermaria-client-platform / production footer version and signup width release

scope: Versioned Kermaria webportal publication, visible footer version, and signup layout fixes that require direct production rendering verification.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse the clean-worktree and SRV-12 release procedure for similar webportal work; recheck current release symlink and live HTML/CSS before calling a visual fix complete.

## Task 1: Deploy v1.0.0.4 with footer version and signup width fix, success

### rollout_summary_files

- rollout_summaries/2026-07-31T13-59-38-wZv6-kermaria_v1004_footer_version_signup_width_deployment.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\31\rollout-2026-07-31T15-59-39-019fb879-34d1-75d2-9ac2-0b541a02944c.jsonl, updated_at=2026-08-01T10:14:46+00:00, thread_id=019fb879-34d1-75d2-9ac2-0b541a02944c, `Version v1.0.0.4` verified on live pages)

### keywords

- v1.0.0.4, PublicShell.tsx, Version v${appPackage.version}, signup-page, max-width: 720px, max-width: 1320px, /opt/kermaria/releases, /opt/kermaria/webportal, kermaria-webportal, DEPLOY_OK

## User preferences

- when the user said “Il manque la version en dans le footer et aussi je ne vois pas la correction…” -> verify the exact requested marker and observable layout on production, not merely a build or healthcheck. [Task 1]

## Reusable knowledge

- Use a clean worktree based on `origin/main` when the main checkout is dirty. On SRV-12, releases live at `/opt/kermaria/releases/<timestamp>` and `/opt/kermaria/webportal` is the active symlink; deploy Next standalone output with `.next/static` and `public`, then restart `kermaria-webportal`. [Task 1]
- `PublicShell.tsx` can import root `package.json` and render `Version v${appPackage.version}`. The signup-wide module was being overridden by global `.signup-page { max-width: 720px; }`; an explicit `.page { max-width: 1320px; width: min(1320px, calc(100vw - 40px)); }` fixed it. [Task 1]

## Failures and how to do differently

- symptom: CSS source changed but the signup remains narrow -> cause: a global selector overrides the module -> fix: inspect live CSS and identify the overriding selector. [Task 1]
- symptom: upload/deploy script fails with `set: pipefail: invalid option name` -> cause: CRLF on Linux script -> fix: use LF. Test TCP/SSH before upload; in PowerShell run commands separately rather than chaining with `&&`. [Task 1]

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

# Task Group: kermaria-client-platform / PayPal/Stripe live configuration and SRV-12/SRV-13 deployment

scope: Live payment-mode configuration and controlled deployment across the dedicated Kermaria edge, Linux webportal, and Windows API hosts; use for configuration/readiness work, not as proof of real payment or webhook processing.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse the topology, config-generation, backup, and readiness procedure for this dedicated SRV-11/12/13 environment; re-check the active host, provider resources, and remote configuration before a later live change.

## Task 1: Switch local PayPal and Stripe runtime configuration to live, success

### rollout_summary_files

- rollout_summaries/2026-07-31T15-48-20-VES0-kermaria_live_deployment_srv12_srv13.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\31\rollout-2026-07-31T17-48-20-019fb8dc-b61e-78a0-bd51-4163a584629c.jsonl, updated_at=2026-08-01T09:31:45+00:00, thread_id=019fb8dc-b61e-78a0-bd51-4163a584629c, local live-mode configuration verified before remote deployment)

### keywords

- PAYPAL_MODE, PAYPAL_WEBHOOK_VERIFY, STRIPE_MODE, STRIPE_WEBHOOK_SECRET, PUBLIC_PORTAL_URL, WEBPORTAL_BASE_URL, dashboard.zacharyhounsa.ovh, kermaria-client-platform.local.env.ps1, build-webportal-config.ps1, build-api-config.ps1

## Task 2: Deploy live configuration to SRV-12 webportal and SRV-13 API, success

### rollout_summary_files

- rollout_summaries/2026-07-31T15-48-20-VES0-kermaria_live_deployment_srv12_srv13.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\31\rollout-2026-07-31T17-48-20-019fb8dc-b61e-78a0-bd51-4163a584629c.jsonl, updated_at=2026-08-01T09:31:45+00:00, thread_id=019fb8dc-b61e-78a0-bd51-4163a584629c, backup, restart, and readiness validation on both hosts)

### keywords

- SRV-11, SRV-12, SRV-13, 192.168.100.212, 192.168.100.213, /etc/kermaria/webportal.env, C:\ProgramData\Kermaria\api-internal.config.json, kermaria-webportal, KermariaApiInternal, Plink, Kerberos, KERMARIA-SRV-13.home.bzh, /api/health/ready, /health/ready

- Related skill: skills/kermaria-srv12-srv13-runtime-deploy/SKILL.md

## Task 3: Verify the one-euro live payment path, uncertain

### rollout_summary_files

- rollout_summaries/2026-07-31T15-48-20-VES0-kermaria_live_deployment_srv12_srv13.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\31\rollout-2026-07-31T17-48-20-019fb8dc-b61e-78a0-bd51-4163a584629c.jsonl, updated_at=2026-08-01T09:31:45+00:00, thread_id=019fb8dc-b61e-78a0-bd51-4163a584629c, no real payment/webhook exercised)

### keywords

- amountCents, 100, createStripeOneShotCheckoutSession, createPayPalOrder, STRIPE_WEBHOOK_SECRET, invoice, webhook

## Task 4: Audit and update live hCaptcha, SMTP, and email allowlist configuration, success

### rollout_summary_files

- rollout_summaries/2026-08-01T09-37-00-bHcq-kermaria_server_config_hcaptcha_smtp_allowlist.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\01\rollout-2026-08-01T11-37-01-019fbcaf-1e2d-7eb0-a3b9-96ffd38b1856.jsonl, updated_at=2026-08-01T10:25:21+00:00, thread_id=019fbcaf-1e2d-7eb0-a3b9-96ffd38b1856, live configuration and actual SMTP delivery verified)

### keywords

- hCaptcha, SMTP_TEST_OK, ssl0.ovh.net, STARTTLS, EMAIL_LIVE_ALLOWLIST_ONLY=false, EMAIL_LIVE_ALLOWLIST=*, SERVICE_AUTH_TOKEN, INTERNAL_API_URL, AD_ALLOWED_UPN_DOMAINS, 192.168.100.212:3000

## User preferences

- when the user asked “Mets les à jour stp.” then “Et maintenant, le déploiement sur les serveurs.” -> once the files and targets are identified, favor direct, targeted operational execution over another theoretical explanation [Task 1][Task 2]

## Reusable knowledge

- The ignored parent file `C:\Users\zhounsah\Documents\Dev\kermaria-client-platform.local.env.ps1` is auto-detected by `scripts/build-webportal-config.ps1` and `scripts/build-api-config.ps1`; set `PAYPAL_MODE=live`, `PAYPAL_WEBHOOK_VERIFY=true`, and `STRIPE_MODE=live` there for this environment. [Task 1]
- The active canonical values for `PUBLIC_PORTAL_URL` and `WEBPORTAL_BASE_URL` were `https://dashboard.zacharyhounsa.ovh`; inspect active server configuration before aligning URLs rather than assuming `portail.zacharyhounsa.ovh`. [Task 1]
- `apps/webportal/lib/paypal.ts` chooses live through `PAYPAL_MODE`; `apps/webportal/lib/stripe.ts` uses `STRIPE_MODE`; `apps/webportal/lib/stripe-webhook.ts` reads one `STRIPE_WEBHOOK_SECRET`, so two Stripe destinations with distinct secrets require one active destination or a code change. [Task 1]
- Dedicated topology: SRV-11 is edge/TLS, SRV-12 (`192.168.100.212`) is Ubuntu/Next webportal on private port 3000 with `kermaria-webportal` and `/etc/kermaria/webportal.env`, and SRV-13 (`192.168.100.213`) is the Windows/.NET API with `KermariaApiInternal` and `C:\ProgramData\Kermaria\api-internal.config.json`. Back up config files before modification, restart the corresponding service, then check both private readiness endpoints and `https://dashboard.zacharyhounsa.ovh/api/health/ready`. [Task 2]
- SSH via Plink needs an explicitly verified host fingerprint; WinRM worked with Kerberos using `KERMARIA-SRV-13.home.bzh`, while IP/Negotiate with TrustedHosts did not. [Task 2]
- Amounts are expressed in cents: a one-euro test is `100`; use a one-shot checkout first, since subscriptions also require live Stripe/PayPal offer resources. [Task 3]
- `SERVICE_AUTH_TOKEN` must match between webportal and API but must never be recorded in clear text; `INTERNAL_API_URL` is webportal server-only. SRV-12 was bound to `192.168.100.212:3000`, so localhost checks were invalid; use `curl -fsS http://192.168.100.212:3000/api/health/ready`. [Task 4]
- SMTP direct test on SRV-13 returned `SMTP_TEST_OK` and the user confirmed delivery. `EMAIL_LIVE_ALLOWLIST_ONLY=false` disables recipient filtering; `EMAIL_LIVE_ALLOWLIST=*` is decorative while that guardrail is off. Mail configuration is API/SRV-13 responsibility. [Task 4]

## Failures and how to do differently

- symptom: a presumed portal URL disagrees with deployed behavior -> cause: local assumptions drifted from the active SRV-12 configuration -> fix: inspect the active configuration and retain the verified canonical host before changing both URL variables. [Task 1]
- symptom: a live Stripe setup has multiple webhook destinations/secrets -> cause: the code accepts only one `STRIPE_WEBHOOK_SECRET` -> fix: keep one matching destination active or extend the webhook verification design before enabling both. [Task 1]
- symptom: SSH/WinRM access fails during the deploy -> cause: an unknown SSH host key or Windows authentication by IP -> fix: supply a verified Plink fingerprint; use the SRV-13 FQDN with Kerberos rather than IP/Negotiate. [Task 2]
- symptom: services/readiness are healthy but the payment rollout is reported as fully proven -> cause: no real PayPal/Stripe transaction or webhook was exercised -> fix: report readiness/config as validated but schedule a controlled functional payment and webhook test. Never retain or re-display conversational secrets; treat exposed values as compromised and rotate them. [Task 1][Task 2][Task 3]
- symptom: SRV-12 change/read command fails through SSH -> cause: OpenSSH did not inherit the user's PuTTY context and complex `sudo`/heredoc quoting is fragile -> fix: use Plink with verified host key and a simple known interactive command/session; back up, restart, then verify port and readiness. The systemd warning for `AD_ALLOWED_UPN_DOMAINS=clients.home.bzh` remains a separate cleanup item. [Task 4]

# Task Group: kermaria-client-platform / V1.0.0 documentation entrypoint and handoff

scope: Current-state-first, repository-wide documentation for Kermaria, with detailed V0.xx traceability and explicit onboarding for humans and AI agents.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse for documentation architecture, version-truth, or repository handoff work in this checkout; re-check tags, manifests, and the current document set before asserting a later release state.

## Task 1: Implement complete Kermaria platform documentation for v1.0.0, partial

### rollout_summary_files

- rollout_summaries/2026-07-31T14-59-40-DZPY-kermaria_v1_0_0_documentation_handoff.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\31\rollout-2026-07-31T16-59-40-019fb8b0-29a9-7e52-875b-4639ff19fe62.jsonl, updated_at=2026-07-31T15:11:00+00:00, thread_id=019fb8b0-29a9-7e52-875b-4639ff19fe62, current-state entrypoint and remaining DATA_MODEL.md gap)

### keywords

- V1.0.0_DOCUMENTATION.md, V1.0.0_FUNCTIONAL_REFERENCE.md, IMPLEMENTATION_MAP_CURRENT.md, DATA_MODEL.md, v1.0.0, README.md, browser -> WEBPORTAL / BFF -> API-INTERNAL -> MariaDB, V0.26_SELF_SERVICE_SIGNUP.md, V0.36_PANIER_UNIFIE_ABONNEMENTS_FACTURES.md

## User preferences

- when scoping documentation, the user chose "Plateforme complète" -> cover public site, client area, admin, BFF, API-INTERNAL, data, operations, security, and integrations by default, not only webportal pages. [Task 1]
- when choosing history, the user selected "Historique détaillé" -> retain V0.xx documents as traceability/context while making current state primary. [Task 1]
- when the user asked for documentation where "une personne physique ou un agent IA" can find answers -> include onboarding order, question-to-file routing, source files, commands, architecture boundaries, and takeover pitfalls. [Task 1]
- when the user approved with "PLEASE IMPLEMENT THIS PLAN" -> create the agreed repository files and cross-links rather than returning recommendations only. [Task 1]

## Reusable knowledge

- `docs/V1.0.0_DOCUMENTATION.md` is the canonical entrypoint; `docs/V1.0.0_FUNCTIONAL_REFERENCE.md` covers public/client/admin journeys, signup, payments/subscriptions, managed content, downloads, AD/KoXo, and feature-to-history mapping. [Task 1]
- Version truth for this release is Git tag `v1.0.0` at `7d473480e697cd72c05e56d63c212ebc997f59d7`, plus repository state; root and `apps/webportal/package.json` still say `0.1.0`, so manifests alone are not authoritative. [Task 1]
- Preserve the architecture boundary `browser -> WEBPORTAL / BFF -> API-INTERNAL -> MariaDB`; WEBPORTAL must not directly access MariaDB, AD, NAS, RDS, VPN, or BPCE. [Task 1]
- Navigation was added to `README.md`, `docs/IMPLEMENTATION_MAP_CURRENT.md`, `ARCHITECTURE.md`, `API_CONTRACT.md`, `DEPLOYMENT.md`, `OPERATIONS.md`, `SECURITY.md`, and `ROADMAP.md`; `git diff --check` and the targeted Markdown-link check passed. [Task 1]

## Failures and how to do differently

- symptom: a broad patch to `DATA_MODEL.md` does not apply -> cause: legacy/mismatched encoding makes literal context unreliable -> fix: use encoding-aware PowerShell/.NET rewriting or line-index insertion. [Task 1]
- symptom: the 1.0.0 documentation appears fully cross-linked -> cause: `DATA_MODEL.md` did not receive its direct 1.0.0 navigation banner -> fix: treat that banner as the remaining follow-up gap. [Task 1]
- symptom: documentation work includes unrelated functional changes -> cause: the starting worktree is dirty -> fix: stage/review only the explicit documentation file list. [Task 1]

# Task Group: kermaria-client-platform / signup layout release v0.40.0.1

scope: Professional French signup layout, BFF contract validation, and isolated publication of the signup lot to `main` from a dirty Kermaria checkout.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform (rollout cwd=C:\Users\zhounsah\Documents\Dev); reuse_rule=reuse the signup behavior and clean-worktree release procedure for similar Kermaria signup changes; re-check branch, base, dependencies, and remote refs before a later publication.

## Task 1: Reorganize signup layout and publish v0.40.0.1 on main, success

### rollout_summary_files

- rollout_summaries/2026-07-31T07-38-46-jBin-signup_v04001_publish_main.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\31\rollout-2026-07-31T09-38-46-019fb71c-7f98-7e42-8633-b7323fb10cef.jsonl, updated_at=2026-07-31T14:14:32+00:00, thread_id=019fb71c-7f98-7e42-8633-b7323fb10cef, validated isolated publication on main)

### keywords

- SignupForm.tsx, apps/webportal/app/api/signup/route.ts, userSize, customerType, Raison sociale, npm run test:signup, npm run typecheck:webportal, npm ci, git worktree, cherry-pick, index.lock, v0.40.0.1, 1e31315, origin/main

## User preferences

- when changing French signup copy, the user insisted on accents and a professional presentation -> retain accents and avoid technical or provisional labels such as "Bloc gauche / Bloc droit". [Task 1]
- when specifying the layout, the user wanted structure information and "Votre besoin" on the left, personal information on the right; for an individual hide "Raison sociale", while association/pro shows a user-count range. [Task 1]
- when requesting publication, the user wanted "commit, tag et push" and corrected the target with "Non met le dans le main." -> verify the target branch explicitly and publish on `main` when requested. [Task 1]
- when the worktree is mixed, publish only the requested file lot and leave unrelated changes untouched. [Task 1]

## Reusable knowledge

- The signup lot is `apps/webportal/components/SignupForm.tsx`, `apps/webportal/app/api/signup/route.ts`, `apps/webportal/app/signup/page.tsx`, and `apps/webportal/app/globals.css`. For `customerType === "individual"`, hide both company name and user-size; for professional/association, user-size is required and passed through BFF validation/message handling. [Task 1]
- In a fresh worktree, run `npm ci` before `npm run test:signup` and `npm run typecheck:webportal`. The recorded final checks passed. [Task 1]
- The isolated final release was commit `1e3131507875546cdb3cc2d6ecf7a9d626ee5f0e` (`fix(webportal): polish signup form layout`); `origin/main` and tag `v0.40.0.1` pointed to it at validation time. [Task 1]

## Failures and how to do differently

- symptom: a dirty/divergent local main risks an unrelated release -> cause: mixed changes and competing Git bases -> fix: create a clean worktree from the exact target base and stage only the allowed files; never use `git add .`. [Task 1]
- symptom: a cherry-pick seems resolved but TypeScript fails -> cause: residual `<<<<<<<`/`>>>>>>>` conflict markers in `route.ts` or `SignupForm.tsx` -> fix: search for markers, resolve, then rerun signup tests and typecheck before commit/push. [Task 1]
- symptom: amend is blocked by `index.lock` -> cause: an active Git process or orphaned lock -> fix: verify Git processes and the lock state before retrying. [Task 1]

# Task Group: kermaria-client-platform / V0.40 KoXo integration reconnaissance

scope: Repository-first analysis and safe preparation for the unfinished V0.40 KoXo synchronization chain; use before resuming V0.40 implementation.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for V0.40 KoXo/signup/AD work in this checkout; treat it as analysis only until implementation and test evidence exists.

## Task 1: Analyze V0.40 KoXo integration before modification, partial

### rollout_summary_files

- rollout_summaries/2026-07-30T19-33-09-f64U-v040_koxo_analysis_and_portfolio_repair.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\30\rollout-2026-07-30T21-33-09-019fb484-2f44-7fe3-a3d1-2c9befc678d0.jsonl, updated_at=2026-07-31T07:23:55+00:00, thread_id=019fb484-2f44-7fe3-a3d1-2c9befc678d0, reconnaissance only; V0.40 was not delivered)

### keywords

- V0.40, docs/v0.40/PROMPT.MD, KoXo, X-Service-Auth, ServiceAuthenticationMiddleware.cs, SignupService.cs, MariaDbSignupRepository.cs, 034_v038_identity_alignment.sql, SERVICE_AUTH_TOKEN, scheduled task

## User preferences

- when beginning V0.40 work, the user required: `Analyse ensuite tout le dépôt avant toute modification` and a final report covering files, architecture, environment variables, tests, results, remaining configuration, and risks -> inspect the full governing spec and relevant code paths before editing. [Task 1]
- when implementing this KoXo lot, the user explicitly prohibited production deployment, real secrets, real data changes, and creation of the real scheduled task -> use mocks, temporary directories, dry-run behavior, and documentation unless later authorized. [Task 1]

## Reusable knowledge

- Start from `docs/v0.40/PROMPT.MD`, then inspect `docs/IMPLEMENTATION_MAP_CURRENT.md`, the V0.38 KoXo/AD documents, signup code, migration `034_v038_identity_alignment.sql`, environment example, internal authentication middleware, admin signup UI, and existing tests. [Task 1]
- Main implementation entry points are `apps/api-internal/Program.cs`, `Services/SignupService.cs`, `Data/Repositories/MariaDbSignupRepository.cs`, `Infrastructure/ServiceAuthenticationMiddleware.cs`, `apps/webportal/lib/internal-api.ts`, and admin signup pages/components. [Task 1]
- This 2026-07 V0.40 analysis predates the 2026-08 SRV-21 production webhook evidence. Kermaria remains Kermaria-first with AD creation at `set-password` and asynchronous KoXo handling, but use the newer `KoXo production webhook synchronization` block for current receiver/API/PowerShell evidence; the earlier scheduled-task chain was not delivered in this rollout. [Task 1]
- Internal API auth is `X-Service-Auth`, with SHA-256 hashing and constant-time comparison; it fails closed outside Development. `.env.example` contains placeholders only and must not receive real credentials. [Task 1]

## Failures and how to do differently

- symptom: V0.40 is described as delivered -> cause: the rollout pivoted to portfolio deployment troubleshooting before implementation or the requested test report -> fix: treat this evidence as analysis/progress and resume from the governing spec with a new implementation and verification run. [Task 1]
- symptom: broad search is noisy or fails under PowerShell -> cause: wildcard syntax/large `rg` scope -> fix: use explicit paths or PowerShell-compatible patterns. [Task 1]

# Task Group: kermaria-client-platform / portfolio host and static-asset deployment

scope: Diagnose and repair `portfolio.zacharyhounsa.ovh` across the SRV-11 Nginx edge and SRV-12 Next standalone release, including static PDF routing.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for this production portfolio topology; recheck live vhost, certificate, release symlink, and asset availability before assuming the public state persists.

## Task 1: Restore portfolio host, conducteur.pdf, and public-route handling, partial

### rollout_summary_files

- rollout_summaries/2026-07-30T19-33-09-f64U-v040_koxo_analysis_and_portfolio_repair.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\30\rollout-2026-07-30T21-33-09-019fb484-2f44-7fe3-a3d1-2c9befc678d0.jsonl, updated_at=2026-07-31T07:23:55+00:00, thread_id=019fb484-2f44-7fe3-a3d1-2c9befc678d0, portfolio home and conducteur.pdf verified; script.pdf unresolved)

### keywords

- portfolio.zacharyhounsa.ovh, SRV-11, SRV-12, Nginx, /portfolio/, public-route-config.ts, conducteur.pdf, script.pdf, /opt/kermaria/releases, kermaria-webportal, WEBPORTAL_SRV12_DEPLOYMENT.md

## Reusable knowledge

- TLS/Nginx terminates on SRV-11 (`192.168.100.211`); the Next standalone webportal runs on SRV-12 (`192.168.100.212:3000`). The portfolio host needs its own Nginx vhost mapping `/` to `/portfolio/index.html` and other paths to `/portfolio/`. [Task 1]
- Portfolio source assets live in `apps/webportal/public/portfolio/`. Deployment requires both the file in the rebuilt release and `"/portfolio"` in `apps/webportal/lib/public-route-config.ts`; copying a PDF into the active release alone is insufficient. [Task 1]
- The verified release was `/opt/kermaria/releases/20260731-072334-portfolio-fix`, activated through `/opt/kermaria/webportal` with systemd service `kermaria-webportal`. `conducteur.pdf`, portfolio home, and `skills.html` returned 200; `conducteur.pdf` had `Content-Type: application/pdf`. [Task 1]
- `projects/radio-saint-vincent.html` links to `../conducteur.pdf` and `../script.pdf`; `docs/WEBPORTAL_SRV12_DEPLOYMENT.md` is the deployment/rollback runbook. [Task 1]

## Failures and how to do differently

- symptom: host serves the main portal or an application 404 -> cause: `portfolio.zacharyhounsa.ovh` is absent from the SRV-11 Nginx vhost -> fix: add and validate the dedicated vhost, then reload only after `nginx -t`. [Task 1]
- symptom: remote Nginx/certbot commands break -> cause: quoting or CRLF line endings -> fix: upload LF-encoded scripts, keep remote scripts simple, and make explicit backups. [Task 1]
- symptom: `script.pdf` is requested but unavailable -> cause: it was absent from repo, Git history, worktrees, SRV-11, SRV-12, and user-profile search -> fix: do not invent or substitute it; request the original file. [Task 1]

# Task Group: kermaria-r740xd-automation / dedicated VM deployment and Zabbix health monitoring

scope: Implemented SRV-11/12/13 dedicated-VM topology, validation evidence, and idempotent Zabbix application monitoring.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev; reuse_rule=reuse for the Kermaria R740xd automation repository and this SRV-11/12/13 topology; revalidate live VM, Zabbix, and deferred-scope status before further changes.

## Task 1: Implement and validate SRV-11/12/13 deployment and monitoring, success

### rollout_summary_files

- rollout_summaries/2026-07-29T10-09-54-QAma-r740xd_srv11_12_13_deployment_zabbix_and_local_secret_update.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\29\rollout-2026-07-29T12-09-54-019fad5a-2650-7e90-982b-8c1b5e1af2ee.jsonl, updated_at=2026-07-30T16:25:41+00:00, thread_id=019fad5a-2650-7e90-982b-8c1b5e1af2ee, validated deployment and monitoring)

### keywords

- R740xd, SRV-11, SRV-12, SRV-13, 192.168.100.211, 192.168.100.212, 192.168.100.213, Zabbix, Set-KermariaHealthMonitoring.ps1, verify-linux-phase2.py, web.page.regexp, expandExpression, 89a6617

## User preferences

- when the user says `PLEASE IMPLEMENT THIS PLAN`, provide the agreed repo-backed runbooks/scripts and validation, not architecture discussion alone. [Task 1]
- for this approved lot, Veeam, KoXo, Windows firewall hardening, DSC/GPO, and secret rotation were deliberately deferred -> preserve those boundaries unless the user reopens them. [Task 1]

## Reusable knowledge

- Dedicated topology: SRV-11 is Ubuntu/nginx/TLS/public entry (`192.168.100.211`), SRV-12 is Ubuntu/Node 24/Next standalone (`192.168.100.212`), and SRV-13 is Windows/.NET API (`192.168.100.213`). [Task 1]
- SRV-12:3000 remains restricted to SRV-11. Monitor it locally through Zabbix Agent 2 `web.page.regexp` rather than opening port 3000 to SRV-10. [Task 1]
- `scripts/r740xd-vm/phase2/zabbix/Set-KermariaHealthMonitoring.ps1` is audit-by-default and applies only with explicit `-Apply`; after fixes, the six monitored objects were conformant. [Task 1]
- Evidence included Linux verifier PASS, 21/21 SRV-13 desired-state checks, public health HTTP 200/`healthy`, `web.test.fail=0`, local SRV-12 `healthy`, no active Zabbix problems, running VMs with automatic start/no checkpoints on SRV-01, and no registered restore-test VM on SRV-02. Monitoring commit: `89a6617`; earlier restore commit: `4627034`. [Task 1]

## Failures and how to do differently

- symptom: PowerShell monitoring audit errors when assigning `$host` -> cause: collision with read-only automatic `$Host` -> fix: use another variable name. [Task 1]
- symptom: idempotence check reports differing Zabbix triggers -> cause: Zabbix canonicalizes expressions -> fix: query/compare `expandExpression=$true`. [Task 1]
- symptom: a large Markdown patch fails -> cause: encoding-sensitive context -> fix: use smaller targeted patches. [Task 1]
- symptom: deployment summary overstates the completed lot -> cause: deferred plan items are mixed with verified state -> fix: explicitly separate live validation from Veeam/KoXo/hardening/DSC/GPO/secret-rotation scope left deferred. [Task 1]

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

# Task Group: Graphify/Codex and kermaria-client-platform repository exploration

scope: Installing and using Graphify from Codex/Windows to map the Kermaria repository, troubleshoot graph persistence, and trace the webportal BFF signup path into API-INTERNAL.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Graphify-based Kermaria code exploration on this Windows/Codex setup; recheck installed version, PATH, and graph artifact existence before treating prior output as current.

## Task 1: Install Graphify CLI and Codex integration, success

### rollout_summary_files

- rollout_summaries/2026-07-30T05-38-16-tRYg-graphify_codex_kermaria_code_only_workflow.md (cwd=\\?\C:\Users\zhounsah\Documents\Codex\2026-07-30\heyy-tu-peux-installer-graphify-sur, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\30\rollout-2026-07-30T07-38-16-019fb187-d4cc-7a20-af82-8695c4d43674.jsonl, updated_at=2026-07-30T06:13:12+00:00, thread_id=019fb187-d4cc-7a20-af82-8695c4d43674, Windows/Codex installation)

### keywords

- graphifyy, graphify 0.9.30, graphify install --platform codex, Python314\\Scripts, PATH, multi_agent, C:\\Users\\zhounsah\\.codex\\skills\\graphify\\SKILL.md

## Task 2: Build and query a Kermaria repository graph, partial then success

### rollout_summary_files

- rollout_summaries/2026-07-30T06-13-58-fEYE-graphify_kermaria_signup_flow.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\30\rollout-2026-07-30T08-13-58-019fb1a8-835f-73f1-95a0-1fd8611a8ab8.jsonl, updated_at=2026-07-30T06:40:19+00:00, thread_id=019fb1a8-835f-73f1-95a0-1fd8611a8ab8, full-repository graph and reliable alternate output)
- rollout_summaries/2026-07-30T05-38-16-tRYg-graphify_codex_kermaria_code_only_workflow.md (cwd=\\?\C:\Users\zhounsah\Documents\Codex\2026-07-30\heyy-tu-peux-installer-graphify-sur, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\30\rollout-2026-07-30T07-38-16-019fb187-d4cc-7a20-af82-8695c4d43674.jsonl, updated_at=2026-07-30T06:13:12+00:00, thread_id=019fb187-d4cc-7a20-af82-8695c4d43674, code-only workflow and graph-loss symptom)

### keywords

- graphify extract . --code-only, graphify cluster-only . --no-label, graphify-out\\graph.json, .codex-tmp\\gfout, tree-sitter-sql, BrokenProcessPool, parallel=False, --budget 5000, graph file not found

## Task 3: Trace webportal signup through API-INTERNAL SignupService, success

### rollout_summary_files

- rollout_summaries/2026-07-30T06-13-58-fEYE-graphify_kermaria_signup_flow.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\30\rollout-2026-07-30T08-13-58-019fb1a8-835f-73f1-95a0-1fd8611a8ab8.jsonl, updated_at=2026-07-30T06:40:19+00:00, thread_id=019fb1a8-835f-73f1-95a0-1fd8611a8ab8, code-backed BFF/API path)
- rollout_summaries/2026-07-30T05-38-16-tRYg-graphify_codex_kermaria_code_only_workflow.md (cwd=\\?\C:\Users\zhounsah\Documents\Codex\2026-07-30\heyy-tu-peux-installer-graphify-sur, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\30\rollout-2026-07-30T07-38-16-019fb187-d4cc-7a20-af82-8695c4d43674.jsonl, updated_at=2026-07-30T06:13:12+00:00, thread_id=019fb187-d4cc-7a20-af82-8695c4d43674, corroborating route/helper trace)

### keywords

- signup/route.ts, callInternalSignup(), INTERNAL_API_URL, getInternalServiceHeaders(), /internal/signup, ISignupService, SignupService.SubmitAsync, ProvisionActiveDirectoryAsync, WEBPORTAL/BFF, MariaDB

## Task 4: Configure Graphify fast-path and the `éco token` trigger, success

### rollout_summary_files

- rollout_summaries/2026-08-01T09-07-11-ZSJV-graphify_token_saving_trigger_kermaria.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\01\rollout-2026-08-01T11-07-11-019fbc93-cea2-78f1-8cd8-690ca9fb170f.jsonl, updated_at=2026-08-01T09:16:06+00:00, thread_id=019fbc93-cea2-78f1-8cd8-690ca9fb170f, existing graph exposed through a junction)

### keywords

- éco token, eco token, AGENTS.md, graphify-out, gfout, junction, graph.json, 4617 nodes, 13738 edges, .git\\info\\exclude

## Task 5: Identify implemented Kermaria payment methods, uncertain

### rollout_summary_files

- rollout_summaries/2026-08-01T09-07-11-ZSJV-graphify_token_saving_trigger_kermaria.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\01\rollout-2026-08-01T11-07-11-019fbc93-cea2-78f1-8cd8-690ca9fb170f.jsonl, updated_at=2026-08-01T09:16:06+00:00, thread_id=019fbc93-cea2-78f1-8cd8-690ca9fb170f, code-backed payment inventory)

### keywords

- createStripeOneShotCheckoutSession, createStripeSubscriptionCheckoutSession, createPayPalOrder, createPayPalSubscription, paymentMethod: "manual", mark-as-paid, "paypal" | "stripe" | "billing"

## User preferences

- when using Graphify, the user wants it “pour économiser des tokens” -> prefer local/AST `--code-only` extraction and avoid document LLM backends unless they are necessary [Task 1][Task 2]
- when a repository-size warning was raised, the user said “Ouais force sur C:\Users\zhounsah\Documents\Dev\kermaria-client-platform” -> once explicit authorization is given, proceed at that broader scope instead of repeatedly insisting on a narrower folder [Task 2]

## Reusable knowledge

- The July 30 Windows/Codex setup had `graphify 0.9.30`, `graphify install --platform codex`, Python 3.14.5 user scripts at `C:\Users\zhounsah\AppData\Roaming\Python\Python314\Scripts`, and `multi_agent = true` in `C:\Users\zhounsah\.codex\config.toml`; recheck rather than assuming this is permanent. [Task 1]
- The existing graph was exposed at the skill-expected `graphify-out\\graph.json` through a Windows junction to `.codex-tmp\\gfout`, with repository-local `.git\\info\\exclude` preventing graph artifacts from appearing in Git. Prefer the existing graph before rebuilding. `AGENTS.md` makes `éco token`/`eco token` a frugal-workflow trigger: reuse prior artifacts, limit reads/search scope, avoid long recaps, and retain necessary verification. [Task 4]
- Code inspection found Stripe card payments, PayPal, and bank transfer/manual payment. Stripe/PayPal support one-shot and subscription helpers; manual payment is `paymentMethod: "manual"` with admin `mark-as-paid`. [Task 5]
- For Kermaria, run the graph from `C:\Users\zhounsah\Documents\Dev\kermaria-client-platform`, not the parent `Dev` directory. `graphify .` pulled in 129 documents needing an LLM; `graphify extract . --code-only` avoided that, and installing `graphifyy[sql]` added `tree-sitter-sql`. [Task 2]
- Before `query`, `explain`, or `path`, check `graphify-out\\graph.json`. If missing, recreate with `graphify extract . --code-only` and `graphify cluster-only . --no-label`; the successful alternate artifact location was `.codex-tmp\\gfout\\graph.json`. [Task 2]
- The observed signup path is public `POST()` in `apps/webportal/app/api/signup/route.ts` -> `callInternalSignup()` -> service-authenticated `INTERNAL_API_URL/internal/signup` -> `Program.cs` `ISignupService` -> `SignupService.SubmitAsync`. Lifecycle: `SubmitAsync` -> `VerifyEmailAsync` -> admin `ApproveAsync` -> `SetPasswordAsync`; AD provisioning happens at password setup via `ProvisionActiveDirectoryAsync`, not at submission. Keep the `AGENTS.md` boundary: browser -> WEBPORTAL/BFF -> API-INTERNAL -> MariaDB. [Task 3]

## Failures and how to do differently

- symptom: a new terminal cannot find Graphify after installation -> cause: it has not inherited the user PATH -> fix: open a new terminal/restart Codex or use the absolute `Python314\\Scripts\\graphify.exe` path. [Task 1]
- symptom: `error: graph file not found: ...\\graphify-out\\graph.json` -> cause: the graph artifact disappeared, not an invalid query -> fix: verify existence first, rebuild code-only, or use the known `.codex-tmp\\gfout` workaround. [Task 2]
- symptom: extraction invoked from Python stdin emits `BrokenProcessPool` or invalid `<stdin>` -> cause: Windows multiprocessing spawn -> fix: use `parallel=False` or execute a real Python script file. [Task 2]
- symptom: a broad graph query is truncated or `path "callInternalSignup()" "SignupService"` has no path -> cause: graph budgets and AST edges do not reliably cross the HTTP boundary -> fix: use targeted symbols plus `--budget 5000`, then `rg` `signup-server.ts`, `route.ts`, and `Program.cs` to bridge the call manually. [Task 2][Task 3]

# Task Group: kermaria-client-platform / persistent multi-agent factory orchestration

scope: The `.codex/factory` infrastructure that persists phase state, enforces Git/QA gates, and supports autonomous Kermaria work without changing application code.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for this factory on branch `chore/remise-a-plat-agentique` after checking the current repository root, branch, and `.codex/factory/STATE.json`; exact phase/commit state is checkout-specific.

## Task 1: Build the persistent multi-agent factory, success

### rollout_summary_files

- rollout_summaries/2026-07-29T19-48-42-PGx0-persistent_multi_agent_factory_orchestrator.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\29\rollout-2026-07-29T21-48-42-019faf6c-100f-78a1-bdce-ca59db28458e.jsonl, updated_at=2026-07-29T20:12:09+00:00, thread_id=019faf6c-100f-78a1-bdce-ca59db28458e, factory-only infrastructure validated)

### keywords

- .codex/factory, STATE.json, ROADMAP.md, PROCESS.md, HUMAN_GATES.md, check-git-state.ps1, validate-phase.ps1, validate-global.ps1, update-state.ps1, QA_NO_PROGRESS_LIMIT, P04, PortalService, HTTP 403

## Task 2: Execute factory phases P04 through P10 RE_QA, paused at HUMAN_GATE

### rollout_summary_files

- rollout_summaries/2026-07-29T20-15-56-elbS-factory_p04_p10_paused_human_gate.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\29\rollout-2026-07-29T22-15-56-019faf84-fd1f-7e00-a5eb-d6b8df07cf94.jsonl, updated_at=2026-07-30T22:03:01+00:00, thread_id=019faf84-fd1f-7e00-a5eb-d6b8df07cf94, latest factory checkpoint; resume only after reconciliation)

### keywords

- HUMAN_GATE, HG-BUSINESS, P10, RE_QA, P10-CONCURRENT-PUBLIC-CONTENT-20260730, v0.39.1, d756592, v0.40, 391844a, f063e4f, PublicPackCard, CompletePhase, PortalAccessDeniedException

## User preferences

- when creating orchestration infrastructure, the user asked to “ne créer que l’infrastructure de l’usine” and no commit -> keep a strict boundary between factory files and application code; propose, but do not create, the commit [Task 1]
- when using `backup/snapshot-avant-remise-a-plat-2026-07-29`, the user required it to be a reference only, never globally merged or restored -> inspect grouped diffs and reimplement the minimal current-code change [Task 1]
- the user wants autonomous resumption without this conversation and human gates only for indispensable decisions -> persist state, make gates explicit, and automate the sequence after validation [Task 1]

## Reusable knowledge

- The real Git root is `C:\Users\zhounsah\Documents\Dev\kermaria-client-platform`; the parent `C:\Users\zhounsah\Documents\Dev` is not a usable Git root. Start with `git rev-parse --show-toplevel`. [Task 1]
- The factory consists of `.codex/factory/{ROADMAP.md,PROCESS.md,STATE.json,HUMAN_GATES.md,DECISIONS.md,BLOCKERS.md,PHASE_TEMPLATE.md}`, P00–P26 phase definitions, nine roles under `.codex/agents/`, and four PowerShell scripts. Initial validated state was `currentPhase: P04`, `currentStep: PRECHECK`; P04 covers the separate `PortalService` HTTP 403 refusal. [Task 1]
- `STATE.json` is a runtime checkpoint: it may remain modified between phase commits and must not be included in an application commit. The process is `Produire → Vérifier → Corriger → Revérifier → Commit local → Phase suivante`; block after three consecutive no-progress cycles (`QA_NO_PROGRESS_LIMIT`). [Task 1]
- Start with `validate-global.ps1 -FactoryOnly`, then `update-state.ps1 -Action StartPhase`. For a resume, run `check-git-state.ps1 -Mode Resume` first and run `update-state.ps1 -Action Resume` only when `interruption.active` is true. Remote operations (`push`, `merge`, `rebase`, `cherry-pick`, tag, deployment) remain human gates. [Task 1]
- At the July 30 pause, `runStatus=HUMAN_GATE`, `currentPhase=P10`, `currentStep=RE_QA`, blocker `HG-BUSINESS` / `P10-CONCURRENT-PUBLIC-CONTENT-20260730`; 12 of 29 entries were DONE (P00–P09 including P06A, plus P10A). Last factory-validated commit: `f063e4f`; P10 must not be treated as committed. [Task 2]
- `CompletePhase` requires the exact phase commit message, only allowlisted files in the commit, and only `STATE.json` left dirty afterward. P04’s one-line `PortalValidationException` -> `PortalAccessDeniedException` fix passed `npm.cmd run test:api`, `test:workflow`, `build:api`, and `git diff --check`, then committed as `3d2b0df`. [Task 2]
- Before resuming P10, reconcile a clean branch/worktree against separately published `v0.39.1 -> d756592` on `origin/main` and `v0.40 -> 391844a` on `origin/chore/remise-a-plat-agentique`; then prioritize P10–P14 and defer P15–P26 unless their scope remains justified. [Task 2]

## Failures and how to do differently

- symptom: Git inspection returns `fatal: not a git repository` -> cause: commands ran from the parent `Dev` folder -> fix: locate and verify the repository root before inspecting or changing state. [Task 1]
- symptom: exploratory PowerShell searches are truncated or hide errors -> cause: output is too broad and exit codes are not handled -> fix: use targeted queries and explicit PowerShell return-code guards. [Task 1]
- symptom: a factory role lookup fails under `.codex/factory/roles` -> cause: roles actually live under `.codex/agents/` -> fix: use `.codex/agents/`. [Task 2]
- symptom: P10 static checks pass but UI behavior regresses -> cause: state-machine edge cases are not covered -> fix: test unavailable historical upfront selections, “Passer au mensuel”, dynamic prop refresh, and A→B→A override resurrection, not only regex assertions. [Task 2]
- symptom: concurrent edits/tags appear while P10 is paused -> cause: `STATE.json` alone does not capture external Git changes -> fix: do not resume, commit, stash, reset, or restore; inspect HEAD, remote refs, tags, index, worktree, and phase-allowlist overlap before explicit reconciliation. A published tag does not make the factory phase complete. [Task 2]

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

# Task Group: UPS browser delivery management and rescheduling cancellation

scope: Inspecting a signed-in UPS account, distinguishing My Choice from package-specific delivery changes, and attempting a delivery-rescheduling cancellation only with user authorization and a verifiable result.
applies_to: cwd=C:\Users\zhounsah\Documents\Codex\2026-07-23\contexte-je-suis-zachary-hounsa-hounkpa-2; reuse_rule=reuse for UPS browser/account delivery-management tasks; package numbers, delivery status, and account/session state are time-sensitive and must be rechecked.

## Task 1: Inspect UPS My Choice and package rescheduling, success

### rollout_summary_files

- rollout_summaries/2026-07-23T08-07-52-YGbt-ups_delivery_rescheduling_cancellation_failed.md (cwd=\\?\C:\Users\zhounsah\Documents\Codex\2026-07-23\contexte-je-suis-zachary-hounsa-hounkpa-2, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\23\rollout-2026-07-23T10-07-52-019f8e04-46cd-7213-a9bf-a1a78091ac9f.jsonl, updated_at=2026-07-23T08:11:07+00:00, thread_id=019f8e04-46cd-7213-a9bf-a1a78091ac9f, read-only UPS account and tracking verification)

### keywords

- UPS, My Choice, Aucune demande en cours., Demande de reprogrammation de la livraison, Ce colis est retenu et sera livré plus tard., Modifier la livraison, Afficher les détails

## Task 2: Cancel package delivery rescheduling, failed without state change

### rollout_summary_files

- rollout_summaries/2026-07-23T08-07-52-YGbt-ups_delivery_rescheduling_cancellation_failed.md (cwd=\\?\C:\Users\zhounsah\Documents\Codex\2026-07-23\contexte-je-suis-zachary-hounsa-hounkpa-2, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\23\rollout-2026-07-23T10-07-52-019f8e04-46cd-7213-a9bf-a1a78091ac9f.jsonl, updated_at=2026-07-23T08:11:07+00:00, thread_id=019f8e04-46cd-7213-a9bf-a1a78091ac9f, cancellation attempt blocked by UPS client-side rendering errors)

### keywords

- UPS, cancel rescheduling, Tab not found: 2. Existing tabs: none, LoginSettingAPIResponse, expirationText, getByRole('button', { name: 'Suivi', exact: true }), strict mode violation

## User preferences

- when an external action is permanent, the user asked: "demande-moi confirmation avant toute modification définitive" -> inspect and explain the exact side effect first; submit only after explicit confirmation. [Task 1]
- when the user then said "Annule la reprogrammation, il me le faut rapidement mon colis." -> that authorizes the attempt, but report failure honestly and never claim a cancellation without UPS confirmation. [Task 2]

## Reusable knowledge

- UPS My Choice membership and package-specific delivery changes are separate surfaces: an active membership with `Aucune demande en cours.` does not rule out a package-specific `Demande de reprogrammation de la livraison`. Inspect the tracking history as the authority. [Task 1]
- Prefer the already-open authenticated tracking page; its expanded history exposed the retained-package message and `Modifier la livraison` action before later navigation became unstable. [Task 1][Task 2]

## Failures and how to do differently

- symptom: UPS tracking detail renders only a shell and cancellation controls cannot be reached -> cause: the client app failed while reading `LoginSettingAPIResponse` / `expirationText` -> fix: wait for a fresh DOM state and reuse an authenticated open page; do not rely on guessed direct detail URLs or claim success. [Task 2]
- symptom: `getByRole('button', { name: 'Suivi' })` has a strict-mode violation -> cause: four matching buttons -> fix: inspect count, then use `getByRole('button', { name: 'Suivi', exact: true })`. [Task 2]

# Task Group: kermaria-client-platform / canonical client-host routing and SRV-01 webportal deploy

scope: Cross-host client login routing (`www` -> `dashboard` / `administration`) and the associated SRV-01 standalone webportal deployment; use for canonical-host bugs or a quick, explicitly unverified rollout.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for this checkout's `*.zacharyhounsa.ovh` / `*.home.bzh` route mapping and SRV-01 webportal deployment; verify browser behavior and the current dirty-worktree scope before relying on the rollout.

## Task 1: Correct vitrine-to-client host routing after login, success

### rollout_summary_files

- rollout_summaries/2026-07-21T16-16-05-PPyx-kermaria_client_login_host_routing_deploy_srv01.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\21\rollout-2026-07-21T18-16-10-019f8576-88e8-7ff3-bda7-f0226b4e3d21.jsonl, updated_at=2026-07-21T16:31:34+00:00, thread_id=019f8576-88e8-7ff3-bda7-f0226b4e3d21, code fix validated by typecheck/build; browser flow still needs confirmation)

### keywords

- www.zacharyhounsa.ovh, dashboard.zacharyhounsa.ovh, administration.zacharyhounsa.ovh, PortalArea, PORTAL_HOST_MAPPINGS, resolvePortalAreaUrl, resolvePortalRoleUrl, LoginForm, /api/auth/login

## Task 2: Deploy the standalone webportal to SRV-01, partial

### rollout_summary_files

- rollout_summaries/2026-07-21T16-16-05-PPyx-kermaria_client_login_host_routing_deploy_srv01.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\21\rollout-2026-07-21T18-16-10-019f8576-88e8-7ff3-bda7-f0226b4e3d21.jsonl, updated_at=2026-07-21T16:31:34+00:00, thread_id=019f8576-88e8-7ff3-bda7-f0226b4e3d21, service running after a user-requested no-smoke deployment)

### keywords

- KERMARIA-SRV-01.home.bzh, KermariaWebportal, webportal-staging, start-webportal.ps1, .next/static, public, logs, HOME\\svc_api_portal_ad, NSSM, robocopy

## User preferences

- when the user said that `*.zacharyhounsa.ovh` remains canonical but login must land on `dashboard.zacharyhounsa.ovh` -> treat it as cross-zone navigation, not a relative-path routing bug [Task 1]
- when the user said `Déploie le rapidement sans vérification sur SRV-01` -> a no-smoke deployment is acceptable only as an explicit exception; report the missing functional confirmation clearly [Task 2]

## Reusable knowledge

- Validated mapping: `www.zacharyhounsa.ovh` = public, `dashboard.zacharyhounsa.ovh` = client, `administration.zacharyhounsa.ovh` = admin; the corresponding `*.home.bzh` hosts are aliases. Relative `/login` and `/dashboard` retain the current host, so role changes need `resolvePortalAreaUrl()` / `resolvePortalRoleUrl()` or a full navigation. [Task 1]
- The fix centralizes host mapping in `apps/webportal/lib/public-route-config.ts`; canonicalization covers the login page, `LoginForm` post-auth `window.location.assign(...)`, browser submissions to `POST /api/auth/login`, and existing sessions from the public home. `npm run typecheck` in `apps/webportal` and root `npm run build:webportal` passed. [Task 1]
- A standalone deploy must explicitly carry `.next/static`, `public`, `start-webportal.ps1`, and `logs` in addition to the standalone bundle; set `HOME\\svc_api_portal_ad:(OI)(CI)M` on `logs`, swap from staging, and restart NSSM. [Task 2]

## Failures and how to do differently

- symptom: TypeScript rejects `LoginForm` access to the auth result -> cause: `AuthState` is a union -> fix: add the explicit `if (!result.authenticated)` guard before authenticated-only fields. [Task 1]
- symptom: a deploy says `KermariaWebportal` is running but its user outcome is unknown -> cause: no functional browser/HTTP smoke was run, and `robocopy` exit code `1` was not independently inspected -> fix: verify staged files and run `www/login -> dashboard/login -> dashboard/dashboard` plus a post-deploy HTTP smoke when the user allows it. [Task 1][Task 2]
- symptom: a focused routing deploy includes unrelated changes -> cause: the checkout is very dirty and the deploy packages the current webportal tree -> fix: inspect and report deploy scope; use a clean or explicitly staged artifact for isolated releases. [Task 2]

# Task Group: kermaria-client-platform / public webportal UX, pack journey, and local validation

scope: Repo-anchored public-site and customer-journey improvements in `apps/webportal`, plus the exact local validation patterns that worked for pack-aware signup/contact/dashboard flows.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria public-site UX, webportal conversion-flow, pack-selection, signup, or local QA tasks in this checkout; treat exact version labels and touched-file lists as rollout-specific evidence.

## Task 1: Analyse the public site and propose concrete improvements, success

### rollout_summary_files

- rollout_summaries/2026-07-15T23-30-00-oFJ9-kermaria_v039_public_vitrine_tunnel.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\16\rollout-2026-07-16T01-30-05-019f681d-a2ab-7f13-ba91-546ca5ede593.jsonl, updated_at=2026-07-18T07:40:22+00:00, thread_id=019f681d-a2ab-7f13-ba91-546ca5ede593, repo-anchored public-site UX and conversion audit)

### keywords

- apps/webportal, PublicShell, PublicPackCard, PublicPackComparisonTable, contact, signup, /offres, conversion audit, public-route-config, v0.39

## Task 2: Implement the `v0.39` public vitrine and pack-aware tunnel, success

### rollout_summary_files

- rollout_summaries/2026-07-15T23-30-00-oFJ9-kermaria_v039_public_vitrine_tunnel.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\16\rollout-2026-07-16T01-30-05-019f681d-a2ab-7f13-ba91-546ca5ede593.jsonl, updated_at=2026-07-18T07:40:22+00:00, thread_id=019f681d-a2ab-7f13-ba91-546ca5ede593, public-site refactor, pack-aware contact/signup flow, and versioned docs)

### keywords

- V0.39_VITRINE_TUNNEL_PUBLIC.md, PublicPackOverviewGrid, PublicPackSelectionSummary, selectionToContactQueryString, buildSignupPackSnapshot, app/page.tsx, app/offres/page.tsx, app/contact/page.tsx, app/signup/page.tsx

## Task 3: Refactor the pack -> signup -> set-password -> dashboard journey, success

### rollout_summary_files

- rollout_summaries/2026-07-15T22-02-48-4Min-kermaria_webportal_pack_journey_refactor_and_testing_guide.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\16\rollout-2026-07-16T00-02-53-019f67cd-cb66-7563-b3dc-6176f6acb293.jsonl, updated_at=2026-07-18T07:40:04+00:00, thread_id=019f67cd-cb66-7563-b3dc-6176f6acb293, highest-impact journey rewrite and dashboard finalization guidance)

### keywords

- signup, set-password, dashboard, souscrire, panier, HeaderCartDrawer, public-routes.ts, Finaliser mon pack, tsconfig.tsbuildinfo, test:subscriptions

## Task 4: Explain how to test the public flow and pack journey locally, success

### rollout_summary_files

- rollout_summaries/2026-07-15T23-30-00-oFJ9-kermaria_v039_public_vitrine_tunnel.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\16\rollout-2026-07-16T01-30-05-019f681d-a2ab-7f13-ba91-546ca5ede593.jsonl, updated_at=2026-07-18T07:40:22+00:00, thread_id=019f681d-a2ab-7f13-ba91-546ca5ede593, automated and manual test recipe for the public vitrine tunnel)
- rollout_summaries/2026-07-15T22-02-48-4Min-kermaria_webportal_pack_journey_refactor_and_testing_guide.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\16\rollout-2026-07-16T00-02-53-019f67cd-cb66-7563-b3dc-6176f6acb293.jsonl, updated_at=2026-07-18T07:40:04+00:00, thread_id=019f67cd-cb66-7563-b3dc-6176f6acb293, local QA walkthrough for signup, set-password, dashboard, and cart flows)

### keywords

- npm run dev:web, dotnet run --project apps/api-internal/Kermaria.ApiInternal.csproj, INTERNAL_API_URL, PUBLIC_VITRINE_ENABLED, SIGNUP_ENABLED, test:ux, test:signup, test:managed-content, typecheck:webportal

## Task 5: Audit publication V1 du webportal sur localhost, success

### rollout_summary_files

- rollout_summaries/2026-07-19T13-29-24-3qHJ-kermaria_audit_viabilite_v1_localhost.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\19\rollout-2026-07-19T15-29-29-019f7a91-3470-7890-9c14-7e41f94c3397.jsonl, updated_at=2026-07-19T13:37:30+00:00, thread_id=019f7a91-3470-7890-9c14-7e41f94c3397, route-by-route V1 publication audit and persistent Markdown note)

### keywords

- localhost:3000, V1, frontend QA, npm run lint:webportal, npm run typecheck:webportal, npm run build:webportal, X-Robots-Tag, noindex, politique-confidentialite, HeaderCartDrawer.tsx, V1_PUBLICATION_AUDIT_LOCALHOST_2026-07-19.md

## Task 6: Correct French visible text, missing accents, and mojibake across the webportal, partial

### rollout_summary_files

- rollout_summaries/2026-07-19T10-31-36-HCNW-webportal_correction_accents_mojibake.md (cwd=C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\19\rollout-2026-07-19T12-31-41-019f79ee-6e57-7f32-b67d-0eb1d6a39d88.jsonl, updated_at=2026-07-19T10:37:52+00:00, thread_id=019f79ee-6e57-7f32-b67d-0eb1d6a39d88, typecheck and targeted scan passed; browser rendering and patch scope still need review)

### keywords

- accents, mojibake, `Ãƒ`, typecheck:webportal, PublicShell, PublicPackCard, PublicPackComparisonTable, ContactForm, SignupForm, SetPasswordForm, AccessDenied, accountAccess

## User preferences

- when the user asked `en regardant en profondeur mon site, quel fonctionnalité ou parcours il faut modifier ?` -> prioritize a high-impact journey diagnosis from the real codebase, not a generic code review or broad inventory [Task 3]
- when the user asked `Tu proposes quoi comme amélioration pour mon site ?` -> inspect actual routes/components before proposing UX changes; repo-anchored suggestions are more useful than generic marketing advice [Task 1]
- when the user then said `Ça me plaît bien, tu peux implémenter ça en v0.39 ?` -> turn the recommendation into a versioned deliverable lot with implementation and docs, not just a brainstorming note [Task 1][Task 2]
- when the user asks `Comment je peux tester ça ?` -> give exact commands, local startup steps, and a manual browser checklist rather than general validation advice [Task 4]
- when refreshing the public site, the user accepted a code-first home/tunnel implementation -> default to code-backed hero/proof blocks unless they explicitly ask to extend managed content/CMS surfaces [Task 2]
- when the user asks for a note `.md` of elements "peu clairs ou inadequats" and recommended corrections -> audit the actually rendered routes and deliver a route-anchored persistent report, not generic advice [Task 5]
- when the user clarified that the web runtime had crashed -> separate a temporary runtime incident from intrinsic product defects, while retaining it as a preflight requirement [Task 5]
- when the user asked to `parcourir toutes les pages web`, correct faults, and `mettre les accents` because a site without accents looks AI-generated -> cover the visible public routes and visitor journey, then explicitly check for mojibake. [Task 6]

## Reusable knowledge

- The strongest public conversion flow in this repo is the chain `homepage -> /offres -> pack-aware /signup or /contact -> /set-password -> /dashboard -> finaliser mon pack`; improving that story is often higher leverage than isolated page polish [Task 2][Task 3]
- Fast orientation for this task family starts with `docs/IMPLEMENTATION_MAP_CURRENT.md`, `docs/V0.32_PUBLIC_PACKS.md`, and `docs/V0.36_PANIER_UNIFIE_ABONNEMENTS_FACTURES.md`, then the `apps/webportal` route/component files named in the task keywords [Task 1][Task 3]
- The public site already had the right building blocks: `PublicShell`, `PublicPackCard`, `PublicPackComparisonTable`, `ContactForm`, and `SignupForm`; the successful refactor mostly improved ordering, wording, metadata, and pack-context transport instead of changing backend models [Task 1][Task 2]
- `/offres` is a natural cards-first then details-later page because the repo already exposes both a compact pack-card UI and a wide comparison table [Task 1][Task 2]
- Pack context can flow through the tunnel with query-string helpers in `apps/webportal/lib/public-packs.ts`; `selectionToContactQueryString(...)` preserves contact context and `buildSignupPackSnapshot(...)` is the reusable shape for visible pack summaries on contact/signup [Task 2]
- `apps/webportal/lib/public-routes.ts` already keeps `/signup` and `/set-password` public; preserve that when changing the public/customer journey [Task 3]
- A strong local test answer in this repo uses three layers: targeted automated checks, local dev startup, and a manual walkthrough across `/`, `/offres`, `/contact?...`, `/signup?...`, `/set-password?token=...`, `/dashboard`, `/souscrire`, and `/panier` [Task 4]
- Versioned public-site deliveries should update `README.md`, `docs/ROADMAP.md`, `docs/IMPLEMENTATION_MAP_CURRENT.md`, and a dedicated `docs/Vx.y_*.md` file such as `docs/V0.39_VITRINE_TUNNEL_PUBLIC.md` [Task 2]
- The public journey `/offres -> /signup?pack=pack-dossier-securise&commitment=1&payment=monthly` preserves pack, commitment, payment mode, and visible pack/price summary. At 390px it had no horizontal overflow, but the header was dense and the hero CTA sat low in the first viewport. [Task 5]
- A passing `typecheck:webportal` and `build:webportal` do not establish V1-public readiness: the audit found `npm run lint:webportal` failing with 27 errors and 1 warning, global `X-Robots-Tag: noindex, nofollow` in `apps/webportal/next.config.ts`, a privacy-policy placeholder and `[adresse e-mail a completer]`, and internal signup wording (`v0.38`, AD, `clients.home.bzh`). Before an indexable public V1, make robots conditional by route and finish/review legal and customer-facing text. [Task 5]
- For a visible-text pass, begin with `app/page.tsx`, `offres/page.tsx`, `contact/page.tsx`, `signup/page.tsx`, `set-password/page.tsx`, `PublicShell.tsx`, public pack components, `ContactForm.tsx`, `SignupForm.tsx`, and `SetPasswordForm.tsx`. `npm run typecheck:webportal` and a targeted `rg` scan over `apps/webportal/app` and `components` passed after the July 19 correction. [Task 6]

## Failures and how to do differently

- symptom: the first answer sounds like generic site advice -> cause: the audit stayed too abstract and not close enough to the real routes/components -> fix: inspect `apps/webportal` implementation files first and anchor recommendations to what already exists [Task 1]
- symptom: work risks clobbering unrelated in-progress edits -> cause: the Kermaria tree is already dirty in adjacent webportal files -> fix: preserve existing user changes explicitly and avoid any reset-style cleanup [Task 2][Task 3]
- symptom: a broad CSS or UI patch fails to apply cleanly -> cause: file shape drift or encoding-sensitive French text -> fix: switch to smaller targeted patches, or rewrite the affected file when line matching is unreliable [Task 2][Task 3]
- symptom: lint stays red even though the changed flow works -> cause: repo-wide lint includes unrelated pre-existing failures outside the feature scope -> fix: rely on targeted lint/build/contract checks and functional validation for the touched area [Task 2][Task 4]
- symptom: `tsc --noEmit` or webportal typecheck fails on generated validator output -> cause: stale `.next` artifacts and `apps/webportal/tsconfig.tsbuildinfo` -> fix: clear the stale cache file and rerun before treating it as a real regression [Task 3]
- symptom: browser navigation times out although the port listens -> cause: the web runtime crashed or is blocked -> fix: restart it, then preflight short-timeout `GET /`, `/offres`, `/signup`, `/api/health/live`, and `/api/health/ready`; inspect rendered HTML/browser interactions as well as health JSON. Do not infer site readiness from a `200` or `/api/health/live` alone. [Task 5]
- symptom: linguistic replacements break TypeScript identifiers such as `AccessDenied`, `accountAccess`, `isWebhookVerificationEnabled`, or `PublicPackSelectionInput` -> cause: global accent replacement touched code, property names, or filenames -> fix: limit edits to explicit user-visible strings/files, keep identifiers ASCII, typecheck, and review the diff before commit. The pass touched about 48 files and had no browser rendering proof. [Task 6]


# Task Group: kermaria-client-platform / Zachary IT public-vitrine release v0.39.1

scope: Reassuring French public-vitrine messaging, pack-aware signup continuity, and isolated publication of this webportal lot to `main`.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse the messaging and clean-worktree release procedure for similarly scoped Kermaria webportal releases; commit, tag, worktree, and base-branch failures are time-specific and must be rechecked.

## Task 1: Publish Zachary IT public vitrine and signup continuity as v0.39.1, success

### rollout_summary_files

- rollout_summaries/2026-07-30T15-09-06-TG1W-zachary_it_vitrine_v0391_publish.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\30\rollout-2026-07-30T17-09-06-019fb392-6f9d-7493-abf4-70cbe78b5ac8.jsonl, updated_at=2026-07-30T19:30:18+00:00, thread_id=019fb392-6f9d-7493-abf4-70cbe78b5ac8, isolated main publication)

### keywords

- Zachary IT, remote backup, digital emergency folder, PublicPackOverviewGrid, active-offer validation, set-password, test:commercial, test:signup, test:forms, test:managed-content, d756592, v0.39.1, origin/main, Turbopack, node_modules junction

## User preferences

- when refreshing French public messaging, the stated objective required a reassuring, educational, non-alarmist presentation and prohibited unsupported claims -> preserve existing commercial mappings and do not invent certifications, encryption, retention, replication, or infrastructure guarantees [Task 1]
- when the user asked “commit, push et tag dans main” -> perform the actual publication from a coherent validated file set and report exact commit/tag/push references [Task 1]

## Reusable knowledge

- The v0.39.1 lot updated homepage/offers/contact messaging, `PublicPackOverviewGrid`, pack selection/contact handling, active-offer validation, and signup/set-password continuity while preserving prices and technical mappings. Targeted checks passed: `npm run test:commercial`, `npm run test:signup`, `npm run test:forms`, and `npm run test:managed-content`. [Task 1]
- For a dirty branch with unrelated commits, use a clean temporary worktree based on `main`, stage only the explicit release file list, and use a patch tag if the prior version tag already exists. The isolated 19-file lot was published as `d756592` (`feat(webportal): strengthen public vitrine and signup continuity`) and annotated `v0.39.1` on `origin/main`. [Task 1]

## Failures and how to do differently

- symptom: full lint/typecheck/build cannot provide clean proof in an external worktree -> cause: pre-existing main errors, missing Node typings, or Turbopack rejecting an out-of-root `node_modules` junction -> fix: report these limits as base-branch/environment issues, retain targeted contract evidence, and prefer a real external worktree with dependencies inside its filesystem root. [Task 1]
- symptom: the local browser shows an empty catalog -> cause: missing runtime catalog data -> fix: do not claim visual verification of populated pack grids/comparisons from that run. [Task 1]

# Task Group: kermaria-client-platform / signup identity, AD alignment, and KoXo V0.38

scope: Code-backed signup/password identity work, Active Directory alignment, and the V0.38 documentation/implementation path from portal-only behavior to linked AD synchronization.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria signup/AD/KoXo design, docs, migrations, or password recovery in this checkout; distinguish pre-V0.38 current-state evidence from the V0.38 implementation and verify actual AD infrastructure separately.

## Task 1: Verify current signup approval and set-password do not create AD accounts, success

### rollout_summary_files

- rollout_summaries/2026-07-15T09-39-15-IjS5-v038_koxo_ad_r740xd_doc_pack.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\15\rollout-2026-07-15T11-39-20-019f6525-0e5b-73b0-b710-a61dd15d53d5.jsonl, updated_at=2026-07-15T10:23:11+00:00, thread_id=019f6525-0e5b-73b0-b710-a61dd15d53d5, current-state verification before future design)

### keywords

- signup, SetPasswordAsync, MariaDbSignupRepository, LdapActiveDirectoryService, Active Directory, customer_ad_links, portal_user, approval flow, no AD creation

## Task 2: Evaluate KoXo Administrator as post-creation AD administration, success

### rollout_summary_files

- rollout_summaries/2026-07-15T09-39-15-IjS5-v038_koxo_ad_r740xd_doc_pack.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\15\rollout-2026-07-15T11-39-20-019f6525-0e5b-73b0-b710-a61dd15d53d5.jsonl, updated_at=2026-07-15T10:23:11+00:00, thread_id=019f6525-0e5b-73b0-b710-a61dd15d53d5, PDF-backed KoXo integration assessment)

### keywords

- KoXo Administrator, koxoadm_fr.pdf, CSV import, XML response, /Synchro=fichier.xml, /ChangePassword, scheduled task, PowerShell, sAMAccountName, clients.home.bzh

## Task 3: Create the future-facing `docs/v0.38/` pack and README routing, success

### rollout_summary_files

- rollout_summaries/2026-07-15T09-39-15-IjS5-v038_koxo_ad_r740xd_doc_pack.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\15\rollout-2026-07-15T11-39-20-019f6525-0e5b-73b0-b710-a61dd15d53d5.jsonl, updated_at=2026-07-15T10:23:11+00:00, thread_id=019f6525-0e5b-73b0-b710-a61dd15d53d5, multi-file V0.38 dossier for later implementation)

### keywords

- docs/v0.38, README.md, V0.38_KOXO_SIGNUP_INTEGRATION.md, V0.38_KOXO_DATA_CONTRACTS.md, V0.38_KOXO_AUTOMATION_RUNBOOK.md, V0.38_R740XD_CUTOVER_CHECKLIST.md, R740xd, future feature

## Task 4: Capture the chosen V0.38 architecture defaults, success

### rollout_summary_files

- rollout_summaries/2026-07-15T09-39-15-IjS5-v038_koxo_ad_r740xd_doc_pack.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\15\rollout-2026-07-15T11-39-20-019f6525-0e5b-73b0-b710-a61dd15d53d5.jsonl, updated_at=2026-07-15T10:23:11+00:00, thread_id=019f6525-0e5b-73b0-b710-a61dd15d53d5, fixed future architecture choices preserved in docs)

### keywords

- set-password trigger, Kermaria source of truth, KoXo async, CSV/XML + tache planifiee, employeeNumber, sAMAccountName, unique email, multi-user signup, clients.home.bzh, OU web clients

## Task 5: Align site/AD documentation for `clients.home.bzh`, partial

### rollout_summary_files

- rollout_summaries/2026-07-18T10-22-38-J5Qp-kermaria_clients_home_bzh_docs_alignment.md (cwd=C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\18\rollout-2026-07-18T12-22-43-019f74bf-dab8-7863-a272-ad9c089135ed.jsonl, updated_at=2026-07-18T10:34:30+00:00, thread_id=019f74bf-dab8-7863-a272-ad9c089135ed, documentation-only current-vs-target alignment)

### keywords

- clients.home.bzh, OU=10_Customers, AD_DOMAIN, AD_CLIENTS_OU_DN, AD_REQUIRED_OU_ROOT, AD_ALLOWED_ROOTS, ActiveDirectoryPathScope, customer_ad_links, V0.38_SITE_AD_ALIGNMENT.md

## Task 6: Implement admin password recovery and portal-to-AD identity alignment; publish `v0.38`, partial

### rollout_summary_files

- rollout_summaries/2026-07-18T08-59-39-9eee-v038_signup_password_admin_ad_sync_commit_push.md (cwd=C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\18\rollout-2026-07-18T10-59-44-019f7473-e2c8-7551-9f70-3f4495db4b02.jsonl, updated_at=2026-07-19T13:33:40+00:00, thread_id=019f7473-e2c8-7551-9f70-3f4495db4b02, focused V0.38 release; API suite remains unresolved outside the lot)

### keywords

- initialize-password, resend-password-email, PortalPasswordService, /internal/admin/signups/{id}, migration-034, 034_v038_identity_alignment.sql, clients.home.bzh, cd8a6e7, v0.38, test:signup, VerifyDownloadsAsync

## User preferences

- when the user asks whether signup already creates AD automatically and gives a concrete mapping rule, verify the actual code path against that rule instead of inferring intended behavior from surrounding docs [Task 1]
- when shaping identity/signup work, the user kept refining structured fields, unique email per user, and pro/association multi-user cases -> default to explicit implementation-oriented schemas rather than loose contact blobs [Task 1][Task 4]
- when the user says `La première option me plait bien. On peut en faire un .md pour l'intégrer quand le R740xd va arriver. Pose-moi un maximum de questions.` -> switch into clarification-first architecture capture, then produce a durable doc pack rather than jumping to premature code [Task 2][Task 3]
- when the user later says `PLEASE IMPLEMENT THIS PLAN` and asks for `Plusieurs notes Markdown`, execute the agreed dossier as a multi-file pack under `docs/v0.38/`, not a single summary note [Task 3]
- when discussing KoXo, the user's repeated choices make the default clear: Kermaria remains source of truth, KoXo is post-creation/daily administration only, and the automation should be asynchronous `CSV/XML + tache planifiee` [Task 2][Task 4]
- when the user asked for `Initialisation par mes soins` and `Renvoie de mail de réinitialisation de mot de passe` -> expose both recovery modes within the approved-signup detail, where `portal_user` and password state are visible, rather than creating unrelated flows [Task 6]

## Reusable knowledge

- Pre-V0.38 repo truth: signup approval created `customer` + `portal_user` in MariaDB, and `SetPasswordAsync` stored only the portal password; AD creation was a separate admin capability. Keep that chronology when interpreting earlier docs. [Task 1]
- When the question is "does signup already create the AD user?", inspect `SignupService`, `MariaDbSignupRepository`, and only then trace the AD service; that is the fastest route to a code-backed answer [Task 1]
- KoXo manual evidence supports CSV import, user-attribute import, XML response files, command-line options such as `/Synchro=fichier.xml` and `/ChangePassword`, plus PowerShell/VBS/BAT automation; no clear REST/webhook interface was found [Task 2]
- The realistic integration shape supported by the evidence is transactional Kermaria-first AD creation followed by asynchronous KoXo replay/administration via exported files and scheduled jobs [Task 2]
- The V0.38 doc pack already exists at `docs/v0.38/` with a README plus four companion docs, and the repo `README.md` links to that folder as the future handoff entrypoint [Task 3]
- The fixed future defaults captured in the V0.38 pack are: AD creation at `set-password`, continuous portal -> AD password sync, `sAMAccountName` based on initial + 6 surname chars with numeric suffixing on collision, `employeeNumber` = Kermaria customer reference, unique email per portal user, multi-user signup for pro/association, and KoXo automation through `CSV/XML` plus a scheduled task [Task 4]
- `ActiveDirectoryPathScope` expects `OU=<customerReference>,OU=10_Customers,<clientsOuDn>` with `Users`, `Groups`, and `Disabled`; the documentation alignment corrected the old `OU=Customers` wording. The exact production DN, service account, ACLs, and cutover remain infrastructure work, not validated application behavior. [Task 5]
- V0.38 adds `POST /internal/admin/signups/{id}/initialize-password` and `/resend-password-email` with matching protected BFF routes. Reuse the one-shot set-password mechanism by refreshing only the hashed token; never log/persist plaintext passwords or tokens, and retain the 12-character minimum. [Task 6]
- Migration `034_v038_identity_alignment.sql` adds structured signup/customer/portal-user and AD synchronization state. `npm run test:signup` passed with 36 checks and `npm run typecheck:webportal` passed; commit `cd8a6e7` (`feat: align signup password flow with AD identity sync`) and annotated `v0.38` were pushed while unrelated dirty changes stayed out. [Task 6]

## Failures and how to do differently

- symptom: AD-behavior answers drift into assumption-heavy architecture talk -> cause: the current signup flow was not checked first -> fix: verify `SignupService` and `MariaDbSignupRepository` before proposing future changes [Task 1]
- symptom: the first search for the AD implementation misses the real file -> cause: the service lives under `Services/ActiveDirectory/`, not a flatter path guess -> fix: search for `LdapActiveDirectoryService` and confirm the actual file path before citing it [Task 1]
- symptom: PDF analysis stalls in the default Python environment -> cause: `pypdf` or the initial tool path is unavailable there -> fix: use the bundled Codex runtime Python and extract/search text from the PDF [Task 2]
- symptom: future docs accidentally read like implemented behavior -> cause: speculative architecture was not clearly separated from current code truth -> fix: label the pack as future/spec/runbook material and keep current-vs-future distinctions explicit [Task 3][Task 4]
- symptom: the local admin signup page/API fails with `Unknown column 'signup_pending.customer_type'` -> cause: MariaDB lacks migration 034 -> fix: apply `034_v038_identity_alignment.sql` before diagnosing the BFF/UI. [Task 6]
- symptom: `npm run test:api` is described as green for V0.38 -> cause: it built but failed at `tests/api-internal/Program.cs:2786` / `VerifyDownloadsAsync` (`Le portail doit filtrer les téléchargements selon les droits actifs`) -> fix: report this as an unresolved downloads validation outside the signup/password lot; do not claim the full API suite passes. [Task 6]

# Task Group: kermaria-client-platform / catalog-driven provisioning, admin service alignment, and V0.37 release finalization

scope: Post-payment provisioning analysis plus implementation and correction of catalog-driven service projection, admin AD surfaces, migration-history idempotency, and the final commit/tag/push for V0.37.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria provisioning/admin-service/migration-history tasks in this checkout; exact commit hashes, tag names, and temp-artifact paths are release-specific evidence.

## Task 1: Trace the real post-payment provisioning path, success

### rollout_summary_files

- rollout_summaries/2026-07-14T09-07-00-fXiM-catalog_driven_ad_provisioning_admin_fixes_commit_push.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\14\rollout-2026-07-14T11-07-05-019f5fe1-2cca-7d40-9048-7bdcdb87ce90.jsonl, updated_at=2026-07-15T09:22:03+00:00, thread_id=019f5fe1-2cca-7d40-9048-7bdcdb87ce90, code-backed payment-to-provisioning inspection)

### keywords

- InvoiceIssuingService.ConfirmPaymentAsync, BilledSubscriptionPaymentTrigger, SubscriptionProvisioningManager, pending_payment, pending_activation, active, rail=billing, admin subscription detail, reconcile provisioning

## Task 2: Implement catalog-driven service topology and dedicated admin AD surfaces, success

### rollout_summary_files

- rollout_summaries/2026-07-14T09-07-00-fXiM-catalog_driven_ad_provisioning_admin_fixes_commit_push.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\14\rollout-2026-07-14T11-07-05-019f5fe1-2cca-7d40-9048-7bdcdb87ce90.jsonl, updated_at=2026-07-15T09:22:03+00:00, thread_id=019f5fe1-2cca-7d40-9048-7bdcdb87ce90, catalog-driven provisioning topology and admin UX split)

### keywords

- technical_service_references, provisioning_group_sam_account_names, commercial_offers, AdminCatalogOfferForm, CustomerActiveDirectoryAdministrationService, CommercialOfferTopologyService, ClientServiceCatalogService, admin AD page, v0.37

## Task 3: Fix admin customer service projection and `schema_migrations` history for migration 033, success

### rollout_summary_files

- rollout_summaries/2026-07-14T09-07-00-fXiM-catalog_driven_ad_provisioning_admin_fixes_commit_push.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\14\rollout-2026-07-14T11-07-05-019f5fe1-2cca-7d40-9048-7bdcdb87ce90.jsonl, updated_at=2026-07-15T09:22:03+00:00, thread_id=019f5fe1-2cca-7d40-9048-7bdcdb87ce90, correction pass after explicit user acceptance feedback)

### keywords

- AdminService, AdminCustomerDetail, customerId, IClientServiceCatalogService.GetServicesAsync, customer_services, schema_migrations, INSERT IGNORE, MariaDbMigrationRunner, 033_catalog_service_topology.sql

## Task 4: Commit, tag, and push the coherent feature set as `v0.37`, success

### rollout_summary_files

- rollout_summaries/2026-07-14T09-07-00-fXiM-catalog_driven_ad_provisioning_admin_fixes_commit_push.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\14\rollout-2026-07-14T11-07-05-019f5fe1-2cca-7d40-9048-7bdcdb87ce90.jsonl, updated_at=2026-07-15T09:22:03+00:00, thread_id=019f5fe1-2cca-7d40-9048-7bdcdb87ce90, repo finalization and publication proof)

### keywords

- git commit, git tag, git push, 95d5f75, feat: add downloads and catalog-driven AD provisioning, v0.37, origin, .codex-tmp, tmp/backup-avant-reparation-tz-20260709.sql

## Task 5: Correct stale AD provisioning status and publish the focused fix, success

### rollout_summary_files

- rollout_summaries/2026-07-18T10-37-47-e76p-kermaria_ad_provisioning_commit_tag_push.md (cwd=C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\18\rollout-2026-07-18T12-37-52-019f74cd-b938-7de2-85c9-5b51e5314479.jsonl, updated_at=2026-07-19T13:28:24+00:00, thread_id=019f74cd-b938-7de2-85c9-5b51e5314479, targeted four-file AD provisioning correction)

### keywords

- PROVISIONING_SYNCHRONIZED, AD_GROUP_SCOPE_INCOMPATIBLE, SubscriptionProvisioningManager, CustomerActiveDirectoryAdministrationService, AdminCustomerActiveDirectoryWorkbench, 5410, 3101, 7a2c153, ad-provisioning-sync-2026-07-19

## User preferences

- when the user asks `Tu peux me dire où en est le provisionning une fois que le client a payé ?`, answer from the real code path and explain the actual admin surfaces, not a theoretical provisioning design [Task 1]
- when the user asks for `Un bouton pour provisionner l'ensemble et un autre pour chaque utilisateur` and says AD actions should be on `une page séparée`, prefer explicit per-user/bulk controls and split AD operations away from the main customer page [Task 2]
- when the user says `Globalement c'est très bien : juste deux petites corrections`, treat those concrete follow-up points as release-blocking acceptance criteria before calling the feature done [Task 3]
- when the user is interrupted and asks to resume the route/thought process, continue from the current investigation state instead of restarting the whole explanation [Task 3]
- when the user asks `Tu peux commid, tag et push ?`, they want publication, not just local validation -> move from feature work into careful git hygiene and remote push once the worktree is coherent [Task 4]
- when the user asks `Tu peux commit, tag et push ?` from a worktree mixing V0.38, V0.39, and unrelated changes -> isolate the validated paths and use a descriptive tag rather than pretending the full tree is a clean version release. [Task 5]

## Reusable knowledge

- The billed-subscription payment flow converges through `InvoiceIssuingService.ConfirmPaymentAsync(...)`, then `BilledSubscriptionPaymentTrigger.OnDocumentPaidAsync(...)` advances `pending_payment -> pending_activation -> active`, records payment state, and may reconcile provisioning [Task 1]
- `SubscriptionProvisioningManager` is the durable place to inspect provisioning status, retry eligibility, mapped groups, reconciled groups, and target users; the admin subscription detail page already exposes these supervision surfaces [Task 1]
- Catalog-driven service/provisioning topology is now stored on `commercial_offers` via `technical_service_references` and `provisioning_group_sam_account_names`, and the portal-side source of truth is `ClientServiceCatalogService` [Task 2]
- To keep admin and client service lists aligned, `AdminService` should project services through the same calculation path as the portal instead of maintaining a separate `customer_services`-style snapshot [Task 2][Task 3]
- The stable fix for migration-033 history drift is idempotent bookkeeping in both places: `MariaDbMigrationRunner` uses `INSERT IGNORE INTO schema_migrations (...)`, and `033_catalog_service_topology.sql` also writes `INSERT IGNORE INTO schema_migrations (migration_id, applied_at)` for manual-application cases [Task 2][Task 3]
- `AdminCustomerDetail` now includes `customerId`, which lets the admin service build a short-lived projection session and call `IClientServiceCatalogService.GetServicesAsync(...)` without reimplementing service logic [Task 3]
- The coherent release state for this feature set was published as commit `95d5f75` and tag `v0.37`; temp artifacts `.codex-tmp/` and `tmp/backup-avant-reparation-tz-20260709.sql` were intentionally left untracked [Task 4]
- Provisioning summaries must inspect effective AD group memberships, not a stale historical `AD_GROUP_SCOPE_INCOMPATIBLE`; when synchronized, report `PROVISIONING_SYNCHRONIZED` with the effective root and diagnostics. Fresh API `5410` and webportal `3101` showed the corrected French status on both admin surfaces. [Task 5]
- The focused fix was commit `7a2c153` (`fix(ad): align provisioning summary with effective group state`) and annotated tag `ad-provisioning-sync-2026-07-19`; only `SubscriptionProvisioningManager.cs`, `CustomerActiveDirectoryAdministrationService.cs`, the admin subscription page, and `AdminCustomerActiveDirectoryWorkbench.tsx` were included. [Task 5]

## Failures and how to do differently

- symptom: provisioning explanations sound plausible but miss the actual state after payment -> cause: the answer skipped the convergence point in `InvoiceIssuingService` and the billing trigger path -> fix: trace `ConfirmPaymentAsync(...)` -> `BilledSubscriptionPaymentTrigger` -> `SubscriptionProvisioningManager` before summarizing [Task 1]
- symptom: the admin customer page still shows fictitious services after catalog work -> cause: admin logic stayed tied to the old `customer_services` perspective -> fix: reuse the portal calculation path through `ClientServiceCatalogService` instead of patching counts locally [Task 2][Task 3]
- symptom: migration 033 exists in business tables but not in `schema_migrations` -> cause: only the data changes were applied or the runner/manual path mismatch was ignored -> fix: make both the runner write and the SQL migration history insert idempotent with `INSERT IGNORE` [Task 2][Task 3]
- symptom: a normal `dotnet build` fails during validation because the output DLL is locked -> cause: a running `dotnet.exe` keeps `Kermaria.ApiInternal.dll` open -> fix: validate with `-p:OutDir=...` to avoid the locked default output path [Task 2]
- symptom: a final commit risks bundling unrelated local artifacts -> cause: the worktree is already dirty -> fix: stage only the coherent feature set and explicitly exclude temp folders/backups instead of using a blanket `git add .` [Task 4]
- symptom: port `5000` is held by an inaccessible old API process -> cause: stale local runtime -> fix: validate the isolated stack on API `5410` plus webportal `3101` rather than killing an unknown process. [Task 5]

# Task Group: kermaria-client-platform / Windows staging and recette deployment

scope: Remote verification and Windows deployment work for API-INTERNAL and WEBPORTAL on SRV-01/SRV-02, including runtime-layout diagnosis, staging swaps, and health verification.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria server verification and deploy tasks on the SRV-01/SRV-02 topology; treat exact commit hashes, backup folder names, and endpoint states as rollout-specific evidence.

## Task 1: Diagnose SRV-01 runtime layout and verify live health, success

### rollout_summary_files

- rollout_summaries/2026-07-06T16-12-18-vmxx-kermaria_srv01_webportal_staging_health_diagnosis.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\06\rollout-2026-07-06T18-12-18-019f3833-acea-7bc3-bdbd-cbb60234fe9f.jsonl, updated_at=2026-07-06T16:17:55+00:00, thread_id=019f3833-acea-7bc3-bdbd-cbb60234fe9f, runtime layout and on-host health proof)

### keywords

- KERMARIA-SRV-01, Remote PowerShell, C:\ProgramData\Kermaria, C:\apps\webportal, validate:staging, check:health, curl.exe, start-webportal.ps1, webportal.config.json, health/ready

## Task 2: Deploy API-INTERNAL and WEBPORTAL to SRV-02/SRV-01 with rollback-safe swaps, success

### rollout_summary_files

- rollout_summaries/2026-07-13T15-14-03-BKPq-kermaria_srv01_srv02_api_webportal_recette_deploy.md (cwd=C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\13\rollout-2026-07-13T17-14-03-019f5c0a-dc66-7cf0-8ffb-eed61cb84f04.jsonl, updated_at=2026-07-13T15:26:37+00:00, thread_id=019f5c0a-dc66-7cf0-8ffb-eed61cb84f04, latest end-to-end deployment proof)

### keywords

- deployment, SRV-01, SRV-02, KermariaWebportal, KermariaApiInternal, robocopy, Get-SmbOpenFile, Close-SmbOpenFile, start-webportal.ps1, SHA-256, staging swap, health/live, health/ready

## Task 3: Rapid SRV-01 webportal deploy after canonical-host fix, partial

### rollout_summary_files

- rollout_summaries/2026-07-21T16-16-05-PPyx-kermaria_client_login_host_routing_deploy_srv01.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=\\?\C:\Users\zhounsah\.codex\sessions\2026\07\21\rollout-2026-07-21T18-16-10-019f8576-88e8-7ff3-bda7-f0226b4e3d21.jsonl, updated_at=2026-07-21T16:31:34+00:00, thread_id=019f8576-88e8-7ff3-bda7-f0226b4e3d21, requested no-smoke deploy; service running only)

### keywords

- KERMARIA-SRV-01.home.bzh, webportal-staging, KermariaWebportal, server.js, start-webportal.ps1, .next/static, public, logs, HOME\\svc_api_portal_ad, robocopy exit code 1

## Task 4: Publish `v0.39` cart/dashboard changes and deploy API plus webportal, success

### rollout_summary_files

- rollout_summaries/2026-07-21T12-34-25-GxM2-kermaria_set_password_and_v039_deployment.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\21\rollout-2026-07-21T14-34-30-019f84ab-955d-7681-b996-eb13107528f6.jsonl, updated_at=2026-07-21T16:12:27+00:00, thread_id=019f84ab-955d-7681-b996-eb13107528f6, `v0.39` publish, rollback recovery, and health proof)

### keywords

- c470f56, v0.39, npm run test:cart, DOWNLOAD_STORAGE_ROOT, RuntimeConfigurationException, C:\ProgramData\Kermaria\downloads, KermariaApiInternal, KermariaWebportal, health/ready

## User preferences

- when the user said the deployment was on `KERMARIA-SRV-01.home.bzh` and pointed to `C:\ProgramData\Kermaria`, switch to on-host verification quickly instead of reasoning only from the local checkout [Task 1]
- when the continuation objective includes publication and deployment -> finish commit/tag/push, artifact creation, rollback-safe deployment, and health checks rather than stopping at code validation. [Task 4]
- when the user asked `Est-ce que tu peux déployer l'API et le webportail sur les serveurs ?` -> default to end-to-end operational execution: build, transfer, swap, verify, and keep rollback copies, not just a plan [Task 2]
- when the user asked `Déploie le rapidement sans vérification sur SRV-01` -> respect the explicit no-functional-check exception, but keep the missing smoke test visible in handoff. [Task 3]

## Reusable knowledge

- `C:\apps\webportal` on SRV-01 is a runtime artifact root, not the monorepo root; root npm scripts such as `validate:staging` and `check:health` live in `C:\Users\zhounsah\Documents\Dev\kermaria-client-platform\package.json`, while `C:\apps\webportal\apps\webportal\package.json` only contains app-local scripts [Task 1]
- `C:\ProgramData\Kermaria\webportal.config.json` is the runtime config source for the NSSM wrapper, and `C:\apps\webportal\start-webportal.ps1` injects env vars before launching `apps\webportal\server.js` [Task 1]
- Reliable remote health verification in this environment used `curl.exe` against `http://127.0.0.1:3000/api/health/live`, `http://127.0.0.1:3000/api/health/ready`, and `http://192.168.100.202:5000/health*`; `Invoke-WebRequest` was less reliable in the remote PowerShell session [Task 1]
- The current Windows deploy pattern is: build locally, package API and Next.js standalone WEBPORTAL, copy to `C:\apps\api-internal-staging` / `C:\apps\webportal-staging`, stop service, rename live to backup, rename staging to live, restart, then verify readiness and public URLs [Task 2]
- WEBPORTAL standalone deploys must include `.next/standalone`, `.next/static`, `public`, `start-webportal.ps1`, writable `logs\`, and the service-account ACL; a bare standalone copy is not enough [Task 2]
- `Get-FileHash` on `Kermaria.ApiInternal.exe` and `server.js` is a cheap integrity check before or after cutover [Task 2]
- The July 21 standalone payload required separate copies of `.next/static`, `public`, `start-webportal.ps1`, and `logs`; the old directory was retained as `C:\apps\webportal-old-20260721-182619`, the service was `Running`, and `server.js` was active. [Task 3]
- Related skill: skills/kermaria-windows-staging-deploy/SKILL.md [Task 1][Task 2][Task 4]
- Before deploying the V0.37+ API binary, configure persistent `DOWNLOAD_STORAGE_ROOT` (verified value: `C:\ProgramData\Kermaria\downloads`) and grant `HOME\svc_api_portal_ad:(OI)(CI)M`; then verify both services are Running and query API plus public readiness. [Task 4]

## Failures and how to do differently

- symptom: `npm run validate:staging` or `npm run check:health` fails on SRV-01 with `ENOENT` or `Missing script` -> cause: command ran from the runtime artifact root instead of the monorepo root -> fix: distinguish `C:\apps\webportal` from the source checkout before deciding where scripts live [Task 1]
- symptom: remote HTTP inspection fails with a PowerShell null-reference path -> cause: `Invoke-WebRequest` / `Invoke-RestMethod` instability in this setup -> fix: use `curl.exe` for direct readiness/live endpoint checks [Task 1]
- symptom: `Rename-Item` on `C:\apps\webportal-staging` fails with access denied or the service cannot return to `Running` -> cause: lingering `robocopy.exe` and open SMB handles on the staging folder -> fix: restore live, kill lingering `robocopy`, close `Get-SmbOpenFile` handles on `C:\apps\webportal-staging*`, then retry the swap [Task 2]
- symptom: WEBPORTAL swap appears partially broken despite a usable copy -> cause: `robocopy` exit code `1` is informational in this workflow -> fix: treat only `>= 8` as copy failure and continue with package verification and health checks [Task 2]
- symptom: a quick deploy is treated as a verified release -> cause: `robocopy` returned exit code `1` without detailed output and no HTTP/browser smoke followed -> fix: verify staged files and run a smoke test as soon as the exception window ends. [Task 3]
- symptom: API starts after a swap then immediately fails with `RuntimeConfigurationException: Configuration invalide : DOWNLOAD_STORAGE_ROOT` -> cause: V0.37+ download storage config/ACL is absent -> fix: roll back immediately, configure the persistent root with service-account Modify access, retry, then independently verify timestamps, services, backups, and readiness endpoints. [Task 4]

# Task Group: kermaria-client-platform / version inventory and memory concordance

scope: Cross-source version lookup for the Kermaria repo, especially when the user wants repo docs, Git tags, Codex memory, and local Claude memory compared explicitly.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria version-history, doc-truth, or "does version X exist?" work; treat exact tag inventories and Claude-memory paths as time-specific evidence that may need rechecking.

## Task 1: Build cross-source version concordance, success

### rollout_summary_files

- rollout_summaries/2026-07-13T19-40-14-g9qB-kermaria_version_concordance_and_v034_gap_check.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\13\rollout-2026-07-13T21-40-19-019f5cfe-8ea9-7441-8bd0-01cb70f6c515.jsonl, updated_at=2026-07-13T21:09:30+00:00, thread_id=019f5cfe-8ea9-7441-8bd0-01cb70f6c515, repo docs vs tags vs Codex memory vs Claude memory concordance)

### keywords

- version concordance, Git tags, docs/ROADMAP.md, docs/IMPLEMENTATION_MAP_CURRENT.md, MEMORY.md, .claude, V0.33, V0.35, V0.36, tableau

## Task 2: Verify V0.34 is absent in verified sources, success

### rollout_summary_files

- rollout_summaries/2026-07-13T19-40-14-g9qB-kermaria_version_concordance_and_v034_gap_check.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\13\rollout-2026-07-13T21-40-19-019f5cfe-8ea9-7441-8bd0-01cb70f6c515.jsonl, updated_at=2026-07-13T21:09:30+00:00, thread_id=019f5cfe-8ea9-7441-8bd0-01cb70f6c515, clean negative lookup across repo, tags, Codex memory, and Claude memory)

### keywords

- V0.34, v0.34, git tag --list, rg -n, ROADMAP.md, version gap, negative lookup, .claude

## User preferences

- when the user asked for `| version | fonctionnalités rajoutés | tag git | ... |`, they wanted a compact, copy-pastable matrix by default for version work [Task 1]
- when the user said `Regarde dans la mémoire Claude. Peut-être que tu auras un indice.` -> future version-inventory work should corroborate across repo docs, Git tags, Codex memory, and local Claude memory instead of trusting one source [Task 1][Task 2]
- when the user narrowed to `V0.34`, they wanted the gap called out explicitly, not hidden inside a broader concordance [Task 2]

## Reusable knowledge

- `docs/ROADMAP.md` and `docs/IMPLEMENTATION_MAP_CURRENT.md` are the main repo-side version anchors for quick concordance work [Task 1]
- A reliable first pass for Kermaria version inventory is: enumerate `git for-each-ref refs/tags` / `git tag --list`, list `docs/V0*`, then compare against Codex memory and the relevant Claude memory files [Task 1]
- local Claude memory currently reflects the V0.33 managed-content family and the V0.36 checkout/docs family more strongly than the intermediate versions, so it is useful as corroboration but not as a complete version index [Task 1]
- `V0.34` was not found in repo docs, Git tags, Codex memory, or the searched Claude memory; the documented repo sequence jumps from `V0.33` to `V0.35` [Task 2]

## Failures and how to do differently

- symptom: searching `C:\Users\zhounsah\.claude` produces too much noise to answer a version question quickly -> cause: the tree contains many unrelated sessions and project artifacts -> fix: restrict the search to version markers plus known memory files first [Task 1]
- symptom: versions appear inconsistent across docs and tags -> cause: this repo mixes doc-only versions with patch-style or feature-suffixed tags -> fix: present an explicit source-vs-source concordance instead of forcing a false one-to-one mapping [Task 1]
- symptom: the user asks whether a missing version exists anywhere useful -> cause: a concordance table can hide the negative result -> fix: state the negative lookup explicitly as "not found in repo docs, tags, Codex memory, or Claude memory" [Task 2]

# Task Group: kermaria-client-platform / checkout, public packs, and implementation handoff

scope: Current-state documentation and release handoff for public packs, managed commercial presentation, billed recurring checkout, and the unified checkout docs published around V0.36.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria checkout, pack, pricing, and documentation-takeover tasks; treat exact commit/tag values as historical anchors.

## Task 1: Release public packs, managed-content, and billed recurring checkout docs, success

### rollout_summary_files

- rollout_summaries/2026-07-07T08-40-15-D9U0-public_packs_managed_content_billed_recurring_checkout.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T10-40-15-019f3bbc-2cc2-7a92-9735-ab8d1f61d2ac.jsonl, updated_at=2026-07-09T16:14:44+00:00, thread_id=019f3bbc-2cc2-7a92-9735-ab8d1f61d2ac, repo-current-state release docs and validation)

### keywords

- Packs.xlsx, V0.32_PUBLIC_PACKS.md, V0.33_CONTENUS_ADMINISTRABLES.md, V0.36_PANIER_UNIFIE_ABONNEMENTS_FACTURES.md, IMPLEMENTATION_MAP_CURRENT.md, commercial_offers, external_reference, recurring checkout, test:cart, test:payments, v0.36

## Task 2: Publish V0.36 unified checkout and staging-validation handoff, success

### rollout_summary_files

- rollout_summaries/2026-07-08T10-47-55-jU3Y-kermaria_v036_checkout_unifie_docs_staging_handoff.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\08\rollout-2026-07-08T12-47-55-019f4157-697f-7f70-8600-1fa58a8f040e.jsonl, updated_at=2026-07-09T16:15:47+00:00, thread_id=019f4157-697f-7f70-8600-1fa58a8f040e, unified checkout and validation-truth handoff)

### keywords

- V0.36, checkout unifie, recurring_checkout, rail=billing, bank transfer, validate-staging.mjs, controlled_write, V0.24_ANOMALIES.md, v0.36.1, README.md, ROADMAP.md, API_CONTRACT.md

## User preferences

- when the user asked `Avant de coder : Commence par explorer le codebase et le fichier Packs.xlsx` -> start with repo reconnaissance plus source-of-truth workbook inspection before editing pack/checkout behavior [Task 1]
- when the user asked to keep compatibility with the existing provisionnement / back-office / checkout architecture -> preserve the current technical mapping and extend it instead of inventing a parallel model [Task 1]
- when the goal is takeover speed, the user values a concrete entry document and implementation map over chat-only explanations [Task 1][Task 2]
- when a release tag already exists, publish a patch tag instead of moving tag history [Task 2]
- when closing this kind of work, include exact proof artifacts such as validation commands, commit hashes, pushes, and tags [Task 1][Task 2]

## Reusable knowledge

- `Packs.xlsx` already contains the business labels, pricing normalization, and pack structure; it is a reliable truth source for public-pack work [Task 1]
- The current doc set for fast takeover is: `docs/IMPLEMENTATION_MAP_CURRENT.md`, `docs/V0.32_PUBLIC_PACKS.md`, `docs/V0.33_CONTENUS_ADMINISTRABLES.md`, and `docs/V0.36_PANIER_UNIFIE_ABONNEMENTS_FACTURES.md`; `README.md` is the top-level index to them [Task 1]
- Public pack presentation is layered over `commercial_offers`, while stable technical identity and billing behavior are keyed off `external_reference` and the existing subscription/provisioning architecture [Task 1]
- The unified checkout is documented as two business tunnels kept separate under the hood: one-shot cart and billed recurring subscriptions via `recurring_checkout` and `rail=billing` [Task 2]
- In the current docs, bank transfer is a recorded payment-rail choice on the commercial document, not immediate capture; the admin payment-marking action is the business trigger once funds are actually received [Task 2]
- `validate-staging.mjs` was realigned so `AD_INTEGRATION_MODE=controlled_write` is accepted in staging and `ALLOW_LOCAL_INTERNAL_API_URL` is no longer required; older failures on those checks can be tooling drift rather than live-environment truth [Task 2]
- Known non-regression noise: `build:api` / `dotnet build` can complete with existing Windows/AD `CA1416` warnings only [Task 1][Task 2]

## Failures and how to do differently

- symptom: broad doc patches fail or apply against the wrong text -> cause: encoding drift and stale context in long files -> fix: read the exact current file text and patch smaller verified blocks [Task 1][Task 2]
- symptom: a release doc sweep accidentally pulls unrelated local artifacts -> cause: the worktree already contains extra files such as temp SQL backups -> fix: inspect git status carefully and explicitly exclude unrelated artifacts like `tmp/backup-avant-reparation-tz-20260709.sql` [Task 1][Task 2]
- symptom: takeover work duplicates docs that already exist -> cause: assuming the repo is missing the handoff instead of checking the staged/current files -> fix: inspect current doc inventory and index state before recreating documents [Task 2]

# Task Group: kermaria-client-platform / managed content docs and architecture handoff

scope: Documentation-grounded managed-content work for legal pages and pack sheets, especially when future agents need the real architecture, repo insertion points, or the right entry docs before making changes.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria tasks about managed content, legal pages, pack sheets, public/admin shell routing, or documentation refreshes; treat exact commit/tag values as historical evidence.

## Task 1: Explore managed-content architecture and repo insertion points, success

### rollout_summary_files

- rollout_summaries/2026-07-07T14-26-31-pKOM-v0_33_managed_content_docs.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T16-26-31-019f3cf9-2de8-72e2-94f1-a4ed6769888c.jsonl, updated_at=2026-07-09T16:16:18+00:00, thread_id=019f3cf9-2de8-72e2-94f1-a4ed6769888c, reconnaissance before doc edits)

### keywords

- managed content, legal:cgv, legal:mentions-legales, AppShell, public-route-config, admin/content, managed_content_entries, public pack sheets, PublicPackCard, PublicPackComparisonTable

## Task 2: Align docs with the real V0.33 managed-content implementation, success

### rollout_summary_files

- rollout_summaries/2026-07-07T14-26-31-pKOM-v0_33_managed_content_docs.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T16-26-31-019f3cf9-2de8-72e2-94f1-a4ed6769888c.jsonl, updated_at=2026-07-09T16:16:18+00:00, thread_id=019f3cf9-2de8-72e2-94f1-a4ed6769888c, central V0.33 doc plus cross-doc reconciliation)

### keywords

- V0.33_CONTENUS_ADMINISTRABLES.md, ARCHITECTURE.md, DATA_MODEL.md, GUIDE_ADMIN.md, V0.27_PUBLIC_VITRINE.md, ROADMAP.md, API_CONTRACT.md, DEPLOYMENT_WINDOWS.md, PRODUCTION_DEPLOYMENT.md, v0.33-managed-content-docs

## Task 3: Preserve a reprise checklist for future managed-content regressions, success

### rollout_summary_files

- rollout_summaries/2026-07-07T14-26-31-pKOM-v0_33_managed_content_docs.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T16-26-31-019f3cf9-2de8-72e2-94f1-a4ed6769888c.jsonl, updated_at=2026-07-09T16:16:18+00:00, thread_id=019f3cf9-2de8-72e2-94f1-a4ed6769888c, diagnostic checklist preserved in V0.33 docs)

### keywords

- reprise checklist, diagnostic, shared key registry, URL decoding, X-Data-Source, public_pack_catalog_content, managed_content_entries, PUBLIC_PACKS

## User preferences

- when the user said `Avant de coder : Commence par explorer le codebase...` -> start with reconnaissance before edits on similar repo-change requests [Task 1]
- when the user said `si une solution simple et robuste existe deja dans le projet, reutilise-la` -> prefer the existing managed-content/admin pattern instead of inventing a parallel CMS or file-based workaround [Task 1]
- when the user wanted content editable `sans avoir a editer les fichiers a la main` -> default to a persistent admin-editable model for similar content tasks [Task 1]
- when the user asked for one `pages / contenus administrables` logic plus visible `date de mise a jour` or version -> keep legal pages and pack sheets in one managed-content model and preserve public updated-at/version metadata [Task 1]
- the user wanted the work to stay understandable and maintainable later -> favor a central reference doc plus cross-links over scattered one-off notes [Task 2][Task 3]

## Reusable knowledge

- The repo already had the right insertion points for managed content: `apps/webportal/app/admin/content`, `apps/webportal/app/admin/content/[key]/page.tsx`, `apps/webportal/app/api/admin/content/[key]/route.ts`, matching internal API pieces, and the public pack catalog components [Task 1]
- The shell split is route-aware via `AppShell.tsx` and `public-route-config.ts`; `PublicShell` is selected only for public routes [Task 1]
- The durable mental model is: `PUBLIC_PACKS` = product manifest, `public_pack_catalog_content` = `/offres` marketing content, `managed_content_entries` = editable legal pages and pack sheets [Task 2][Task 3]
- Public pack sheets are not a copy of the vitrine; they combine the manifest, the commercial catalog, and Markdown editorial content [Task 2]
- `docs/V0.33_CONTENUS_ADMINISTRABLES.md` is the best single entry point for understanding the managed-content feature and now includes a reprise/diagnostic checklist [Task 2][Task 3]
- Admin previews should preserve context by opening public pages in a new tab [Task 2]
- The docs rollout was published as commit `4ad773a` and tag `v0.33-managed-content-docs`, which are useful historical anchors when tracing how the documentation was reconciled to the code [Task 2]

## Failures and how to do differently

- symptom: broad documentation edits fail unexpectedly -> cause: doc files drifted or had local modifications that broke large context-based patches -> fix: verify current file text first and patch in smaller targeted hunks [Task 1][Task 2]
- symptom: a docs-only commit appears to stage nothing in PowerShell -> cause: the command used `&&` in a PowerShell context -> fix: use separate commands or PowerShell separators like `;` [Task 2]
- symptom: docs work risks pulling unrelated repo changes into the commit -> cause: the branch already contains other work or temp backup files -> fix: stage only the intended docs files and leave temporary backups untracked [Task 2]

# Task Group: kermaria-client-platform / Stripe portal URL and webhook recovery

scope: Stripe and portal-return troubleshooting for the canonical portal URL, staging config drift, webhook replay, and the recovery runbook around V0.35.2.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria payment-return or Stripe webhook recovery tasks when `PUBLIC_PORTAL_URL`, `WEBPORTAL_BASE_URL`, migrations, or replay behavior are in scope; exact event ids are historical evidence.

## Task 1: Fix canonical portal URL handling for Stripe and PayPal returns, success

### rollout_summary_files

- rollout_summaries/2026-07-06T20-52-03-QPku-stripe_portal_url_and_webhook_recovery.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\06\rollout-2026-07-06T22-52-03-019f3933-cc67-7220-94b7-4f2d0445fd62.jsonl, updated_at=2026-07-09T16:06:31+00:00, thread_id=019f3933-cc67-7220-94b7-4f2d0445fd62, canonical portal URL fix)

### keywords

- PUBLIC_PORTAL_URL, WEBPORTAL_BASE_URL, public-routes.ts, getPortalPublicUrl, portail.home.bzh, localhost:3000, success_url, cancel_url, return_url, test:payments-stripe

## Task 2: Diagnose Stripe webhook failures and document recovery/replay, success

### rollout_summary_files

- rollout_summaries/2026-07-06T20-52-03-QPku-stripe_portal_url_and_webhook_recovery.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\06\rollout-2026-07-06T22-52-03-019f3933-cc67-7220-94b7-4f2d0445fd62.jsonl, updated_at=2026-07-09T16:06:31+00:00, thread_id=019f3933-cc67-7220-94b7-4f2d0445fd62, recovery runbook and replay proof)

### keywords

- V0.35.2_STRIPE_PORTAL_RETURN_AND_WEBHOOK_RECOVERY.md, AD_REQUIRED_OU_ROOT, schema_migrations, 022_webhook_resource_id_length, invoice.paid, invoice.payment_succeeded, curl.exe --data-binary, evt_1TqJvyPjVmQIehZau0CZhslp, v0.35.2

## User preferences

- when the user asked `Je mets quoi comme valeur alors ? Mes deux noms de domaine ?` -> give the exact canonical value to set, not a vague set of options [Task 1]
- when the user asked `Tu peux l'intégrer dans les variables ?` -> update the repo/local env directly once the safe value is clear [Task 1]
- when the user reported `Toujours pas j'ai une erreur...` and provided logs -> continue from the runtime evidence until the exact failure and recovery path are identified [Task 2]

## Reusable knowledge

- In this repo, `PUBLIC_PORTAL_URL` is a single canonical portal URL and should point to `https://portail.home.bzh`, not the `www.*` vitrine host and not a multi-domain list [Task 1]
- `WEBPORTAL_BASE_URL` and `PUBLIC_PORTAL_URL` should be updated together to keep return URLs, metadata, and helpers aligned [Task 1]
- `getPortalPublicUrl(request)` is the shared helper for portal return URLs; avoid route-local localhost fallbacks [Task 1]
- The recovery runbook now lives in `docs/V0.35.2_STRIPE_PORTAL_RETURN_AND_WEBHOOK_RECOVERY.md` and records the canonical URL, `AD_REQUIRED_OU_ROOT` requirement when `AD_INTEGRATION_MODE` is `read_only` or `controlled_write`, the `022_webhook_resource_id_length` migration, `schema_migrations` drift repair, and the Stripe replay procedure via `curl.exe --data-binary` [Task 2]
- The webhook path now handles both `invoice.paid` and `invoice.payment_succeeded`, reads the subscription id from either `data.object.subscription` or `data.object.parent.subscription_details.subscription`, and can safely ignore duplicate event replays [Task 2]

## Failures and how to do differently

- symptom: Stripe or PayPal returns go to `localhost:3000` -> cause: stale local env or portal URL drift -> fix: set one canonical `https://portail.home.bzh` value in both `WEBPORTAL_BASE_URL` and `PUBLIC_PORTAL_URL` [Task 1]
- symptom: a Stripe fix is mixed with unrelated local work -> cause: the worktree is already dirty -> fix: isolate only the URL helper and recovery docs before staging or committing [Task 2]
- symptom: the first diff/status pass is noisy after staging changes -> cause: overlapping add/status commands on a dirty tree -> fix: re-run add and status in sequence and verify only intended files are staged [Task 2]

# Task Group: kermaria-client-platform / signup, hCaptcha, SMTP, and set-password live fixes

scope: Live signup, hCaptcha, SMTP, and set-password work on the Kermaria webportal/API stack, especially when the issue touches runtime config, ARR/proxy IP handling, or Windows deployment/runbook details.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria signup/email/token-validation tasks on staging/live; exact mailbox names, service states, and commit hashes are historical evidence.

## Task 1: Restore SMTP live send, open internal signup, and deploy config/doc updates, success

### rollout_summary_files

- rollout_summaries/2026-07-07T15-05-27-iICg-smtp_ovh_resiliation_fix_signup_recette_opened_deployed.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev\kermaria-client-platform\.claude\worktrees\funny-sammet-ec5801, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T17-05-27-019f3d1c-d57e-74e0-beef-979fec4dc424.jsonl, updated_at=2026-07-07T15:05:32+00:00, thread_id=019f3d1c-d57e-74e0-beef-979fec4dc424, OVH mailbox root cause, internal signup opening, and staging deploy)

### keywords

- SMTP, ssl0.ovh.net, STARTTLS, contact@zacharyhounsa.ovh, SIGNUP_ENABLED, EMAIL_LIVE_ALLOWLIST_ONLY, @home.bzh, build-webportal-config.ps1, build-api-config.ps1, SIGNUP_OUVERTURE_RECETTE.md, PUBLIC_PORTAL_URL

## Task 2: Validate set-password tokens on GET, deploy the fix, and patch the runbook, success

### rollout_summary_files

- rollout_summaries/2026-07-07T15-05-27-gEbL-set_password_ux_fix_staging_deploy_runbook_push_cleanup.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform\.claude\worktrees\friendly-archimedes-93ab6d, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T17-05-27-019f3d1c-d57c-7c40-a510-ae0b3de35f59.jsonl, updated_at=2026-07-07T15:05:32+00:00, thread_id=019f3d1c-d57c-7c40-a510-ae0b3de35f59, GET non destructif, staging deploy, runbook correction, push, and backup cleanup)

### keywords

- set-password, signup/verify, GET validation, TOKEN_INVALID, TOKEN_EXPIRED, verify-signup-contract.mjs, start-webportal.ps1, logs ACL, DEPLOYMENT_WINDOWS.md, V0.26-2b

## Task 3: Support native HTML set-password fallback without raw JSON errors, success

### rollout_summary_files

- rollout_summaries/2026-07-21T12-34-25-GxM2-kermaria_set_password_and_v039_deployment.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\21\rollout-2026-07-21T14-34-30-019f84ab-955d-7681-b996-eb13107528f6.jsonl, updated_at=2026-07-21T16:12:27+00:00, thread_id=019f84ab-955d-7681-b996-eb13107528f6, native form compatibility and contract/runtime checks)

### keywords

- /api/set-password, INVALID_REQUEST, application/x-www-form-urlencoded, requestBffJson, NextResponse.redirect, 303 See Other, getPortalPublicUrlFromHeaders, verify-signup-contract.mjs, GET /internal/signup/set-password/validate

## User preferences

- when debugging live issues, the user provided direct repro evidence and expected concrete diagnosis rather than speculative code changes [Task 1]
- when a browser reproduction shows raw `/api/set-password` `INVALID_REQUEST` JSON -> reproduce the exact request mode first; preserve anti-replay and repair the BFF/presentation boundary. [Task 3]
- when the user corrected the SMTP context with `mon plan MX est résilié... C'est le même mot de passe` -> treat mailbox identity changes as the first fix axis before changing transport code [Task 1]
- when the user said `Faut qu'on règle l'erreur du Captcha là` and kept steering with live tests -> prioritize live verification over theory for signup/captcha incidents [Task 2]
- when the user said `On peut le faire et après on commit` -> for this workflow, deploy/verify can come before commit when the goal is operational recovery [Task 2]
- when the user said `Ce n'est donc PAS une faille — juste une mauvaise UX` and `Ne rien changer au comportement backend d'anti-rejeu` -> preserve backend consumption semantics and solve set-password issues at the GET/presentation layer when possible [Task 2]
- when the user said `Fait tout ce que tu as à faire` -> treat that as permission to finish the remaining operational work end-to-end, including deploy verification, docs, push, runbook updates, and cleanup [Task 2]

## Reusable knowledge

- This repo’s live email path uses `System.Net.Mail`; for OVH `ssl0.ovh.net:587`, STARTTLS can succeed while AUTH fails, so direct `curl.exe --ssl-reqd smtp://...` is a reliable way to separate mailbox/auth issues from application-code issues [Task 1]
- Internal signup opening requires more than `SIGNUP_ENABLED=true`: hCaptcha keys must be present, the allowlist must permit the target recipients, and `PUBLIC_PORTAL_URL` must point at the real portal host for verification/set-password links [Task 1]
- `build-webportal-config.ps1` writes `\\KERMARIA-SRV-01\C$\ProgramData\Kermaria\webportal.config.json` and `build-api-config.ps1` writes `\\KERMARIA-SRV-02\C$\ProgramData\Kermaria\api-internal.config.json` [Task 1]
- `/signup/verify` is the right pattern to mirror for “validate at GET/render, not after submit”; the backend already enforces anti-replay because `password_setup_token_hash` is nulled on consumption, so `FindApprovedByPasswordHashAsync` can power a read-only GET validation path [Task 2]
- The Windows rename-swap runbook for WEBPORTAL must preserve `start-webportal.ps1` and a writable `logs\` directory with the service ACL before restarting `KermariaWebportal` [Task 2]
- `verify-signup-contract.mjs` is the focused validation gate for signup-flow contract changes when repo-wide `tsc` noise is unrelated [Task 2]
- `SetPasswordForm` normally posts JSON through `requestBffJson`, but its native `<form action="/api/set-password" method="post">` fallback posts `application/x-www-form-urlencoded`; the BFF must support both body modes, recover the hidden token, and redirect native submissions with `303 See Other` to `/set-password?status=success` or `?error=...`. [Task 3]
- Use `getPortalPublicUrlFromHeaders(request.headers)` for redirects so the incoming `127.0.0.1` host is preserved rather than becoming `localhost`. `verify-signup-contract.mjs` passed 36 checks; native POST returned 303 and invalid GET rendered HTTP 200 without a password field. [Task 3]
- Related skill: skills/kermaria-windows-staging-deploy/SKILL.md [Task 1][Task 2]

## Failures and how to do differently

- symptom: SMTP errors suggest TLS transport trouble -> cause: the actual break can be mailbox/auth drift instead of `SMTP_USE_STARTTLS` behavior -> fix: probe the mailbox directly with `curl.exe` before changing code [Task 1]
- symptom: local HTTPS smoke tests to `https://portail.home.bzh/signup` fail once -> cause: client-side connection noise can mask healthy remote services -> fix: confirm with server-side service status, config, and logs before declaring the deploy broken [Task 1]
- symptom: staging webportal swap fails after a standalone copy -> cause: missing wrapper files or writable `logs\` directory/ACL in the payload -> fix: stage `start-webportal.ps1`, `logs\`, and the service ACL explicitly before swapping live [Task 2]
- symptom: repo-wide `tsc --noEmit` explodes with ambient Node-type errors -> cause: pre-existing global type noise unrelated to the touched signup files -> fix: use targeted lint/contract checks or a focused build instead [Task 2]
- symptom: a native set-password form displays raw `INVALID_REQUEST` JSON -> cause: the BFF accepts only the React JSON request while browser fallback sends form-urlencoded -> fix: parse form bodies and use an HTTP 303 presentation redirect; keep GET token validation non-destructive and POST as the sole token-consuming operation. A full browser/mail-link walkthrough was still not performed. [Task 3]

# Task Group: kermaria-client-platform / PayPal subscriptions, provisioning, and contract maintenance

scope: Subscription implementation, webhook/billing flow, provisioning handoff, and contract-test maintenance around the PayPal/Stripe subscription surfaces.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria subscription, PayPal, webhook, provisioning, or related contract-test work; exact branch names and commit hashes are historical anchors.

## Task 1: Implement V0.22 PayPal subscriptions phases A-F and document the test recipe, success

### rollout_summary_files

- rollout_summaries/2026-07-07T15-05-37-NGdZ-v022_subscriptions_paypal_phases_docs_pr_followup.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T17-05-37-019f3d1c-fd1d-7a30-b84d-a8f8cdeaf213.jsonl, updated_at=2026-07-07T15:05:37+00:00, thread_id=019f3d1c-fd1d-7a30-b84d-a8f8cdeaf213, full implementation plus doc/gotcha capture)

### keywords

- V0.22, PAYPAL_MODE, paypal_plan_id_sandbox, paypal_plan_id_live, PAYPAL_WEBHOOK_VERIFY, subscriptions, webhook, BILLING.SUBSCRIPTION.ACTIVATED, PAYMENT.SALE.COMPLETED, JsonPropertyName, docs/V0.22_SUBSCRIPTIONS.md

## Task 2: Fix provisioning retry UX and pause with a V0.31 remaining-tests handoff, partial/success

### rollout_summary_files

- rollout_summaries/2026-07-06T16-41-46-tCCs-v031_subscription_provisioning_retry_fix_and_md_handoff.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\06\rollout-2026-07-06T18-41-46-019f384e-a6ad-7f30-9ca3-73a3509167b0.jsonl, updated_at=2026-07-07T08:25:41+00:00, thread_id=019f384e-a6ad-7f30-9ca3-73a3509167b0, provisioning retry fix plus resume note)

### keywords

- V0.31_TESTS_RESTANTS.md, SubscriptionService, LdapActiveDirectoryService, AD_GROUP_MEMBER_ALREADY_PRESENT, PROVISIONING_UNCHANGED, AdminReconcileProvisioningButton, useTransition, GG_VPN, GG_RDS, GG_Radio

## Task 3: Realign subscriptions contract assertions after the admin catalog split, success

### rollout_summary_files

- rollout_summaries/2026-07-07T15-05-37-TNfC-subscriptions_contract_paypal_ids_detail_page_and_repo_renam.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev\kermaria-client-platform\.claude\worktrees\compassionate-nobel-b9bb87, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T17-05-37-019f3d1c-fcdf-7190-8dbb-9601e4e75f04.jsonl, updated_at=2026-07-07T15:05:37+00:00, thread_id=019f3d1c-fcdf-7190-8dbb-9601e4e75f04, detail-page assertions plus repository rename fix)
- rollout_summaries/2026-07-07T15-05-37-KFG3-subscriptions_contract_paypal_plan_ids_form_assertions.md (cwd=\?\C:\Users\zhounsah\Documents\Dev\kermaria-client-platform\.claude\worktrees\competent-feistel-47f338\apps\webportal, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T17-05-37-019f3d1c-fce5-7c00-8fba-478c618b5143.jsonl, updated_at=2026-07-07T15:05:37+00:00, thread_id=019f3d1c-fce5-7c00-8fba-478c618b5143, earlier contract-fix attempt on moved PayPal ID assertions)

### keywords

- verify-subscriptions-contract.mjs, paypalPlanIdSandbox, paypalPlanIdLive, app/admin/catalog/[id]/page.tsx, AdminCatalogOfferForm.tsx, GetByExternalIdAsync, test:subscriptions, test:payments-stripe

## User preferences

- for long subscription rollouts, the user accepted phased, commit-sized delivery with validation between phases rather than one opaque batch [Task 1]
- the user wanted durable docs and manual test recipes after each complex integration phase, not just code changes [Task 1]
- when the user said `Note ça dans un .md, on fera la suite après` -> pause cleanly with a markdown handoff instead of continuing deeper into provisioning work [Task 2]
- when fixing subscription contract tests, the user wanted the assertion moved to where the UI really renders today and asked to review nearby assertions too, not just the first failing line [Task 3]
- when touching subscription contracts, preserve adjacent suite behavior such as `npm run test:payments-stripe`, not just `test:subscriptions` [Task 3]

## Reusable knowledge

- The repo’s subscription implementation uses MariaDB migrations in `apps/api-internal/Migrations/MariaDb/`, records applied ids in `schema_migrations`, and prefers `UTC_TIMESTAMP(6)` / `DateTime.UtcNow` for timestamps [Task 1]
- Sandbox and live PayPal plan ids are distinct and the repo now resolves them by `PAYPAL_MODE`; a single `paypalPlanId` field is not sufficient once both modes exist [Task 1]
- `PAYPAL_WEBHOOK_VERIFY=false` is the easiest local webhook test mode; when tests return `401`, verify the running BFF actually saw the env change and was restarted [Task 1]
- The webhook path stores raw payloads in `paypal_webhook_events.raw_payload`, and common failure shields are: `ECONNREFUSED` usually means API-INTERNAL is not listening on the expected port, a reused `event_id` will hit idempotence, and `PAYMENT.SALE.COMPLETED` can fail if the billing-document insert shape is wrong [Task 1]
- The existing AD service layer is already idempotent and surfaces no-op codes like `AD_GROUP_MEMBER_ALREADY_PRESENT` / `AD_GROUP_MEMBER_ALREADY_ABSENT`; future provisioning work should reuse those primitives [Task 2]
- The admin subscription page already has the natural supervision surfaces: provisioning status plus retry, and the current V0.31 resume order is documented in `docs/V0.31_TESTS_RESTANTS.md` [Task 2]
- In the current admin catalog UI, PayPal plan ids render on `app/admin/catalog/[id]/page.tsx` for monthly offers, not on the list page; the repository lookup abstraction is now `GetByExternalIdAsync(rail, externalId)` rather than `GetByPayPalIdAsync` [Task 3]

## Failures and how to do differently

- symptom: local webhook tests return `401` or `ECONNREFUSED` -> cause: either verification mode/env drift or API-INTERNAL not listening where the BFF expects -> fix: confirm the running process env and port first before changing webhook code [Task 1]
- symptom: PayPal/TS contracts drift after a sandbox/live split or API rename -> cause: some consumers still assume one `paypalPlanId` field or PayPal-specific lookup naming -> fix: trace every consumer to active-mode resolution and the current repository abstraction [Task 1][Task 3]
- symptom: worktree cleanup or deletion fails on Windows -> cause: the running session still holds the directory -> fix: remove the git worktree entry and clean the physical directory later from another shell/session [Task 1][Task 2]
- symptom: the provisioning retry button stays stuck after success -> cause: UI state and `router.refresh()` are not coordinated -> fix: use `useTransition`, keep busy state through refresh, and clear submission state in `finally` [Task 2]
- symptom: `test:subscriptions` still fails after moving the first PayPal assertion -> cause: the failure may be a second stale assertion such as the repository method rename -> fix: review the surrounding contract block and rerun adjacent suites from the correct directory [Task 3]

# Task Group: kermaria-client-platform / V0.24 staging checklist, audit, and documentation

scope: V0.24 staging-recette planning and execution on SRV-01/SRV-02/SRV-07, including the live tracker, audit matrix, cross-doc updates, and follow-up-chip routing.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria V0.24 staging/audit/doc tasks on the same split-host topology; exact checklist counts, email addresses, and backup artifacts are time-specific evidence.

## Task 1: Run the first V0.24 staging pass and update the tracking docs, partial

### rollout_summary_files

- rollout_summaries/2026-07-06T16-21-53-4reu-v024_staging_validation_doc_update_and_intrusive_service_tes.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\06\rollout-2026-07-06T18-21-53-019f383c-7403-7af1-b718-f2b0bd6bfa3e.jsonl, updated_at=2026-07-06T17:07:05+00:00, thread_id=019f383c-7403-7af1-b718-f2b0bd6bfa3e, early validation plus intrusive tests and doc updates)

### keywords

- validate:staging, check:health, INTERNAL_API_URL, PUBLIC_PORTAL_URL, robots.txt, sitemap.xml, X-Robots-Tag, blocked_allowlist, correlation_id, npm audit, dotnet list --vulnerable

## Task 2: Complete the V0.24 recipe, security audit, and documentation set, success

### rollout_summary_files

- rollout_summaries/2026-07-07T15-05-27-NTd3-v0_24_staging_recipe_audit_docs.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform\.claude\worktrees\priceless-driscoll-a6928d, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T17-05-27-019f3d1c-d593-7ba2-8792-64403bb7daad.jsonl, updated_at=2026-07-07T15:05:32+00:00, thread_id=019f3d1c-d593-7ba2-8792-64403bb7daad, long-form recipe, audit, and docs evidence)
- rollout_summaries/2026-07-07T15-05-27-A5js-v024_staging_recette_audit_docs_stripe_smtp_hcaptcha.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform\.claude\worktrees\priceless-driscoll-a6928d, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T17-05-27-019f3d1c-d58f-7571-b571-99ac13e38c0a.jsonl, updated_at=2026-07-07T15:05:32+00:00, thread_id=019f3d1c-d58f-7571-b571-99ac13e38c0a, complementary follow-up chips for Stripe, SMTP, and hCaptcha)

### keywords

- split-host staging, TEST_WEB_RESTORE, check-secrets, SECRET_ROTATION.md, GUIDE_CLIENT_PAIEMENT.md, GUIDE_ADMIN.md, hCaptcha chip, SMTP chip, Stripe chip, restore:mariadb, audit_logs, MOCK-INV, MOCK-NUM

## User preferences

- when the user asked `Tu peux me faire une liste de tous les éléments à tester sur la v0.24 ?` and `Et je coche au fur et à mesure` -> provide an exhaustive checklist broken into small stable items with tracker ids preserved [Task 1]
- the user asked in French and wanted the tracker filled live with status/comment/date/operator -> keep future V0.24 operational checklists and progress updates in French unless asked otherwise [Task 1][Task 2]
- when the user said `À chaque scénario, coche le statut ([x] / [!] / [-]), ajoute un commentaire concis, la date et l'opérateur. Commits par lot cohérent` -> update `docs/V0.24_SUIVI.md` as the live source of truth while the work is happening, not afterward [Task 2]
- the user accepted practical RDP guidance and direct execution steps instead of long hypotheses during live staging work [Task 1][Task 2]
- the user accepted chip-based follow-up handling for long-running fixes once the main recipe/audit/doc stream was complete [Task 2]

## Reusable knowledge

- `docs/V0.24_SUIVI.md` is the authoritative live tracker, while `docs/V0.24_STABILISATION.md` defines the full scope and `docs/SIGNUP_OUVERTURE_RECETTE.md` captures the internal-signup opening procedure [Task 1]
- The critical split-host topology is SRV-01 = WEBPORTAL, SRV-02 = API-INTERNAL, SRV-07 = MariaDB; `INTERNAL_API_URL` on WEBPORTAL must point to `http://192.168.100.202:5000`, and `PUBLIC_PORTAL_URL` must point to `https://portail.home.bzh` for return URLs [Task 1][Task 2]
- `validate:staging` reads `process.env`, so both JSON configs and any staging-only variables not in JSON must be loaded into the current session before running it [Task 1][Task 2]
- The recipe validated: BPCE mock issuance, PayPal one-shot sandbox payment, PayPal monthly subscription activation/cancel/idempotence, vitrine `X-Robots-Tag` split, timezone behavior, and MariaDB restore into `TEST_WEB_RESTORE` with a temporary API pointed at the restored DB; unresolved deeper fixes were split into Stripe, SMTP, and hCaptcha chips instead of blocking the whole V0.24 stream [Task 2]
- The audit pattern that worked was: secrets matrix first, then `check:secrets`, `git grep`, `.env.example` coverage, `npm audit`, `dotnet list --vulnerable`, and log greps for proof-backed controls [Task 2]
- `README.md` is the canonical navigation entry for user-facing docs, `ROADMAP.md` carries coarse milestone references, and the detailed execution state stays in `V0.24_SUIVI.md` [Task 2]

## Failures and how to do differently

- symptom: `validate:staging` fails with confusing health/config issues -> cause: environment pollution (`DEMO_*`) or incomplete JSON/env loading -> fix: purge stray vars, load both configs, then add the missing staging-only variables before rerunning [Task 1][Task 2]
- symptom: WEBPORTAL readiness fails on staging -> cause: split-host config drift such as `INTERNAL_API_URL=http://localhost:5000` -> fix: treat localhost defaults as suspect first on this topology [Task 1][Task 2]
- symptom: restore tests fail early -> cause: target DB such as `TEST_WEB_RESTORE` was not created first -> fix: create the DB, restore, verify migrations/tables, then point the temporary API at it [Task 2]
- symptom: an audit note or follow-up doc breaks `check:secrets` -> cause: literal weak/test password strings were copied into docs -> fix: describe the rotation generically instead of keeping the literal [Task 2]
- symptom: live recipe state flips into risky modes during testing -> cause: signup/email/public toggles were left enabled after validation -> fix: revert staging to the safe baseline once the specific check is complete [Task 2]

# Task Group: kermaria-client-platform / validation and contract drift repair

scope: Conservative validation, contract-triage, and QA-matrix work: bring `npm run validate` back to green, classify local versus environment-only evidence, and avoid claiming unproduced test artifacts as delivered.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform; reuse_rule=reuse for Kermaria validate/contract-maintenance tasks on the same repo; treat `result*.txt` logs and exact dirty-file states as rollout-specific evidence.

## Task 1: Bring `npm run validate` back to green by fixing docs and stale contract scripts, success

### rollout_summary_files

- rollout_summaries/2026-07-06T13-32-17-mrru-kermaria_validate_green_obsolete_contracts_secret_redaction.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\06\rollout-2026-07-06T15-32-17-019f37a1-2b05-79d0-9598-01368d3ca764.jsonl, updated_at=2026-07-06T15:47:05+00:00, thread_id=019f37a1-2b05-79d0-9598-01368d3ca764, full green pipeline after doc redaction and contract realignment)

### keywords

- npm run validate, result1.txt, result2.txt, check:secrets, verify-admin-activity-contract.mjs, verify-commercial-foundation-contract.mjs, recentActivities, PayButton, next-env.d.ts, NU1900

## Task 2: Audit repo truth versus memory, fast-forward the worktree onto `main`, and isolate a pre-existing `check:secrets` blocker, partial

### rollout_summary_files

- rollout_summaries/2026-07-07T15-05-27-lImt-kermaria_roadmap_tests_memory_realign_main_validate_blocked.md (cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform\.claude\worktrees\objective-elbakyan-c059db, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\07\rollout-2026-07-07T17-05-27-019f3d1c-d57b-7572-b022-0f07bdaacde9.jsonl, updated_at=2026-07-07T15:05:32+00:00, thread_id=019f3d1c-d57b-7572-b022-0f07bdaacde9, repo-truth arbitration, contract-test realignment, fast-forward onto main, and validate blocker)

### keywords

- v0.19..HEAD, fast-forward, origin/main, verify-bpce-invoicing-contract.mjs, verify-subscriptions-contract.mjs, roadmap-current, check:secrets, [REDACTED_SECRET], ff-only, worktree, main

## Task 3: Produce a reviewed Kermaria Excel validation matrix, success

### rollout_summary_files

- rollout_summaries/2026-07-19T13-25-22-aUKG-kermaria_classeur_validation_tests_revu.md (cwd=C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\19\rollout-2026-07-19T15-25-27-019f7a8d-8252-70c0-b7d4-4b11f33d231e.jsonl, updated_at=2026-07-21T21:44:27+00:00, thread_id=019f7a8d-8252-70c0-b7d4-4b11f33d231e, reviewed `.xlsx` delivered; no staging/live execution in this rollout)

### keywords

- Kermaria_plan_validation_projet_2026-07-21_revu.xlsx, Controle qualite, docs/V0.24_SUIVI.md, docs/IMPLEMENTATION_MAP_CURRENT.md, verify-*.mjs, automated local, staging, manual, intrusive/live

## User preferences

- when the user asked to `vérifier` and later to `corriger ensemble`, they wanted evidence-based verification before edits and conservative changes rather than a broad refactor [Task 1]
- the user accepted `test obsolète` versus `régression produit` classification -> classify validate failures before changing code [Task 1]
- when the user asked `Maintenant, il faut tester quoi ?` -> end with a practical prioritized next test step grounded in the actual pipeline state [Task 1]
- when the user accepted leaving `apps/webportal/next-env.d.ts` alone -> exclude pre-existing generated noise from the intentional patch set [Task 1]
- when the user asked to `tranche explicitement` in case of divergence and `N'invente aucune validation staging` -> prefer repo docs, git history, and dated proof over memory-only claims, and keep “verified” separate from “still to verify” [Task 2]
- when the user asked for a clean report onto `main` and said `Si validate n'est pas faisable de façon fiable, explique-le en une phrase` -> stop clearly at the first trustworthy blocker rather than forcing a noisy continuation [Task 2]

- when the user asks for an `.xlsx` project test series -> deliver a real, directly usable workbook in French, not a recommendation list; separate automated local, staging, manual, and intrusive scenarios. [Task 3]

## Reusable knowledge

- The validate pipeline includes `check:secrets`, both TS typechecks, `build:web`, `build:api`, `test:api`, and the Node-based contract suites such as `test:activity` and `test:commercial` [Task 1]
- `result1.txt` / `result2.txt` are better truth sources than memory when reconstructing which stage actually failed or passed [Task 1]
- `app/admin/page.tsx` now links to `/admin/activity`, while `app/admin/activity/page.tsx` owns `recentActivities`; contract tests should assert the real owner file [Task 1]
- `app/commercial-documents/[id]/page.tsx` legitimately exposes payment UI via `PayButton`, `isPayPalConfigured()`, and `isStripeConfigured()`; modern commercial contracts should assert the current invariant instead of forbidding payment strings globally [Task 1]
- `NU1900` from NuGet vulnerability metadata retrieval was warning-only in the successful run [Task 1]
- `docs/V0.24_SUIVI.md` is stronger evidence than external memory when deciding whether a staging recipe item was actually executed; in this audit it overruled stale external claims and showed that V0.24 was still mostly unvalidated apart from `V0.26-2b` [Task 2]
- A clean report from a worktree to the main checkout can be a simple `git merge --ff-only` when the worktree commit is a direct descendant; in this rollout the fast-forward preserved the exact hash on `main` and kept the patch set to three files [Task 2]
- `npm run validate` begins with `check:secrets`, so a documented password literal can block the whole run before any build/test stage executes [Task 2]
- When `test:bpce` or `test:subscriptions` fail after product evolution, check whether the contract is anchored to a legacy UI location or API name before treating it as a runtime regression; `GetByExternalIdAsync` and `app/admin/catalog/[id]/page.tsx` were the current truth in this pass [Task 2]

- For a project test workbook, start with `AGENTS.md`, `README.md`, `docs/IMPLEMENTATION_MAP_CURRENT.md`, `docs/V0.24_SUIVI.md`, `docs/V0.24_STABILISATION.md`, root/webportal `package.json`, V0.36-V0.39 docs, and contract scripts. Preserve `browser -> WEBPORTAL / BFF -> API-INTERNAL -> MariaDB`: WEBPORTAL never calls MariaDB, AD, SMTP, BPCE, or other internal integrations directly. [Task 3]
- The delivered workbook is `C:\Users\zhounsah\Documents\Dev\outputs\019f7a8d-8252-70c0-b7d4-4b11f33d231e\Kermaria_plan_validation_projet_2026-07-21_revu.xlsx`; it includes synthesis, coverage, test catalogue, and `Controle qualite` for KO, open P0, and incomplete-proof rows. `docs/V0.24_SUIVI.md` remains the live tracker and staging/live claims still need dated proof. [Task 3]

## Failures and how to do differently

- symptom: the first validate diagnosis is incomplete -> cause: the log stopped at the first blocker and the later failures were still hidden -> fix: inspect the actual tail or rerun after each fix to reveal the next stage [Task 1]
- symptom: `check:secrets` fails on a tracking doc -> cause: a literal sensitive-looking value is present in the documentation -> fix: keep the operational meaning but replace the literal with generic wording [Task 1]
- symptom: a contract suite rejects real current UI behavior -> cause: the test is anchored to an older product version -> fix: inspect the current page/component first and move the assertion to the actual owner of the invariant [Task 1]
- symptom: memory, docs, and git history disagree about what was already validated -> cause: external memory over-asserted staging completion -> fix: arbitrate explicitly from repo docs and git evidence, and do not mark staging work done without dated proof [Task 2]
- symptom: shell output makes `validate` look like a general failure when only the first guardrail ran -> cause: `check:secrets` stops the pipeline immediately -> fix: explain that early blocker clearly instead of implying later suites were also run [Task 2]

- symptom: a workbook is mistaken for proof that staging/live validation happened -> cause: the matrix inventory is confused with execution evidence -> fix: label existing proof separately from scenarios to replay, and do not run destructive/live tests merely to fill it. [Task 3]

# Task Group: Windows Dev workspace cleanup and E2E-copy removal

scope: Safe space recovery in `C:\Users\zhounsah\Documents\Dev`, including cache/artifact cleanup and deletion of an identified project-copy directory after explicit confirmation.
applies_to: cwd=C:\Users\zhounsah\Documents\Dev; reuse_rule=reuse for similarly scoped Windows workspace cleanup only after exact-path validation and user authorization for material source-like directories.

## Task 1: Remove regenerable Dev caches and artifacts, success

### rollout_summary_files

- rollout_summaries/2026-07-19T13-16-37-UeoA-dev_workspace_cleanup_and_remove_chrome_e2e_copy.md (cwd=C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\19\rollout-2026-07-19T15-16-42-019f7a85-8290-7cb1-8fd3-ed73e4077e8c.jsonl, updated_at=2026-07-19T13:22:22+00:00, thread_id=019f7a85-8290-7cb1-8fd3-ed73e4077e8c, 3.45 GB cache/artifact cleanup with active project dependencies preserved)

### keywords

- Resolve-Path, .next, .npm-cache, out, .tmp, tmp, .codex-tmp, bin, obj, node_modules, dotnet locked DLL, Un élément de canal vide n’est pas autorisé

## Task 2: Identify then delete `kermaria-client-platform.chrome-e2e`, success

### rollout_summary_files

- rollout_summaries/2026-07-19T13-16-37-UeoA-dev_workspace_cleanup_and_remove_chrome_e2e_copy.md (cwd=C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\07\19\rollout-2026-07-19T15-16-42-019f7a85-8290-7cb1-8fd3-ed73e4077e8c.jsonl, updated_at=2026-07-19T13:22:22+00:00, thread_id=019f7a85-8290-7cb1-8fd3-ed73e4077e8c, removed only after explicit `Supprime-le` confirmation)

### keywords

- kermaria-client-platform.chrome-e2e, Playwright, package-lock.json, no .git, Resolve-Path, Remove-Item -LiteralPath, 61.4 MB

## Task 3: Inventory and archive source-like copies without deletion, partial

### rollout_summary_files

- rollout_summaries/2026-08-03T07-34-06-6dB3-dev_workspace_cleanup_archive_deletion_blocked.md (cwd=\\?\C:\Users\zhounsah\Documents\Dev, rollout_path=C:\Users\zhounsah\.codex\sessions\2026\08\03\rollout-2026-08-03T09-34-06-019fc68b-5162-7b23-b206-236f6c6b4dad.jsonl, updated_at=2026-08-03T07:47:22+00:00, thread_id=019fc68b-5162-7b23-b206-236f6c6b4dad, archives created; session policy blocked deletion)

### keywords

- _archives_cleanup_20260803, Compress-Archive, Remove-Item, blocked by policy, _artifacts, _deploy_v040, graphify-out, worktrees, 2.40 GB

## User preferences

- when the user asked to `nettoyer le dossier pour libérer de l’espace (supprimer le cache, dossiers temporaires, etc...)` -> inventory and measure first, delete only regenerable artifacts, and preserve source and active-project dependencies. [Task 1]
- when an E2E-looking copy contains recent code differences -> report that risk before deletion and wait for explicit confirmation; `Supprime-le` authorized this exact removal. [Task 2]

## Reusable knowledge

- Before recursive deletion, `Resolve-Path`, verify the resolved path stays beneath the intended workspace, measure it, then remove the literal validated path. A `.chrome-e2e` directory without `.git` but with a near-complete project tree, Playwright lockfile evidence, and test artifacts is likely a working snapshot, not an autonomous repository. [Task 1][Task 2]
- Do not kill `dotnet` processes merely to remove locked `apps\api-internal\bin` DLLs; locked binaries may be from active local APIs. `git status` must target a project subdirectory because `Dev` itself is not a Git repository. [Task 1]

## Failures and how to do differently

- symptom: PowerShell says `Un élément de canal vide n’est pas autorisé` after a loop -> cause: a pipeline was placed directly after the loop/block -> fix: assign `$rows = foreach (...) { ... }` first, then pipe `$rows`. [Task 1]
