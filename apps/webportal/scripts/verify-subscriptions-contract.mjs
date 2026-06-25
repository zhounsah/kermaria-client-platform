import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

// Webportal artefacts
const sharedTypes = await read("../../packages/shared/src/index.ts");
const internalApi = await read("lib/internal-api.ts");
const paypalLib = await read("lib/paypal.ts");
const paypalWebhookLib = await read("lib/paypal-webhook.ts");
const subscribeCreateRoute = await read(
  "app/api/subscriptions/create/route.ts",
);
const subscribeReturnRoute = await read(
  "app/api/subscriptions/return/route.ts",
);
const webhookRoute = await read("app/api/webhooks/paypal/route.ts");
const adminCancelRoute = await read(
  "app/api/admin/subscriptions/[id]/cancel/route.ts",
);
const adminListPage = await read("app/admin/subscriptions/page.tsx");
const adminDetailPage = await read("app/admin/subscriptions/[id]/page.tsx");
const clientListPage = await read("app/profile/subscriptions/page.tsx");
const subscribeButton = await read("components/SubscribeButton.tsx");
const cancelButton = await read(
  "components/AdminCancelSubscriptionButton.tsx",
);
const adminNav = await read("components/AdminNavigation.tsx");
const catalogForm = await read("components/AdminCatalogOfferForm.tsx");
const adminCatalogPage = await read("app/admin/catalog/page.tsx");
const servicesPage = await read("app/services/page.tsx");

// Repo-level artefacts
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

// --- Schema ---
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

// --- Shared types ---
assert.match(
  sharedTypes,
  /type SubscriptionStatus =/,
  "SubscriptionStatus doit etre exporte dans shared.",
);
assert.match(
  sharedTypes,
  /interface SubscriptionSummary/,
  "SubscriptionSummary doit etre defini dans shared.",
);
assert.match(
  sharedTypes,
  /interface AdminSubscriptionDetail/,
  "AdminSubscriptionDetail doit etre defini dans shared.",
);
assert.match(
  sharedTypes,
  /type CommercialOfferBillingCadence/,
  "CommercialOfferBillingCadence doit etre exporte dans shared.",
);

// --- Webhook ---
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
  "Le webhook doit lire le body brut (text) pour la signature.",
);
assert.match(
  webhookRoute,
  /\/internal\/webhooks\/paypal/,
  "Le webhook BFF doit forwarder vers l'endpoint interne.",
);
assert.match(
  programCs,
  /"\/internal\/webhooks\/paypal"/,
  "L'endpoint API-INTERNAL webhook doit etre declare.",
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
  /CreateBillingDocumentFromOfferAsync/,
  "Le service doit creer un commercial_document pour le paiement.",
);
assert.match(
  webhookService,
  /IssueInvoiceAsync/,
  "Le service doit emettre la facture BPCE.",
);
assert.match(
  webhookService,
  /ConfirmPaymentAsync/,
  "Le service doit confirmer le paiement (mark_as_paid + email).",
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

// --- Subscribe flow ---
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
  subscribeCreateRoute,
  /billingCadence !== "monthly"/,
  "La route create doit refuser les offres non mensuelles.",
);
assert.match(
  subscribeCreateRoute,
  /paypalPlanId/,
  "La route create doit verifier paypalPlanId.",
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
  subscribeButton,
  /Souscrire à/,
  "Le bouton Souscrire doit etre present.",
);
assert.match(
  servicesPage,
  /SubscribeButton/,
  "La page services doit monter SubscribeButton.",
);
assert.match(
  servicesPage,
  /billingCadence === "monthly"/,
  "La page services doit conditionner le bouton sur la cadence.",
);
assert.match(
  clientListPage,
  /getClientSubscriptions/,
  "La page client doit charger les souscriptions.",
);

// --- Admin flow ---
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
  /subscription\.admin_cancel/,
  "L'audit subscription.admin_cancel doit etre emis.",
);
assert.match(
  adminListPage,
  /AdminSubscriptionsPage/,
  "La page admin liste doit etre declaree.",
);
assert.match(
  adminListPage,
  /MRR/,
  "La page admin doit afficher MRR.",
);
assert.match(
  adminDetailPage,
  /AdminCancelSubscriptionButton/,
  "La page admin detail doit monter le bouton d'annulation.",
);
assert.match(
  adminDetailPage,
  /Factures BPCE générées/,
  "La page admin detail doit afficher l'historique BPCE.",
);
assert.match(
  cancelButton,
  /\/api\/admin\/subscriptions/,
  "Le bouton admin cancel doit appeler la route BFF.",
);
assert.match(
  adminCancelRoute,
  /cancelPayPalSubscription/,
  "La route BFF doit appeler cancelPayPalSubscription chez PayPal.",
);
assert.match(
  adminNav,
  /\/admin\/subscriptions/,
  "Le lien Abonnements doit etre dans la navigation admin.",
);

// --- Catalog admin form ---
assert.match(
  catalogForm,
  /billingCadence/,
  "Le formulaire catalogue doit gerer billingCadence.",
);
assert.match(
  catalogForm,
  /paypalPlanId/,
  "Le formulaire catalogue doit gerer paypalPlanId.",
);
assert.match(
  adminCatalogPage,
  /billingCadence/,
  "La page catalogue doit afficher la cadence.",
);

// --- Service+repo wiring ---
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
  /MarkAsPendingActivationAsync/,
  "MarkAsPendingActivationAsync doit etre defini.",
);
assert.match(
  subscriptionRepoMaria,
  /ActivateAsync/,
  "Maria repo doit avoir ActivateAsync.",
);
assert.match(
  subscriptionRepoMaria,
  /GetByPayPalIdAsync/,
  "Maria repo doit avoir GetByPayPalIdAsync.",
);
assert.match(
  commercialRepoMaria,
  /CreateBillingDocumentFromOfferAsync/,
  "Maria repo doit avoir CreateBillingDocumentFromOfferAsync.",
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

// --- Env vars ---
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

console.log("Vérification du contrat souscriptions V0.22 réussie.");
