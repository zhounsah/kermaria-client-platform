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
const checkoutContracts = await read(
  "../../apps/api-internal/Contracts/CheckoutContracts.cs",
);
const recurringCheckoutService = await read(
  "../../apps/api-internal/Services/RecurringCheckoutService.cs",
);
const recurringCheckoutMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/029_billed_recurring_checkout.sql",
);
const recurringOfferCadenceBackfillMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/030_recurring_offer_cadence_backfill.sql",
);
const paypalAutoPlanRoute = await read(
  "app/api/admin/catalog/[id]/paypal-plan/route.ts",
);
const paypalRuntimeConfigCs = await read(
  "../../apps/api-internal/Data/Configuration/PayPalRuntimeConfiguration.cs",
);
const checkoutSummaryRoute = await read("app/api/checkout/summary/route.ts");
const recurringCheckoutItemsRoute = await read(
  "app/api/checkout/subscriptions/items/route.ts",
);
const recurringCheckoutRemoveRoute = await read(
  "app/api/checkout/subscriptions/items/remove/route.ts",
);
const recurringCheckoutConfirmRoute = await read(
  "app/api/checkout/subscriptions/confirm/route.ts",
);
const addRecurringCheckoutButton = await read(
  "components/AddRecurringCheckoutButton.tsx",
);
const recurringCheckoutConfirmButton = await read(
  "components/RecurringCheckoutConfirmButton.tsx",
);
const publicPackCard = await read("components/PublicPackCard.tsx");

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
  recurringCheckoutMigration,
  /ENUM\('paypal','stripe','billing'\)/,
  "La migration billed checkout doit ajouter le rail billing.",
);
assert.match(
  recurringCheckoutMigration,
  /pending_payment/,
  "La migration billed checkout doit ajouter pending_payment.",
);
assert.match(
  recurringCheckoutMigration,
  /CREATE TABLE IF NOT EXISTS recurring_checkout_items/i,
  "La migration billed checkout doit creer recurring_checkout_items.",
);
assert.match(
  recurringCheckoutMigration,
  /CREATE TABLE IF NOT EXISTS commercial_document_line_subscriptions/i,
  "La migration billed checkout doit creer le lien ligne-document-souscription.",
);
assert.match(
  recurringOfferCadenceBackfillMigration,
  /billing_cadence = 'monthly'/,
  "Un backfill doit remettre en monthly les offres recurrentes creees avant billing_cadence.",
);
assert.match(
  recurringOfferCadenceBackfillMigration,
  /ACCES-VPN[\s\S]*SAVE-PERSO[\s\S]*SUPERV-SERVICE/,
  "Le backfill doit couvrir les references historiques recurrentes du catalogue.",
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
  /"pending_payment"/,
  "SubscriptionStatus doit inclure pending_payment.",
);
assert.match(
  sharedTypes,
  /type PaymentRail = "paypal" \| "stripe" \| "billing";/,
  "PaymentRail doit inclure billing.",
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
  /fiscalRegime: FiscalRegime;/,
  "SubscriptionSummary doit exposer fiscalRegime.",
);
assert.match(
  sharedTypes,
  /fiscalMention: string;/,
  "SubscriptionSummary doit exposer fiscalMention.",
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
  /interface CheckoutSummary/,
  "CheckoutSummary doit etre partage pour le recap unifie.",
);
assert.match(
  sharedTypes,
  /interface RecurringCheckoutItem/,
  "RecurringCheckoutItem doit etre partage.",
);
assert.match(
  sharedTypes,
  /interface CheckoutRecurringConfirmResponse/,
  "CheckoutRecurringConfirmResponse doit etre partage.",
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
  programCs,
  /"\/internal\/portal\/checkout\/summary"/,
  "L'endpoint checkout summary doit etre declare.",
);
assert.match(
  programCs,
  /"\/internal\/portal\/checkout\/subscriptions\/items"/,
  "L'endpoint d'ajout recurring checkout doit etre declare.",
);
assert.match(
  programCs,
  /"\/internal\/portal\/checkout\/subscriptions\/items\/remove"/,
  "L'endpoint de suppression recurring checkout doit etre declare.",
);
assert.match(
  programCs,
  /"\/internal\/portal\/checkout\/subscriptions\/confirm"/,
  "L'endpoint de confirmation recurring checkout doit etre declare.",
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
  checkoutSummaryRoute,
  /\/internal\/portal\/checkout\/summary/,
  "La route BFF checkout summary doit forwarder vers l'API interne.",
);
assert.match(
  recurringCheckoutItemsRoute,
  /\/internal\/portal\/checkout\/subscriptions\/items/,
  "La route BFF d'ajout recurring checkout doit forwarder vers l'API interne.",
);
assert.match(
  recurringCheckoutRemoveRoute,
  /\/internal\/portal\/checkout\/subscriptions\/items\/remove/,
  "La route BFF de suppression recurring checkout doit forwarder vers l'API interne.",
);
assert.match(
  recurringCheckoutConfirmRoute,
  /\/internal\/portal\/checkout\/subscriptions\/confirm/,
  "La route BFF de confirmation recurring checkout doit forwarder vers l'API interne.",
);
assert.match(
  publicPackCard,
  /AddRecurringCheckoutButton/,
  "La carte pack doit proposer l'ajout au recurring checkout en mode souscription.",
);
assert.match(
  addRecurringCheckoutButton,
  /\/api\/checkout\/subscriptions\/items/,
  "Le bouton d'ajout recurring checkout doit appeler la route BFF dediee.",
);
assert.match(
  recurringCheckoutConfirmButton,
  /\/api\/checkout\/subscriptions\/confirm/,
  "Le bouton de confirmation recurring checkout doit appeler la route BFF dediee.",
);
assert.match(
  clientListPage,
  /getClientSubscriptions/,
  "La page client doit charger les souscriptions.",
);
assert.match(
  clientListPage,
  /formatSubscriptionRailLabel|Facture locale/,
  "La page client doit afficher le rail billing/facture locale.",
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
  clientCancelRoute,
  /subscription\.billingSystem === "billing_v2"[\s\S]*BILLING_V2_CANCELLATION_NOT_AVAILABLE[\s\S]*Aucune action de paiement n'a été déclenchée[\s\S]*pending_cancellation/,
  "La route BFF client doit bloquer explicitement une resiliation V2 avant tout chemin legacy.",
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
  /Revenu mensuel équivalent/,
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
  /Provisionn?ing Active Directory/,
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
  adminCancelRoute,
  /subscription\.billingSystem === "billing_v2"[\s\S]*BILLING_V2_CANCELLATION_NOT_AVAILABLE[\s\S]*Aucune action de paiement n'a été déclenchée[\s\S]*pending_cancellation/,
  "La route BFF admin doit bloquer explicitement une resiliation V2 avant tout chemin legacy.",
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
  subscriptionService,
  /CreateBilledPendingAsync/,
  "CreateBilledPendingAsync doit exister pour les souscriptions facturees.",
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
  checkoutContracts,
  /record CheckoutSummaryResponse/,
  "Les contrats C# doivent exposer CheckoutSummaryResponse.",
);
assert.match(
  checkoutContracts,
  /record CheckoutRecurringConfirmResponse/,
  "Les contrats C# doivent exposer CheckoutRecurringConfirmResponse.",
);
assert.match(
  recurringCheckoutService,
  /CreateBilledPendingAsync/,
  "RecurringCheckoutService doit creer les souscriptions facturees avant paiement.",
);
assert.match(
  recurringCheckoutService,
  /CreateRecurringCheckoutDocumentAsync/,
  "RecurringCheckoutService doit creer une facture initiale groupee.",
);
assert.match(
  recurringCheckoutService,
  /CreateBilledPendingAsync/,
  "RecurringCheckoutService doit creer les souscriptions locales avant paiement.",
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

/**
 * Decoupe une ou plusieurs routes a leur frontiere reelle.
 *
 * Un decompte fixe de caracteres glisse dans la route suivante des que le
 * fichier bouge — ne serait-ce qu'en changeant de fin de ligne — et
 * l'assertion se met alors a parler d'autre chose que de la route visee.
 */
function routeSection(source, marker, routeCount = 1) {
  const start = source.indexOf(marker);
  assert.notEqual(start, -1, `Route introuvable dans Program.cs : ${marker}`);
  let end = start;
  for (let index = 0; index < routeCount; index += 1) {
    const next = source.indexOf("\napp.Map", end + 1);
    end = next === -1 ? source.length : next;
  }
  return source.slice(start, end);
}

// ----------------------------------------------------------------------
// Utilisateurs supplementaires Billing V2 (Phase 4, cablage produit)
// ----------------------------------------------------------------------

const additionalUsersPage = await read(
  "app/profile/subscriptions/[id]/users/page.tsx",
);
const additionalUsersManager = await read(
  "components/BillingV2AdditionalUsersManager.tsx",
);
const additionalUsersAssignRoute = await read(
  "app/api/subscriptions/[id]/users/[userId]/assign/route.ts",
);
const additionalUsersResendRoute = await read(
  "app/api/subscriptions/[id]/users/[userId]/resend-invitation/route.ts",
);
const billingV2PortalProjection = await read(
  "../../apps/api-internal/Services/BillingV2PortalSubscriptionProjection.cs",
);
const additionalUserRepository = await read(
  "../../apps/api-internal/Data/Repositories/"
  + "MariaDbBillingV2AdditionalUserIdentityRepository.cs",
);

assert.match(
  sharedTypes,
  /additionalUserSlotsCount\?: number;/,
  "Le contrat partage doit porter le nombre de places utilisateur.",
);
assert.match(
  sharedTypes,
  /assignedAdditionalUsersCount\?: number;/,
  "Le contrat partage doit porter le nombre de places attribuees.",
);
assert.match(
  subscriptionContracts,
  /"additionalUserSlotsCount"/,
  "SubscriptionSummary doit exposer additionalUserSlotsCount.",
);
assert.match(
  subscriptionContracts,
  /"assignedAdditionalUsersCount"/,
  "SubscriptionSummary doit exposer assignedAdditionalUsersCount.",
);

// Les compteurs passent par des sous-requetes scalaires : une jointure sur
// billing_v2_subscription_users multiplierait les lignes financieres de la
// projection, et donc les montants lus par l'espace client.
for (const alias of [
  "AS additional_user_slots_count",
  "AS assigned_additional_users_count",
]) {
  assert.ok(
    billingV2PortalProjection.includes(alias),
    `La projection portail doit exposer ${alias}.`,
  );
}
assert.ok(
  !/JOIN\s+billing_v2_subscription_users/i.test(billingV2PortalProjection),
  "Les compteurs ne doivent jamais etre obtenus par une jointure : elle "
  + "dupliquerait les lignes financieres de la souscription.",
);
assert.ok(
  billingV2PortalProjection.includes("UserSlotEntitlementSource"),
  "Les compteurs doivent reutiliser la definition unique d'une place "
  + "utilisateur supplementaire, pas une variante locale.",
);
// Le droit contractuel ne suffit pas : compter une place resiliee, ou les
// places d'un abonnement resilie, annoncerait a l'ecran des places que la
// politique d'attribution refuse ensuite systematiquement.
assert.ok(
  billingV2PortalProjection.includes("AdministrableSlotPredicate"),
  "Les compteurs doivent porter le predicat d'administrabilite partage.",
);
assert.equal(
  (billingV2PortalProjection.match(/AdministrableSlotPredicate/g) ?? []).length,
  4,
  "Les quatre compteurs — deux dans la projection client, deux dans la "
  + "projection admin — portent le predicat.",
);
assert.match(
  additionalUserRepository,
  /internal const string AdministrableSlotPredicate/,
  "Le predicat d'administrabilite est defini une seule fois, aupres de la "
  + "definition d'une place utilisateur supplementaire.",
);
for (const predicate of [
  "AND slot.is_primary = 0",
  "AND slot.status = 'active'",
  "AND subscription.status = 'active'",
]) {
  assert.ok(
    additionalUserRepository.includes(predicate),
    `Le predicat partage doit imposer « ${predicate} ».`,
  );
}
// La lecture produit s'appuie sur le meme predicat que les compteurs : deux
// clauses ecrites separement finiraient par diverger, et l'ecran listerait
// des places que les compteurs ignorent.
assert.match(
  additionalUserRepository,
  /\+ AdministrableSlotPredicate/,
  "La lecture des places doit consommer le predicat partage.",
);

assert.match(
  clientListPage,
  /Utilisateurs supplémentaires/,
  "La liste des souscriptions doit annoncer les utilisateurs supplementaires.",
);
assert.match(
  clientListPage,
  /additionalUserSlots > 0 \? \(/,
  "Le renvoi ne s'affiche que si la souscription ouvre au moins une place.",
);
assert.match(
  clientListPage,
  /\/profile\/subscriptions\/\$\{encodeURIComponent\(item\.id\)\}\/users/,
  "Le lien « Gerer les utilisateurs » doit pointer vers l'ecran dedie.",
);
assert.match(
  clientListPage,
  /Gérer les utilisateurs/,
  "Le libelle du lien doit rester explicite.",
);

assert.match(
  additionalUsersPage,
  /await requireClientSession\(\)/,
  "L'ecran des utilisateurs supplementaires exige une session client.",
);
assert.match(
  additionalUsersPage,
  /getBillingV2AdditionalUsers\(/,
  "Le chargement passe par internal-api.ts, pas par un fetch maison.",
);
assert.ok(
  !additionalUsersPage.includes("fetch("),
  "La page ne doit contenir aucun appel reseau direct.",
);
assert.match(
  internalApi,
  /\/internal\/portal\/billing-v2\/subscriptions\/\$\{encodeURIComponent\(subscriptionId\)\}\/users/,
  "La lecture des places passe par la route portail dediee.",
);

for (const [status, label] of [
  ["available", "À attribuer"],
  ["invited", "Invitation envoyée"],
  ["activating", "Activation en cours"],
  ["active", "Activé"],
  ["attention", "Activation à finaliser"],
  ["disabled", "Désactivé"],
]) {
  assert.ok(
    additionalUsersManager.includes(`${status}: { label: "${label}"`),
    `L'etat ${status} doit etre presente « ${label} ».`,
  );
}
assert.ok(
  !/Désactiver/.test(additionalUsersManager),
  "Aucune action de desactivation n'est proposee au client.",
);
assert.ok(
  !/disable/i.test(additionalUsersManager.replace(/disabled/g, "")),
  "L'ecran n'appelle jamais la desactivation d'une place.",
);
assert.match(
  additionalUsersManager,
  /Ajouter un utilisateur/,
  "Une place libre propose l'attribution.",
);
assert.match(
  additionalUsersManager,
  /Renvoyer l'invitation/,
  "Une place invitee propose le renvoi de l'invitation.",
);
assert.match(
  additionalUsersManager,
  /router\.refresh\(\)/,
  "Une mutation reussie doit relire l'etat serveur.",
);
// Le contrat reel de l'attribution : reduire le formulaire produirait une
// fiche d'identite incomplete que personne ne reviendrait completer.
for (const field of [
  "email",
  "displayName",
  "personalTitle",
  "givenName",
  "surname",
  "birthDate",
  "initials",
  "phone",
]) {
  assert.ok(
    additionalUsersManager.includes(`name="${field}"`),
    `Le formulaire d'attribution doit porter le champ ${field}.`,
  );
}
// Controle sur le code effectif : les commentaires expliquent justement
// pourquoi ces notions restent internes, et les compter comme des fuites
// ferait supprimer l'explication plutot que le risque.
const managerCode = additionalUsersManager
  .replace(/\/\*[\s\S]*?\*\//g, "")
  .replace(/^\s*\/\/.*$/gm, "");
for (const forbidden of ["koxo", "objectGuid", "employeeNumber", "failureCode"]) {
  assert.ok(
    !managerCode.toLowerCase().includes(forbidden.toLowerCase()),
    `L'ecran client ne doit jamais montrer ${forbidden}.`,
  );
}

// Meme lecture que ci-dessus : ces routes expliquent en commentaire
// pourquoi elles refusent un client fourni par le navigateur.
const stripComments = (source) => source
  .replace(/\/\*[\s\S]*?\*\//g, "")
  .replace(/^\s*\/\/.*$/gm, "");

for (const [route, name] of [
  [stripComments(additionalUsersAssignRoute), "assign"],
  [stripComments(additionalUsersResendRoute), "resend-invitation"],
]) {
  assert.match(
    route,
    /handlePortalPayloadMutationTyped/,
    `La route BFF ${name} doit reutiliser portal-bff.ts.`,
  );
  assert.match(
    route,
    /isValidPortalIdentifier\(id\)\s*\|\|\s*!isValidPortalIdentifier\(userId\)/,
    `La route BFF ${name} doit valider les deux identifiants.`,
  );
  assert.ok(
    !route.includes("fetch("),
    `La route BFF ${name} ne doit pas refaire un appel reseau maison.`,
  );
  assert.ok(
    !/customerId/i.test(route),
    `La route BFF ${name} ne doit jamais accepter ni relayer un client `
    + "fourni par le navigateur.",
  );
  assert.ok(
    !/actorReference|portalUserId/i.test(route),
    `La route BFF ${name} ne doit jamais relayer d'acteur navigateur.`,
  );
}
assert.ok(
  !/\.\.\.(candidate|payload|body)/.test(additionalUsersAssignRoute),
  "Le corps recu n'est jamais relaye par etalement : un champ non prevu "
  + "passerait tel quel a l'API.",
);

for (const route of [
  '"/internal/portal/billing-v2/subscriptions/{subscriptionId}/users"',
  '"/internal/portal/billing-v2/subscriptions/{subscriptionId}/users/{subscriptionUserId}/assign"',
  '"/internal/portal/billing-v2/subscriptions/{subscriptionId}/users/{subscriptionUserId}/resend-invitation"',
]) {
  assert.ok(
    programCs.includes(route),
    `API-INTERNAL doit exposer la route ${route}.`,
  );
}
// Les trois routes portail, prises a leurs frontieres reelles.
const additionalUsersRoutesSection = routeSection(
  programCs,
  '"/internal/portal/billing-v2/subscriptions/{subscriptionId}/users"',
  3,
);
assert.ok(
  !/customerId\s*[,)]/.test(
    additionalUsersRoutesSection.replace(/session\.CustomerId/g, ""),
  ),
  "Les routes portail ne prennent jamais un client en parametre : il vient "
  + "de la session.",
);
assert.ok(
  (additionalUsersRoutesSection.match(/session\.CustomerId/g) ?? []).length >= 3,
  "Chaque route portail borne son traitement au client de la session.",
);
assert.ok(
  additionalUsersRoutesSection.includes("session.UserId"),
  "L'acteur audite est celui de la session.",
);
assert.ok(
  !additionalUsersRoutesSection.includes("TryMaterializeAsync"),
  "La materialisation reste interne : elle n'est jamais declenchee par le "
  + "navigateur.",
);
assert.ok(
  !additionalUsersRoutesSection.includes("DisableAsync"),
  "La desactivation n'est pas exposee au parcours client.",
);

console.log("Verification du contrat souscriptions v0.32 reussie.");
