v1

## User Profile

Works mainly on Kermaria from Windows/PowerShell across the Next.js webportal/BFF, ASP.NET Core API, MariaDB, Git releases, and SRV-11/12/13/16/21 operations. Requests are often in French. Values proof from source, runtime, Git, service state, and public rendering; distinguishes a technical deployment from a business-functional test. Uses Claude for product framing and reserves this agent primarily for server deployments, production verification, and sensitive operations.

## User preferences

- Start with the real architecture and existing models; do not create a parallel catalog, workflow, or abstraction when an established one fits.
- For editorial work, begin with the requested read-only analysis; create no artificial articles or generic SEO pages, only the engine and fixtures strictly needed for validation.
- A public editorial feature is not complete from a direct URL alone: expose it through coherent public navigation and avoid routing `Services` to a `noindex` client portal.
- Keep the frontend out of financial authority: resolve and validate price-affecting choices server-side before signup/order.
- For public demos, use only fictitious local data; make them visibly DEMO/read-only, keep them isolated from production, and expose them from public navigation.
- Use proper French accents in every public-facing UI, metadata, breadcrumb, and CTA; direct URLs alone are not sufficient discoverability.
- For deployments, follow the runbook, hash artifacts, preserve backups/staging swaps, respect SRV-13 before SRV-12, then prove service, readiness, logs, and requested public markers.
- For Kermaria implementation/release work, "Commit, tag push comme d'habitude" means finish the explicit-file commit, annotated tag, push of `main` and tag, deployment, and production verification—not merely local tests.
- Keep concurrent dirty-worktree changes isolated: explicitly stage only intended files; never blanket-stage, reset, stash, or overwrite another session’s work.
- For customer backup status, validate the real business-service-to-job mapping; do not expose Veeam infrastructure details or invent a business metric from unavailable source data.
- Honor “sans modifier aucun fichier” / “aucun déploiement” and audit-first requests as strict boundaries; never display secrets or private-key contents.
- When content is self-editable (“le modifier moi-même”), route to the admin workflow instead of proposing source edits/deployment.
- Keep product ideation with Claude unless explicitly brought back here; prioritize deployment/operations.

- For production integrations, separate code wiring, live configuration, connectivity, webhook acceptance, and real downstream execution; do not call a chain end-to-end verified from partial evidence.
- When asked to publish only this conversation's changes, stage explicit paths and report remaining worktree edits, tag target, remote refs, and `main` containment separately.

## General Tips

- Kermaria Git root: `C:\Users\zhounsah\Documents\Dev\kermaria-client-platform`; run `git rev-parse --show-toplevel` before Git actions.
- Topology: SRV-11 edge/TLS; SRV-12 Ubuntu/Next (`kermaria-webportal`, `192.168.100.212:3000`); SRV-13 Windows/.NET (`KermariaApiInternal`, `192.168.100.213:5000`); SRV-16 Veeam collector; SRV-21 KoXo receiver `:8042`.
- Secrets stay redacted. Use Plink with a verified host fingerprint when the PuTTY context is needed; prefer short commands or LF remote scripts over deeply nested PowerShell/SSH quoting.
- A successful readiness probe is not public UI proof. After a restart, wait/retry, then fetch the exact requested route/version/visible marker.
- For Git releases, verify local and remote tag targets plus branch containment sequentially; a tag on a feature branch is not evidence that the release is in `main`.
- For Kermaria SEO pages, `/ressources` is the public hub and reuses `getPublicEditorialSitemap()`; `/solutions` is a `noindex` client portal. Keep npm SemVer valid and use `displayVersion` for labels such as `v1.3.3.1`.
- Canonical web hosts: `www.zacharyhounsa.ovh` public vitrine; `dashboard.zacharyhounsa.ovh` client; `administration.zacharyhounsa.ovh` admin. For host-routing changes, prove the exact redirect/status/header behavior, including a real unknown-route 404.
- `managed_content_entries` overrides SeedContent. Readiness/config does not prove a payment or webhook: use controlled real events where required.
- For SRV-12 standalone releases, ship `.next/static` and `public`, ensure `.next/cache` is writable by `kermaria-web`, switch the active `/opt/kermaria/webportal` symlink, then verify private and public readiness.
- SRV-12 deployment: use `192.168.100.212:3000` rather than loopback. PowerShell-to-SSH Bash needs LF (`tr -d '\r' | bash -s`); do not directly `source /etc/kermaria/webportal.env` because it has BOM/CRLF/quoted values.

## What's in Memory

### cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform

#### 2026-08-11

- SEO/canonicalisation dashboard/www and v1.3.3.4: www.zacharyhounsa.ovh, dashboard.zacharyhounsa.ovh, administration.zacharyhounsa.ovh, public-route-config.ts, proxy.ts, public-metadata.ts, readInternalJsonOrNull, v1.3.3.4
  - desc: Production-validated canonical host routing, metadata/OG, robots/sitemap, favicon, and real 404 follow-up to the editorial platform; search first for dashboard/www SEO or missing editorial slugs.
  - learnings: Vitrine paths on dashboard/admin 301 to `www`, private routes stay local; a no-redirect API response is `200 bytes=0`, so `response.text()` must return `null` before JSON parsing to obtain a real public 404. Release `7ea23f3`/`v1.3.3.4` was hash-checked and deployed on SRV-12.

#### 2026-08-10

- KoXo production synchronization, AD provisioning, and release 1.0.0.8: KoxoSyncWebhookTriggerService, password_set, koxo_pending, AD_PROVISIONING_GROUP_DNS, HOME\\zhounsah, 6efa2ec
  - desc: SRV-13-to-SRV-21 KoXo trigger evidence, child-domain group remapping, bounded SRV-13 ACLs, and reconciled `1.0.0.8` release state.
  - learnings: code/config/TCP were verified but no fresh signed POST/KoXo replay occurred; preserve aliases while changing DNs, use native `icacls.exe` argument arrays, and check remote tag plus `main` containment separately.

- Editorial platform and public SEO navigation: /ressources, Services, /solutions, getPublicEditorialSitemap(), displayVersion, managed_content_entries, IEditorialService
  - desc: Read-only editorial architecture analysis, Wiki/SEO/FAQ engine, and public discoverability model in `kermaria-client-platform`; search for CMS boundaries, `/ressources`, or admin editorial work.
  - learnings: retain the BFF/internal API/auth/audit path; `/solutions` is `noindex`, while `/ressources` lists published indexable SEO pages from the editorial sitemap. The newer v1.3.3.4 canonicalisation release is listed under 2026-08-11.

#### 2026-08-08

- Public isolated client-space demo: decouvrir-espace-client, DemoClientSpace.tsx, demo-client-space/data.ts, PublicShell.tsx, v1.2.1
  - desc: Public read-only client-space demo with local mock data, navigation exposure, French-visible copy, and SRV-12 release proof.
  - learnings: no production/API/auth/billing/backup calls; verify DEMO labels, navigation links, accent rendering, and forbidden technical names on the live page.
- Veeam backup status and v1.1.14 handoff: KoXoDATA, backup_jobs, backup_runs, protection_status, 044_veeam_backup_status, SRV-16, /backups
  - desc: Collector-to-private-API-to-MariaDB-to-portal flow, customer-safe mapping, and the partial v1.1.14 deployment.
  - learnings: SRV-13/SRV-16 are deployed; SRV-12 remains unproven (public v1.1.13 and `/backups` 404) until valid SSH access, symlink swap, restart, and public checks occur.
- Diagnostic configurator and central catalog: /diagnostic, /configurer, /api/configurer/resolve, CommercialOfferSummary, externalReference, v1.1.13
  - desc: Canonical public-pack/catalog resolution with server-side pricing validation, migrations 042/043, and a verified release.
  - learnings: reuse `packages/shared/src/index.ts`, `public-packs.ts`, and `internal-api.ts`; browser inputs are never financial truth.

#### 2026-08-07

- Public offers comparison-table editor: /offres, /admin/public-pack-catalog, PublicPackComparisonTable, PATCH /api/admin/public-pack-catalog
  - desc: Self-service public-pack comparison-content administration; search this before editing `/offres` source or deploying.
  - learnings: `/admin/public-pack-catalog` changes presentation rows without redeployment; `/admin/catalog` owns prices and billable variants.

### Older Memory Topics

#### cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform

- Read-only repository map and technical boundaries: Next.js, .NET, apps/webportal, apps/api-internal, MariaDB
  - desc: Strict reconnaissance, Git state, architecture, and validation entrypoints; cwd-specific.
- Payments, subscriptions, and live configuration: PayPal, Stripe, PUBLIC_PORTAL_URL, InvoiceIssuingService, SubscriptionProvisioningManager
  - desc: Provider runtime configuration, checkout contracts, webhook/recovery, and deployment safeguards.
- Signup, identity, AD, and KoXo history: SignupService, MariaDbSignupRepository, customer_ad_links, V0.40
  - desc: Signup/set-password lifecycle, AD provisioning, contract-backed implementation, and earlier KoXo design context.
- Public webportal, catalog, and client journeys: public-pack-catalog, managed_content_entries, checkout, download center
  - desc: Public UX, catalog/managed content, client downloads, and local validation patterns.
- Documentation, validation, Graphify, and factory: V1.0.0_DOCUMENTATION.md, validate, graphify extract . --code-only, .codex/factory
  - desc: Current-state documentation, contract/validation repair, code exploration, and persistent multi-agent orchestration.
- Staged SRV-13 then SRV-12 deployment, public backup/privacy, and KoXo webhook: SHA256, kermaria-webportal, managed_content_entries, KoxoSyncWebhookTriggerService, `202 queued`
  - desc: Release runbooks, persistent-content recovery, and webhook proof boundaries; use for older Kermaria production-release or KoXo work, cwd=C:\Users\zhounsah\Documents\Dev\kermaria-client-platform.

#### cwd=C:\Users\zhounsah\Documents\Dev

- SRV-11 Nextcloud Nginx vhost: KERMARIA-SRV-11, nextcloud.home.bzh, HAProxy, send-proxy-v2, nginx -t, HSTS
  - desc: Read-only Nginx/TLS audit and bounded vhost activation behind the existing HAProxy edge; cwd=C:\Users\zhounsah\Documents\Dev.
- Windows workspace cleanup and R740xd monitoring: _archives_cleanup_20260803, Compress-Archive, Remove-Item, R740xd, Zabbix
  - desc: Candidate inventory, source-like worktree archiving, deletion-policy stop rule, and dedicated-VM monitoring; cwd-specific.

#### cwd=C:\Users\zhounsah\Documents\Codex

- Server identity and UPS delivery: persistent assistant identity, least privilege, UPS My Choice
  - desc: Security boundary for durable privileged assistant accounts and browser delivery management.
