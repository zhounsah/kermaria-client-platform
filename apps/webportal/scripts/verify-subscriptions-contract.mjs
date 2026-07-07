import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const sharedTypes = await read("../../packages/shared/src/index.ts");
const internalApi = await read("lib/internal-api.ts");
const paypalLib = await read("lib/paypal.ts");
const paypalWebhookLib = await read("lib/paypal-webhook.ts");
const subscribeCreateRoute = await read("app/api/subscriptions/create/route.ts");
const subscribeReturnRoute = await read("app/api/subscriptions/return/route.ts");
const webhookRoute = await read("app/api/webhooks/paypal/route.ts");
const adminCancelRoute = await read(
  "app/api/admin/subscriptions/[id]/cancel/route.ts",
);
const adminReconcileRoute = await read(
  "app/api/admin/subscriptions/[id]/provisioning/reconcile/route.ts",
);
const clientCancelRoute = await read("app/api/subscriptions/[id]/cancel/route.ts");
const adminListPage = await read("app/admin/subscriptions/page.tsx");
const adminDetailPage = await read("app/admin/subscriptions/[id]/page.tsx");
const clientListPage = await read("app/profile/subscriptions/page.tsx");
const subscribeButton = await read("components/SubscribeButton.tsx");
const cancelButton = await read(
  "components/AdminCancelSubscriptionButton.tsx",
);
const clientCancelButton = await read(
  "components/ClientCancelSubscriptionButton.tsx",
);
const reconcileButton = await read(
  "components/AdminReconcileProvisioningButton.tsx",
);
const adminNav = await read("components/AdminNavigation.tsx");
const catalogForm = await read("components/AdminCatalogOfferForm.tsx");
const adminCatalogPage = await read("app/admin/catalog/page.tsx");
const adminCatalogDetailPage = await read("app/admin/catalog/[id]/page.tsx");
const servicesPage = await read("app/services/page.tsx");

const envExample = await read("../../.env.example");
const programCs = await read("../../apps/api-internal/Program.cs");
const subscriptionContracts = await read(
  "../../apps/api-internal/Contracts/SubscriptionContracts.cs",
);
const subscriptionService = await read(
  "../../apps/api-internal/Services/SubscriptionService.cs",
);
const webhookService = await read(
  "../../apps/api-internal/Services/PayPalWebhookService.cs",
);
const paypalCancellationWorker = await read(
  "../../apps/api-internal/Services/PayPalPendingCancellationWorker.cs",
);
const subscriptionRepoMaria = await read(
  "../../apps/api-internal/Data/Repositories/MariaDbSubscriptionRepository.cs",
);
const commercialRepoMaria = await read(
  "../../apps/api-internal/Data/Repositories/MariaDbCommercialRepository.cs",
);
const subscriptionMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/012_subscriptions.sql",
);
const offerMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/011_subscription_offers.sql",
);
const webhookMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/014_paypal_webhook_events.sql",
);
const linkMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/015_subscription_document_link.sql",
);
const planPerModeMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/016_paypal_plan_per_mode.sql",
);
const publicPackOfferMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/023_public_pack_offers.sql",
);
const signupPackSnapshotMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/024_signup_pack_snapshot.sql",
);
const subscriptionPackMetadataMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/025_subscription_pack_metadata.sql",
);
const paypalAutoPlanRoute = await read(
  "app/api/admin/catalog/[id]/paypal-plan/route.ts",
);
const paypalRuntimeConfigCs = await read(
  "../../apps/api-internal/Data/Configuration/PayPalRuntimeConfiguration.cs",
);

assert.match(
  offerMigration,
  /billing_cadence ENUM\('one_time','monthly'\)/,
  "billing_cadence ENUM doit etre defini.",
);
assert.match(
  offerMigration,
  /paypal_plan_id VARCHAR\(64\)/,
  "paypal_plan_id VARCHAR(64) doit etre defini.",
);
assert.match(
  subscriptionMigration,
  /CREATE TABLE.+subscriptions/i,
  "La table subscriptions doit etre creee.",
);
assert.match(
  subscriptionMigration,
  /paypal_subscription_id VARCHAR\(64\)/,
  "subscriptions.paypal_subscription_id doit etre defini.",
);
assert.match(
  subscriptionMigration,
  /next_billing_at/,
  "subscriptions.next_billing_at doit etre defini.",
);
assert.match(
  webhookMigration,
  /CREATE TABLE.+paypal_webhook_events/i,
  "paypal_webhook_events table doit etre creee.",
);
assert.match(
  webhookMigration,
  /UNIQUE KEY ux_paypal_webhook_events_event_id \(event_id\)/,
  "L'unicite event_id doit etre garantie pour l'idempotence.",
);
assert.match(
  linkMigration,
  /subscription_id CHAR\(36\)/,
  "commercial_documents.subscription_id doit etre defini.",
);
assert.match(
  planPerModeMigration,
  /paypal_plan_id_sandbox/,
  "Migration 016 doit ajouter paypal_plan_id_sandbox.",
);
assert.match(
  planPerModeMigration,
  /paypal_plan_id_live/,
  "Migration 016 doit ajouter paypal_plan_id_live.",
);
assert.match(
  planPerModeMigration,
  /DROP COLUMN paypal_plan_id\b/,
  "Migration 016 doit dropper l'ancienne colonne paypal_plan_id.",
);
assert.match(
  publicPackOfferMigration,
  /setup_fee_amount_cents/,
  "La migration packs doit ajouter les frais de mise en service.",
);
assert.match(
  publicPackOfferMigration,
  /billing_interval_months/,
  "La migration packs doit ajouter l'intervalle de facturation.",
);
assert.match(
  publicPackOfferMigration,
  /commitment_months/,
  "La migration packs doit ajouter l'engagement.",
);
assert.match(
  publicPackOfferMigration,
  /payment_mode ENUM\('monthly','upfront'\)/,
  "La migration packs doit ajouter le mode de paiement.",
);
assert.match(
  publicPackOfferMigration,
  /public_pack_code/,
  "La migration packs doit ajouter le code de pack public.",
);
assert.match(
  publicPackOfferMigration,
  /PACK-DOSSIER-1M-MENS/,
  "La migration packs doit semer la variante Dossier 1M.",
);
assert.match(
  publicPackOfferMigration,
  /PACK-PRO-12M-COMPT/,
  "La migration packs doit semer la variante Pro 12M comptant.",
);
assert.match(
  signupPackSnapshotMigration,
  /pack_selection_snapshot_json/,
  "La migration signup doit stocker le snapshot du pack choisi.",
);
assert.match(
  subscriptionPackMetadataMigration,
  /pending_cancellation/,
  "La migration subscription doit introduire pending_cancellation.",
);
assert.match(
  subscriptionPackMetadataMigration,
  /paid_cycles_count/,
  "La migration subscription doit stocker le nombre de cycles payes.",
);
assert.match(
  subscriptionPackMetadataMigration,
  /commitment_ends_at/,
  "La migration subscription doit stocker la fin d'engagement.",
);
assert.match(
  subscriptionPackMetadataMigration,
  /cancel_at_term_end/,
  "La migration subscription doit stocker la resiliation a fin de terme.",
);

assert.match(
  sharedTypes,
  /type SubscriptionStatus =/,
  "SubscriptionStatus doit etre exporte dans shared.",
);
assert.match(
  sharedTypes,
  /"pending_cancellation"/,
  "SubscriptionStatus doit inclure pending_cancellation.",
);
assert.match(
  sharedTypes,
  /interface SubscriptionSummary/,
  "SubscriptionSummary doit etre defini dans shared.",
);
assert.match(
  sharedTypes,
  /offerExternalReference: string \| null;/,
  "SubscriptionSummary doit exposer offerExternalReference.",
);
assert.match(
  sharedTypes,
  /publicPackCode: PublicPackCode \| null;/,
  "SubscriptionSummary doit exposer publicPackCode.",
);
assert.match(
  sharedTypes,
  /setupFeeAmountCents: number;/,
  "SubscriptionSummary doit exposer setupFeeAmountCents.",
);
assert.match(
  sharedTypes,
  /billingIntervalMonths: number;/,
  "SubscriptionSummary doit exposer billingIntervalMonths.",
);
assert.match(
  sharedTypes,
  /commitmentMonths: number;/,
  "SubscriptionSummary doit exposer commitmentMonths.",
);
assert.match(
  sharedTypes,
  /paymentMode: CommercialOfferPaymentMode;/,
  "SubscriptionSummary doit exposer paymentMode.",
);
assert.match(
  sharedTypes,
  /paidCyclesCount: number;/,
  "SubscriptionSummary doit exposer paidCyclesCount.",
);
assert.match(
  sharedTypes,
  /commitmentEndsAt: string \| null;/,
  "SubscriptionSummary doit exposer commitmentEndsAt.",
);
assert.match(
  sharedTypes,
  /cancelRequestedAt: string \| null;/,
  "SubscriptionSummary doit exposer cancelRequestedAt.",
);
assert.match(
  sharedTypes,
  /cancelAtTermEnd: boolean;/,
  "SubscriptionSummary doit exposer cancelAtTermEnd.",
);
assert.match(
  sharedTypes,
  /interface AdminSubscriptionDetail/,
  "AdminSubscriptionDetail doit etre defini dans shared.",
);
assert.match(
  sharedTypes,
  /interface SignupPackSelectionSnapshot/,
  "SignupPackSelectionSnapshot doit etre partage.",
);
assert.match(
  sharedTypes,
  /type CommercialOfferPaymentMode = "monthly" \| "upfront";/,
  "CommercialOfferPaymentMode doit etre partage.",
);
assert.match(
  sharedTypes,
  /type CommercialOfferBillingCadence/,
  "CommercialOfferBillingCadence doit etre exporte dans shared.",
);
assert.match(
  sharedTypes,
  /paypalPlanIdSandbox: string \| null/,
  "CommercialOfferSummary doit exposer paypalPlanIdSandbox.",
);
assert.match(
  sharedTypes,
  /paypalPlanIdLive: string \| null/,
  "CommercialOfferSummary doit exposer paypalPlanIdLive.",
);

assert.match(
  paypalWebhookLib,
  /verifyPayPalWebhookSignature/,
  "verifyPayPalWebhookSignature doit etre exporte.",
);
assert.match(
  paypalWebhookLib,
  /verify-webhook-signature/,
  "Le helper doit appeler l'endpoint verify-webhook-signature.",
);
assert.match(
  paypalWebhookLib,
  /PAYPAL_WEBHOOK_VERIFY/,
  "PAYPAL_WEBHOOK_VERIFY doit etre lu.",
);
assert.match(
  paypalWebhookLib,
  /PAYPAL_WEBHOOK_ID/,
  "PAYPAL_WEBHOOK_ID doit etre lu.",
);
assert.match(
  webhookRoute,
  /request\.text\(\)/,
  "Le webhook doit lire le body brut pour la signature.",
);
assert.match(
  webhookRoute,
  /\/internal\/webhooks\/paypal/,
  "Le webhook BFF doit forwarder vers l'endpoint interne.",
);
assert.match(
  programCs,
  /"\/internal\/webhooks\/paypal"/,
  "L'endpoint webhook PayPal doit etre declare.",
);
assert.match(
  webhookService,
  /BILLING\.SUBSCRIPTION\.ACTIVATED/,
  "Le service doit gerer ACTIVATED.",
);
assert.match(
  webhookService,
  /BILLING\.SUBSCRIPTION\.CANCELLED/,
  "Le service doit gerer CANCELLED.",
);
assert.match(
  webhookService,
  /PAYMENT\.SALE\.COMPLETED/,
  "Le service doit gerer PAYMENT.SALE.COMPLETED.",
);
assert.match(
  webhookService,
  /CreateBillingDocumentForSubscriptionAsync/,
  "Le service doit creer un document commercial pour le paiement.",
);
assert.match(
  webhookService,
  /IssueInvoiceAsync/,
  "Le service doit emettre la facture BPCE.",
);
assert.match(
  webhookService,
  /ConfirmPaymentAsync/,
  "Le service doit confirmer le paiement.",
);
assert.match(
  webhookService,
  /RecordPaymentAsync/,
  "Le service doit enregistrer le cycle paye apres webhook.",
);
assert.match(
  webhookService,
  /PaidCyclesCount == 0/,
  "Le webhook doit limiter la mise en service au premier cycle.",
);
assert.match(
  webhookService,
  /subscription\.activated/,
  "L'audit subscription.activated doit etre emis.",
);
assert.match(
  webhookService,
  /subscription\.payment_received/,
  "L'audit subscription.payment_received doit etre emis.",
);
assert.match(
  paypalCancellationWorker,
  /pending_cancellation/,
  "Le worker PayPal doit cibler les resiliations differees.",
);
assert.match(
  paypalCancellationWorker,
  /\/v1\/billing\/subscriptions\/.*\/cancel/,
  "Le worker PayPal doit annuler la souscription a echeance.",
);
assert.match(
  paypalCancellationWorker,
  /UpdateStatusAsync/,
  "Le worker PayPal doit mettre a jour le statut local.",
);

assert.match(
  paypalLib,
  /createPayPalSubscription/,
  "createPayPalSubscription helper doit exister.",
);
assert.match(
  paypalLib,
  /\/v1\/billing\/subscriptions/,
  "Le helper doit appeler /v1/billing/subscriptions.",
);
assert.match(
  paypalLib,
  /cancelPayPalSubscription/,
  "cancelPayPalSubscription helper doit exister.",
);
assert.match(
  paypalLib,
  /createPayPalProduct/,
  "createPayPalProduct helper doit exister.",
);
assert.match(
  paypalLib,
  /createPayPalPlan/,
  "createPayPalPlan helper doit exister.",
);
assert.match(
  paypalLib,
  /\/v1\/catalogs\/products/,
  "createPayPalProduct doit cibler /v1/catalogs/products.",
);
assert.match(
  paypalLib,
  /\/v1\/billing\/plans/,
  "createPayPalPlan doit cibler /v1/billing/plans.",
);
assert.match(
  paypalLib,
  /interval_count/,
  "createPayPalPlan doit parametrer interval_count.",
);
assert.match(
  paypalLib,
  /setup_fee/,
  "createPayPalPlan doit parametrer setup_fee.",
);
assert.match(
  paypalAutoPlanRoute,
  /createPayPalProduct/,
  "La route auto-plan doit appeler createPayPalProduct.",
);
assert.match(
  paypalAutoPlanRoute,
  /createPayPalPlan/,
  "La route auto-plan doit appeler createPayPalPlan.",
);
assert.match(
  paypalAutoPlanRoute,
  /PLAN_ALREADY_EXISTS/,
  "La route auto-plan doit refuser si le plan existe deja pour le mode.",
);
assert.match(
  paypalRuntimeConfigCs,
  /enum PayPalMode/,
  "PayPalRuntimeConfiguration doit definir l'enum PayPalMode.",
);
assert.match(
  paypalRuntimeConfigCs,
  /PAYPAL_MODE/,
  "PayPalConfigurationResolver doit lire PAYPAL_MODE.",
);
assert.match(
  subscribeCreateRoute,
  /billingCadence !== "monthly"/,
  "La route create doit refuser les offres non mensuelles.",
);
assert.match(
  subscribeCreateRoute,
  /paypalPlanIdLive|paypalPlanIdSandbox|activePlanId/,
  "La route create doit verifier un plan PayPal actif.",
);
assert.match(
  subscribeCreateRoute,
  /setupFeeAmountCents/,
  "La route create doit transmettre les frais de mise en service.",
);
assert.match(
  subscribeCreateRoute,
  /\/internal\/portal\/subscriptions/,
  "La route create doit persister via l'endpoint interne.",
);
assert.match(
  subscribeReturnRoute,
  /return-approved/,
  "La route return doit appeler return-approved.",
);
assert.match(
  programCs,
  /"\/internal\/portal\/subscriptions"/,
  "L'endpoint portal subscriptions doit etre declare.",
);
assert.match(
  programCs,
  /\/internal\/portal\/subscriptions\/\{id\}\/return-approved/,
  "L'endpoint return-approved doit etre declare.",
);
assert.match(
  programCs,
  /\/internal\/portal\/subscriptions\/\{id\}\/cancel/,
  "L'endpoint portal cancel doit etre declare.",
);
assert.match(
  programCs,
  /\/internal\/portal\/pending-pack-selection/,
  "L'endpoint de reprise du pack signup doit etre declare.",
);
assert.match(
  subscribeButton,
  /Souscrire/,
  "Le bouton Souscrire doit etre present.",
);
assert.match(
  servicesPage,
  /getPendingPackSelection/,
  "La page services doit reprendre la selection issue du signup.",
);
assert.match(
  servicesPage,
  /PublicPackCard/,
  "La page services doit presenter les packs grand public.",
);
assert.match(
  servicesPage,
  /getPublicCommercialCatalog/,
  "La page services doit charger le catalogue public.",
);
assert.match(
  servicesPage,
  /Finaliser mon pack|Catalogue packs|Souscrire .* pack/,
  "La page services doit expliciter la reprise et la souscription de packs.",
);
assert.match(
  clientListPage,
  /getClientSubscriptions/,
  "La page client doit charger les souscriptions.",
);
assert.match(
  clientListPage,
  /ClientCancelSubscriptionButton/,
  "La page client doit exposer la resiliation.",
);
assert.match(
  clientListPage,
  /Ajouter \/ remplacer une offre/,
  "La page client doit guider vers l'ajout ou le remplacement d'offre.",
);
assert.match(
  clientCancelButton,
  /\/api\/subscriptions\//,
  "Le bouton client cancel doit appeler la route BFF client.",
);
assert.match(
  clientCancelRoute,
  /\/internal\/portal\/subscriptions\/\$\{encodeURIComponent\(id\)\}\/cancel/,
  "La route BFF client doit forwarder vers l'endpoint interne de resiliation.",
);
assert.match(
  clientCancelRoute,
  /scheduleStripeSubscriptionCancellationAtPeriodEnd/,
  "La route BFF client doit pouvoir programmer une fin de terme Stripe.",
);
assert.match(
  clientCancelRoute,
  /pending_cancellation/,
  "La route BFF client doit reconnaitre pending_cancellation.",
);

assert.match(
  programCs,
  /"\/internal\/admin\/subscriptions"/,
  "L'endpoint admin subscriptions doit etre declare.",
);
assert.match(
  programCs,
  /\/internal\/admin\/subscriptions\/\{id\}\/cancel/,
  "L'endpoint admin cancel doit etre declare.",
);
assert.match(
  programCs,
  /\/internal\/admin\/subscriptions\/\{id\}\/provisioning\/reconcile/,
  "L'endpoint admin de relance provisioning doit etre declare.",
);
assert.match(
  programCs,
  /subscription\.admin_cancel/,
  "L'audit subscription.admin_cancel doit etre emis.",
);
assert.match(
  programCs,
  /AddHostedService<PayPalPendingCancellationWorker>/,
  "Le worker PayPal de resiliation differee doit etre enregistre.",
);
assert.match(
  adminListPage,
  /AdminSubscriptionsPage/,
  "La page admin liste doit etre declaree.",
);
assert.match(
  adminListPage,
  /Revenu mensuel equivalent/,
  "La page admin doit afficher un equivalent mensuel.",
);
assert.match(
  adminDetailPage,
  /AdminCancelSubscriptionButton/,
  "La page admin detail doit monter le bouton d'annulation.",
);
assert.match(
  adminDetailPage,
  /Factures BPCE g/,
  "La page admin detail doit afficher l'historique BPCE.",
);
assert.match(
  adminDetailPage,
  /Provisioning Active Directory/,
  "La page admin detail doit afficher la section provisioning.",
);
assert.match(
  adminDetailPage,
  /AdminReconcileProvisioningButton/,
  "La page admin detail doit permettre la relance du provisioning.",
);
assert.match(
  adminDetailPage,
  /cancelAtTermEnd|Resiliation programmee/,
  "La page admin detail doit afficher la resiliation differee.",
);
assert.match(
  cancelButton,
  /\/api\/admin\/subscriptions/,
  "Le bouton admin cancel doit appeler la route BFF.",
);
assert.match(
  adminCancelRoute,
  /cancelPayPalSubscription/,
  "La route BFF admin doit pouvoir annuler PayPal.",
);
assert.match(
  adminCancelRoute,
  /cancelStripeSubscription/,
  "La route BFF admin doit pouvoir annuler Stripe.",
);
assert.match(
  adminCancelRoute,
  /scheduleStripeSubscriptionCancellationAtPeriodEnd/,
  "La route BFF admin doit pouvoir programmer une fin de terme Stripe.",
);
assert.match(
  reconcileButton,
  /\/api\/admin\/subscriptions\/.*provisioning\/reconcile/,
  "Le bouton admin de relance doit appeler la route BFF dediee.",
);
assert.match(
  adminReconcileRoute,
  /\/internal\/admin\/subscriptions\/\$\{encodeURIComponent\(id\)\}\/provisioning\/reconcile/,
  "La route BFF admin de relance doit forwarder vers l'endpoint interne.",
);
assert.match(
  adminNav,
  /\/admin\/subscriptions/,
  "Le lien Abonnements doit etre dans la navigation admin.",
);

assert.match(
  catalogForm,
  /billingCadence/,
  "Le formulaire catalogue doit gerer billingCadence.",
);
assert.match(
  catalogForm,
  /paypalPlanIdSandbox/,
  "Le formulaire catalogue doit gerer paypalPlanIdSandbox.",
);
assert.match(
  catalogForm,
  /paypalPlanIdLive/,
  "Le formulaire catalogue doit gerer paypalPlanIdLive.",
);
assert.match(
  catalogForm,
  /setupFeeAmountCents/,
  "Le formulaire catalogue doit conserver les frais de mise en service.",
);
assert.match(
  catalogForm,
  /billingIntervalMonths/,
  "Le formulaire catalogue doit conserver l'intervalle de facturation.",
);
assert.match(
  catalogForm,
  /commitmentMonths/,
  "Le formulaire catalogue doit conserver la duree d'engagement.",
);
assert.match(
  catalogForm,
  /paymentMode/,
  "Le formulaire catalogue doit conserver le mode de paiement.",
);
assert.match(
  catalogForm,
  /publicPackCode/,
  "Le formulaire catalogue doit conserver le code du pack public.",
);
assert.match(
  catalogForm,
  /plan PayPal/,
  "Le formulaire doit exposer le bouton de creation du plan PayPal.",
);
assert.match(
  adminCatalogPage,
  /formatCommitmentMonths|publicPackCode/,
  "La liste catalogue doit afficher les metadonnees pack.",
);
assert.match(
  adminCatalogDetailPage,
  /paypalPlanIdSandbox/,
  "La fiche catalogue detail doit afficher l'id sandbox.",
);
assert.match(
  adminCatalogDetailPage,
  /paypalPlanIdLive/,
  "La fiche catalogue detail doit afficher l'id live.",
);
assert.match(
  adminCatalogDetailPage,
  /formatBillingIntervalMonths|publicPackCode/,
  "La fiche catalogue detail doit afficher les metadonnees pack.",
);

assert.match(
  subscriptionContracts,
  /AdminSubscriptionDetail/,
  "AdminSubscriptionDetail doit etre dans les contracts C#.",
);
assert.match(
  subscriptionService,
  /AdminCancelAsync/,
  "AdminCancelAsync doit etre defini.",
);
assert.match(
  subscriptionService,
  /ClientCancelAsync/,
  "ClientCancelAsync doit etre defini.",
);
assert.match(
  subscriptionService,
  /MarkAsPendingActivationAsync/,
  "MarkAsPendingActivationAsync doit etre defini.",
);
assert.match(
  subscriptionService,
  /ReconcileProvisioningAsync/,
  "ReconcileProvisioningAsync doit etre defini.",
);
assert.match(
  subscriptionService,
  /RecordPaymentAsync/,
  "RecordPaymentAsync doit etre defini.",
);
assert.match(
  subscriptionRepoMaria,
  /ActivateAsync/,
  "Maria repo doit avoir ActivateAsync.",
);
assert.match(
  subscriptionRepoMaria,
  /GetByExternalIdAsync/,
  "Maria repo doit avoir GetByExternalIdAsync.",
);
assert.match(
  subscriptionRepoMaria,
  /RecordPaymentAsync/,
  "Maria repo doit avoir RecordPaymentAsync.",
);
assert.match(
  subscriptionRepoMaria,
  /RequestCancellationAsync/,
  "Maria repo doit avoir RequestCancellationAsync.",
);
assert.match(
  commercialRepoMaria,
  /CreateBillingDocumentForSubscriptionAsync/,
  "Maria repo doit avoir CreateBillingDocumentForSubscriptionAsync.",
);
assert.match(
  commercialRepoMaria,
  /GetDocumentsForSubscriptionAsync/,
  "Maria repo doit avoir GetDocumentsForSubscriptionAsync.",
);
assert.match(
  internalApi,
  /getClientSubscriptions/,
  "getClientSubscriptions doit etre exporte.",
);
assert.match(
  internalApi,
  /getAdminSubscriptions/,
  "getAdminSubscriptions doit etre exporte.",
);
assert.match(
  internalApi,
  /getAdminSubscription\b/,
  "getAdminSubscription doit etre exporte.",
);
assert.match(
  internalApi,
  /getPendingPackSelection/,
  "getPendingPackSelection doit etre exporte.",
);

assert.match(
  envExample,
  /AD_REQUIRED_OU_ROOT=/,
  "AD_REQUIRED_OU_ROOT doit etre documente dans .env.example.",
);
assert.match(
  envExample,
  /AD_ALLOWED_ROOTS=/,
  "AD_ALLOWED_ROOTS doit etre documente dans .env.example.",
);
assert.match(
  envExample,
  /SUBSCRIPTION_PROVISIONING_GROUPS__ACCES-RDS=GG_RDS/,
  "Le mapping RDS doit etre documente dans .env.example.",
);
assert.match(
  envExample,
  /PACK-BUREAU-1M-MENS/,
  "Le fichier d'environnement d'exemple doit documenter les packs publics.",
);
assert.match(
  envExample,
  /AD_PROVISIONING_GROUP_DNS__GG_RDS=/,
  "La whitelist DN des groupes AD doit etre documentee dans .env.example.",
);
assert.match(
  envExample,
  /PAYPAL_MODE=sandbox/,
  "PAYPAL_MODE doit etre documente dans .env.example.",
);
assert.match(
  envExample,
  /PAYPAL_WEBHOOK_ID=/,
  "PAYPAL_WEBHOOK_ID doit etre documente dans .env.example.",
);
assert.match(
  envExample,
  /PAYPAL_WEBHOOK_VERIFY=/,
  "PAYPAL_WEBHOOK_VERIFY doit etre documente dans .env.example.",
);

console.log("Verification du contrat souscriptions v0.32 reussie.");
