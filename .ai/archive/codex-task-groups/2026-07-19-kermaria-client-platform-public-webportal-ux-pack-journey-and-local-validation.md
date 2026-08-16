---
source: codex-generated-memory
status: historical-or-revalidate
last_evidence_date: 2026-07-19
---

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


