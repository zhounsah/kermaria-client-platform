---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-08-08
---

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

