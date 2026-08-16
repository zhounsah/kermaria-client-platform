---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-11
---

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

