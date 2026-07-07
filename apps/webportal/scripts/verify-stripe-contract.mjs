import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

// Webportal artefacts
const sharedTypes = await read("../../packages/shared/src/index.ts");
const stripeLib = await read("lib/stripe.ts");
const stripeWebhookLib = await read("lib/stripe-webhook.ts");
const runtimeConfig = await read("lib/runtime-config.ts");
const createIntentRoute = await read(
  "app/api/payments/stripe/create-intent/route.ts",
);
const paymentReturnRoute = await read("app/api/payments/stripe/return/route.ts");
const webhookRoute = await read("app/api/webhooks/stripe/route.ts");
const subscribeCreateRoute = await read(
  "app/api/subscriptions/create/route.ts",
);
const subscribeStripeReturnRoute = await read(
  "app/api/subscriptions/stripe/return/route.ts",
);
const clientCancelRoute = await read(
  "app/api/subscriptions/[id]/cancel/route.ts",
);
const adminCancelRoute = await read(
  "app/api/admin/subscriptions/[id]/cancel/route.ts",
);
const stripePriceRoute = await read(
  "app/api/admin/catalog/[id]/stripe-price/route.ts",
);
const payButton = await read("components/PayButton.tsx");
const subscribeButton = await read("components/SubscribeButton.tsx");
const catalogForm = await read("components/AdminCatalogOfferForm.tsx");
const adminPaymentsPage = await read("app/admin/payments/page.tsx");
const adminSubscriptionsPage = await read("app/admin/subscriptions/page.tsx");
const servicesPage = await read("app/services/page.tsx");
const commercialDocumentPage = await read(
  "app/commercial-documents/[id]/page.tsx",
);

// Repo-level artefacts
const envExample = await read("../../.env.example");
const programCs = await read("../../apps/api-internal/Program.cs");
const stripeConfigCs = await read(
  "../../apps/api-internal/Data/Configuration/StripeRuntimeConfiguration.cs",
);
const runtimeValidatorCs = await read(
  "../../apps/api-internal/Data/Configuration/RuntimeConfigurationValidator.cs",
);
const subscriptionContracts = await read(
  "../../apps/api-internal/Contracts/SubscriptionContracts.cs",
);
const commercialContracts = await read(
  "../../apps/api-internal/Contracts/CommercialContracts.cs",
);
const stripeWebhookService = await read(
  "../../apps/api-internal/Services/StripeWebhookService.cs",
);
const invoiceIssuingService = await read(
  "../../apps/api-internal/Services/InvoiceIssuingService.cs",
);
const subscriptionRepoMaria = await read(
  "../../apps/api-internal/Data/Repositories/MariaDbSubscriptionRepository.cs",
);
const commercialRepoMaria = await read(
  "../../apps/api-internal/Data/Repositories/MariaDbCommercialRepository.cs",
);
const webhookEventsMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/017_stripe_webhook_events.sql",
);
const subscriptionsRailMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/018_subscriptions_stripe_rail.sql",
);
const offersPaymentMethodMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/019_stripe_offers_and_payment_method.sql",
);

// --- Schema ---
assert.match(
  webhookEventsMigration,
  /CREATE TABLE.+stripe_webhook_events/i,
  "La table stripe_webhook_events doit etre creee.",
);
assert.match(
  webhookEventsMigration,
  /UNIQUE KEY ux_stripe_webhook_events_event_id \(event_id\)/,
  "event_id doit etre UNIQUE pour l'idempotence.",
);
assert.match(
  subscriptionsRailMigration,
  /ADD COLUMN IF NOT EXISTS rail ENUM\('paypal','stripe'\)/,
  "subscriptions.rail doit etre ajoute.",
);
assert.match(
  subscriptionsRailMigration,
  /stripe_subscription_id VARCHAR\(64\)/,
  "subscriptions.stripe_subscription_id doit etre ajoute.",
);
assert.match(
  offersPaymentMethodMigration,
  /stripe_price_id_test VARCHAR\(64\)/,
  "commercial_offers.stripe_price_id_test doit etre ajoute.",
);
assert.match(
  offersPaymentMethodMigration,
  /stripe_price_id_live VARCHAR\(64\)/,
  "commercial_offers.stripe_price_id_live doit etre ajoute.",
);
assert.match(
  offersPaymentMethodMigration,
  /payment_method ENUM\('paypal','stripe','manual'\)/,
  "commercial_documents.payment_method doit etre ajoute.",
);

// --- C# config ---
assert.match(
  stripeConfigCs,
  /enum StripeMode\s*\{\s*Disabled,\s*Test,\s*Live/,
  "StripeMode doit avoir 3 etats Disabled/Test/Live.",
);
assert.match(
  runtimeValidatorCs,
  /STRIPE_MODE/,
  "RuntimeConfigurationValidator doit lire STRIPE_MODE.",
);
assert.match(
  runtimeValidatorCs,
  /STRIPE_WEBHOOK_SECRET/,
  "Le garde-fou live doit exiger STRIPE_WEBHOOK_SECRET.",
);

// --- C# contracts ---
assert.match(
  subscriptionContracts,
  /string Rail,/,
  "SubscriptionSummary doit exposer Rail.",
);
assert.match(
  subscriptionContracts,
  /\[property: JsonPropertyName\("stripeSubscriptionId"\)\]/,
  "StripeSubscriptionId doit etre annote JsonPropertyName.",
);
assert.match(
  commercialContracts,
  /\[property: JsonPropertyName\("stripePriceIdTest"\)\]/,
  "CommercialOfferSummary doit exposer stripePriceIdTest.",
);
assert.match(
  commercialContracts,
  /string\? PaymentMethod\)/,
  "CommercialDocumentSummary doit exposer PaymentMethod.",
);
assert.match(
  commercialContracts,
  /record PaymentConfirmPayload\(string\? PaymentMethod\)/,
  "PaymentConfirmPayload doit etre defini.",
);

// --- C# services / repositories ---
assert.match(
  stripeWebhookService,
  /"payment_intent.succeeded"/,
  "StripeWebhookService doit gerer payment_intent.succeeded.",
);
assert.match(
  stripeWebhookService,
  /"invoice.paid"/,
  "StripeWebhookService doit gerer invoice.paid.",
);
assert.match(
  stripeWebhookService,
  /"invoice.payment_succeeded"/,
  "StripeWebhookService doit gerer invoice.payment_succeeded.",
);
assert.match(
  stripeWebhookService,
  /"customer.subscription.deleted"/,
  "StripeWebhookService doit gerer customer.subscription.deleted.",
);
assert.match(
  invoiceIssuingService,
  /ConfirmPaymentAsync\(\s*string documentId,\s*string correlationId,\s*string paymentMethod,/,
  "ConfirmPaymentAsync doit accepter paymentMethod.",
);
assert.match(
  subscriptionRepoMaria,
  /GetByExternalIdAsync/,
  "MariaDbSubscriptionRepository doit exposer GetByExternalIdAsync.",
);
assert.match(
  commercialRepoMaria,
  /payment_method = @paymentMethod/,
  "MarkDocumentPaidAsync doit persister payment_method.",
);

// --- Program.cs wiring ---
assert.match(
  programCs,
  /"\/internal\/webhooks\/stripe"/,
  "La route /internal/webhooks/stripe doit etre declaree.",
);
assert.match(
  programCs,
  /StripeConfigurationResolver\.Resolve/,
  "StripeConfigurationResolver doit etre resolu au demarrage.",
);
assert.match(
  programCs,
  /IStripeWebhookService, StripeWebhookService/,
  "IStripeWebhookService doit etre enregistre en DI.",
);

// --- BFF lib ---
assert.match(
  stripeLib,
  /export function getStripeMode/,
  "getStripeMode doit etre exporte.",
);
assert.match(
  stripeLib,
  /export async function createStripeOneShotCheckoutSession/,
  "createStripeOneShotCheckoutSession doit etre exporte.",
);
assert.match(
  stripeLib,
  /export async function createStripeSubscriptionCheckoutSession/,
  "createStripeSubscriptionCheckoutSession doit etre exporte.",
);
assert.match(
  stripeLib,
  /setupFeeAmountCents/,
  "Le helper Stripe abonnement doit accepter les frais de mise en service.",
);
assert.match(
  stripeLib,
  /recurring\[interval_count\]/,
  "Le helper Stripe doit piloter interval_count pour les engagements 1\/6\/12.",
);
assert.match(
  stripeLib,
  /scheduleStripeSubscriptionCancellationAtPeriodEnd/,
  "Le helper Stripe doit permettre la resiliation a fin de terme.",
);
assert.match(
  stripeLib,
  /export async function cancelStripeSubscription/,
  "cancelStripeSubscription doit etre exporte.",
);
assert.match(
  stripeWebhookLib,
  /export function verifyStripeSignature/,
  "verifyStripeSignature doit etre exporte.",
);
assert.match(
  stripeWebhookLib,
  /createHmac\("sha256"/,
  "La verification de signature doit etre calculee localement (HMAC).",
);
assert.match(
  runtimeConfig,
  /export function isStripeConfigured/,
  "isStripeConfigured doit etre exporte.",
);

// --- BFF routes ---
assert.match(
  createIntentRoute,
  /createStripeOneShotCheckoutSession/,
  "La route create-intent doit appeler createStripeOneShotCheckoutSession.",
);
assert.match(
  paymentReturnRoute,
  /payment-success/,
  "La route return doit rediriger vers payment-success sans confirmer le paiement.",
);
assert.match(
  webhookRoute,
  /\/internal\/webhooks\/stripe/,
  "Le webhook BFF doit forwarder vers /internal/webhooks/stripe.",
);
assert.match(
  subscribeCreateRoute,
  /rail === "stripe"/,
  "La route subscriptions/create doit brancher sur le rail.",
);
assert.match(
  subscribeStripeReturnRoute,
  /getStripeCheckoutSession/,
  "Le retour subscription Stripe doit relire la Checkout Session.",
);
assert.match(
  stripePriceRoute,
  /createStripeProduct/,
  "La route admin stripe-price doit creer un produit Stripe.",
);
assert.match(
  stripePriceRoute,
  /billingIntervalMonths \?\? 1/,
  "La route admin stripe-price doit utiliser l'intervalle catalogue.",
);
assert.match(
  clientCancelRoute,
  /cancelStripeSubscription/,
  "La route client de résiliation doit pouvoir annuler Stripe.",
);
assert.match(
  adminCancelRoute,
  /cancelStripeSubscription/,
  "La route admin d'annulation doit pouvoir annuler Stripe.",
);

assert.match(
  clientCancelRoute,
  /scheduleStripeSubscriptionCancellationAtPeriodEnd/,
  "La route client de resiliation doit pouvoir programmer une fin de terme Stripe.",
);
assert.match(
  adminCancelRoute,
  /scheduleStripeSubscriptionCancellationAtPeriodEnd/,
  "La route admin d'annulation doit pouvoir programmer une fin de terme Stripe.",
);

// --- UI ---
assert.match(
  payButton,
  /stripeEnabled/,
  "PayButton doit accepter stripeEnabled.",
);
assert.match(
  subscribeButton,
  /rail/,
  "SubscribeButton doit gerer le rail.",
);
assert.match(
  catalogForm,
  /Créer le prix Stripe/,
  "Le formulaire catalogue doit proposer la creation du prix Stripe.",
);
assert.match(
  adminPaymentsPage,
  /"Rail"/,
  "La page admin paiements doit exposer une colonne Rail.",
);
assert.match(
  adminSubscriptionsPage,
  /item\.rail === "stripe"/,
  "La page admin abonnements doit afficher le rail.",
);
assert.match(
  servicesPage,
  /getStripeMode/,
  "La page services doit propager le mode Stripe aux cartes packs.",
);
assert.match(
  servicesPage,
  /PublicPackCard/,
  "La page services doit utiliser les cartes packs pour les souscriptions Stripe.",
);
assert.match(
  commercialDocumentPage,
  /isStripeConfigured/,
  "La page document doit verifier isStripeConfigured.",
);

// --- Shared types ---
assert.match(
  sharedTypes,
  /export type PaymentRail = "paypal" \| "stripe";/,
  "PaymentRail doit etre exporte.",
);
assert.match(
  sharedTypes,
  /stripePriceIdTest: string \| null;/,
  "CommercialOfferSummary doit exposer stripePriceIdTest.",
);
assert.match(
  sharedTypes,
  /paymentMethod: PaymentRail \| "manual" \| null;/,
  "CommercialDocumentSummary doit exposer paymentMethod.",
);

// --- Env vars ---
assert.match(
  envExample,
  /STRIPE_MODE=disabled/,
  "STRIPE_MODE doit etre documente dans .env.example avec le defaut disabled.",
);
assert.match(
  envExample,
  /STRIPE_WEBHOOK_SECRET=/,
  "STRIPE_WEBHOOK_SECRET doit etre documente dans .env.example.",
);
assert.match(
  envExample,
  /STRIPE_WEBHOOK_VERIFY=/,
  "STRIPE_WEBHOOK_VERIFY doit etre documente dans .env.example.",
);

console.log("Vérification du contrat Stripe V0.29 réussie.");
