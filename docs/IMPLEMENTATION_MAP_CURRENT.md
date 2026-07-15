# Implementation Map - Current State

Purpose: give a fast handoff for a human or another AI agent.

Read this file first, then open the versioned documents listed below.

## What exists today

The current repo state is built in layers:

1. `V0.22_SUBSCRIPTIONS.md`
   Monthly subscriptions via PayPal Subscriptions.
2. `V0.29_STRIPE_PAYMENTS.md`
   Stripe payment rail for commercial documents.
3. `V0.32_PUBLIC_PACKS.md`
   Public packs, public catalog editor, pack snapshots at signup, and pack to
   technical-service mapping.
4. `V0.33_CONTENUS_ADMINISTRABLES.md`
   Managed content for legal pages, about page, and public pack technical
   sheets.
5. `V0.35_CART_ALACARTE.md`
   Historical base for one-time cart checkout.
6. `V0.35.1_TIMEZONE_UTC_FIX.md`
   UTC hardening.
7. `V0.36_PANIER_UNIFIE_ABONNEMENTS_FACTURES.md`
   Current unified checkout state: one-time cart + billed recurring checkout,
   initial invoice, billing rail renewals, deferred cancellation.
8. `V0.37_CENTRE_TELECHARGEMENTS_CLIENT.md`
   Secure client download center: dedicated client page, admin CRUD,
   private binary storage, entitlement-based visibility.

## Functional picture

Public website:

- `/offres`: public comparison page for the 4 packs.
- `/offres/[slug]`: public technical sheet per pack.
- `/signup`: optional pack preselection snapshot at request time.

Authenticated client space:

- `/services`: current services + `Finaliser mon pack` block when signup
  approval carried a pack snapshot.
- `/downloads`: secure download center grouped by category.
- `/souscrire`: one-time offers, recurring packs, quote-only services.
- `/panier`: unified summary page.
- `/commercial-documents/[id]`: payment page for issued invoices.
- `/profile/subscriptions`: customer subscription history and cancellation.

Back-office:

- `/admin/catalog`: billable offers, prices, cadence, PSP ids.
- `/admin/public-pack-catalog`: public pack presentation editor.
- `/admin/content/[key]`: legal pages, about page, pack technical sheets.
- `/admin/downloads`: download list, detail, binary upload, visibility rules.
- `/admin/downloads/categories`: download categories.
- `/admin/subscriptions`: subscription list, detail, cancellation,
  provisioning visibility.

## Source of truth by concern

Public pack presentation:

- DB table `public_pack_catalog_content`
- edited from `/admin/public-pack-catalog`

Managed editorial content:

- DB table `managed_content_entries`
- edited from `/admin/content/[key]`

Secure download center:

- DB tables `download_categories`, `download_resources`,
  `download_resource_visibility_rules`
- binary payloads stored under private `DOWNLOAD_STORAGE_ROOT`
- access resolved by `apps/api-internal/Services/DownloadService.cs`
- edited from `/admin/downloads` and `/admin/downloads/categories`

Billable pack variants:

- DB table `commercial_offers`
- seeded by migration `023_public_pack_offers.sql`
- referenced by `external_reference`

Technical pack structure and provisioning intent:

- `packages/shared/src/index.ts`
- `PUBLIC_PACKS`
- `technicalServiceReferences`
- `provisioningGroupSamAccountNames`

Recurring checkout persistence:

- DB table `recurring_checkout_items`
- DB table `commercial_document_line_subscriptions`

Subscription lifecycle:

- DB table `subscriptions`
- handled by `SubscriptionService`
- renewed by `BillingSubscriptionRenewalWorker`

## Files to open first

Shared model and pack manifest:

- `packages/shared/src/index.ts`

Public pack helpers:

- `apps/webportal/lib/public-packs.ts`

Public pack UI:

- `apps/webportal/app/offres/page.tsx`
- `apps/webportal/components/PublicPackComparisonTable.tsx`
- `apps/webportal/components/PublicPackCard.tsx`
- `apps/webportal/app/admin/public-pack-catalog/page.tsx`
- `apps/webportal/components/AdminPublicPackCatalogForm.tsx`

Recurring checkout:

- `apps/api-internal/Services/RecurringCheckoutService.cs`
- `apps/api-internal/Services/BillingSubscriptionRenewalWorker.cs`
- `apps/api-internal/Services/Provisioning/BilledSubscriptionPaymentTrigger.cs`
- `apps/api-internal/Services/BilledRecurringCheckoutSchemaEnsurer.cs`
- `apps/webportal/app/panier/page.tsx`
- `apps/webportal/app/souscrire/page.tsx`

Secure downloads:

- `apps/api-internal/Services/DownloadService.cs`
- `apps/api-internal/Services/DownloadStorageService.cs`
- `apps/api-internal/Data/Repositories/MariaDbDownloadRepository.cs`
- `apps/webportal/app/downloads/page.tsx`
- `apps/webportal/components/AdminDownloadForm.tsx`
- `apps/webportal/components/AdminDownloadCategoriesManager.tsx`

Commercial document materialization:

- `apps/api-internal/Data/Repositories/MariaDbCommercialRepository.cs`

Subscription persistence:

- `apps/api-internal/Data/Repositories/MariaDbSubscriptionRepository.cs`
- `apps/api-internal/Services/SubscriptionService.cs`

## Important implementation decisions

- No dedicated `packs` SQL table was introduced. Public packs are a
  presentation and mapping layer over `commercial_offers`.
- Public texts are back-office editable, but technical mapping stays in code.
- Pack variants are identified by `external_reference`, not by UI labels.
- The recurring checkout creates local subscriptions first, then one issued
  invoice, then waits for invoice payment before activation.
- Renewals for the `billing` rail are driven locally by a background worker,
  not by a PSP subscription plan.
- Cancellation can become `pending_cancellation` and is finalized at term end.
- Downloads do not reuse `managed_content_entries`: metadata lives in dedicated
  SQL tables and binaries never enter `apps/webportal/public`.
- The client JSON for downloads never exposes physical storage paths or raw
  external URLs; every button goes through `/api/downloads/{id}/file`.
- Download visibility is computed from existing active entitlements only:
  `subscriptions.publicPackCode`, `subscriptions.offerExternalReference`,
  active `customer_services.service_type`, and provisioned groups reserved for
  future extension.

## Practical reading order for a takeover

If you want to understand the public offer model:

1. `docs/V0.32_PUBLIC_PACKS.md`
2. `packages/shared/src/index.ts`
3. `apps/webportal/lib/public-packs.ts`
4. `apps/webportal/app/admin/public-pack-catalog/page.tsx`

If you want to understand the current checkout and subscription behavior:

1. `docs/V0.36_PANIER_UNIFIE_ABONNEMENTS_FACTURES.md`
2. `apps/webportal/app/souscrire/page.tsx`
3. `apps/webportal/app/panier/page.tsx`
4. `apps/api-internal/Services/RecurringCheckoutService.cs`
5. `apps/api-internal/Services/Provisioning/BilledSubscriptionPaymentTrigger.cs`
6. `apps/api-internal/Services/BillingSubscriptionRenewalWorker.cs`

If you want to understand the secure download center:

1. `docs/V0.37_CENTRE_TELECHARGEMENTS_CLIENT.md`
2. `apps/api-internal/Services/DownloadService.cs`
3. `apps/api-internal/Data/Repositories/MariaDbDownloadRepository.cs`
4. `apps/webportal/app/downloads/page.tsx`
5. `apps/webportal/components/AdminDownloadForm.tsx`

If you want to debug provisioning:

1. `docs/V0.32_PUBLIC_PACKS.md`
2. `apps/api-internal/Data/Configuration/SubscriptionProvisioningRuntimeConfiguration.cs`
3. `apps/api-internal/Services/Provisioning/*`
