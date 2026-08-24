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
const billingV2ReturnRoute = await read(
  "app/api/subscriptions/billing-v2/return/route.ts",
);
const cancellationExecutorCs = await read(
  "../../apps/api-internal/Services/BillingV2ProviderCancellationExecutor.cs",
);
const clientCancelRoute = await read(
  "app/api/subscriptions/[id]/cancel/route.ts",
);
const adminCancelRoute = await read(
  "app/api/admin/subscriptions/[id]/cancel/route.ts",
);
const payButton = await read("components/PayButton.tsx");
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
const inboundEventExtractor = await read(
  "../../apps/api-internal/Services/BillingV2ProviderInboundEventService.cs",
);
const documentPaymentService = await read(
  "../../apps/api-internal/Services/CommercialDocumentStripePaymentService.cs",
);
const invoiceIssuingService = await read(
  "../../apps/api-internal/Services/InvoiceIssuingService.cs",
);
const commercialRepoMaria = await read(
  "../../apps/api-internal/Data/Repositories/MariaDbCommercialDocumentRepository.cs",
);
const webhookEventsMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/017_stripe_webhook_events.sql",
);
const offersPaymentMethodMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/019_stripe_offers_and_payment_method.sql",
);

// --- Schema ---
// Migration HISTORIQUE. `stripe_webhook_events` a ete supprimee par la
// migration 071 : les webhooks fournisseur sont desormais absorbes par
// `billing_v2_provider_inbound_events`. Ce qui est verifie ici n'est donc pas
// l'etat courant du schema, mais qu'une migration DEJA APPLIQUEE en production
// n'est pas reecrite apres coup — la reecrire desynchroniserait les bases qui
// l'ont jouee de celles qui la rejoueraient.
assert.match(
  webhookEventsMigration,
  /CREATE TABLE.+stripe_webhook_events/i,
  "La migration 017 appliquee ne doit pas etre reecrite apres coup.",
);
assert.match(
  webhookEventsMigration,
  /UNIQUE KEY ux_stripe_webhook_events_event_id \(event_id\)/,
  "event_id doit etre UNIQUE pour l'idempotence.",
);
// Les migrations 018 et 019 restent au depot comme historique applique,
// mais elles ne decrivent plus le rail courant : les tables qu'elles
// touchaient ont ete supprimees par la migration 071. Le rail Stripe des
// abonnements est celui de Billing V2, verifie par la suite C#
// `--billing-v2-stripe-rail`.
assert.match(
  offersPaymentMethodMigration,
  /stripe_price_id_live VARCHAR\(64\)/,
  "La migration 018 appliquee ne doit pas etre reecrite apres coup.",
);
assert.match(
  offersPaymentMethodMigration,
  /payment_method ENUM\('paypal','stripe','manual'\)/,
  "commercial_documents.payment_method reste porte par cette migration.",
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
// Les references de prix provider ne sont plus portees par une offre : elles
// vivent dans billing_v2_provider_price_mappings, rattachees a une version de
// prix. Une offre qui porterait son propre price_id recreerait un second
// catalogue tarifaire.
assert.doesNotMatch(
  commercialContracts,
  /stripePriceId|CommercialOffer/,
  "Les contrats commerciaux ne doivent plus porter d offre ni de prix provider.",
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
// Un seul chemin d'entree pour les evenements Stripe : l'extracteur Billing
// V2. Le webhook reste un signal ; la convergence vient du refetch provider.
for (const eventName of [
  "invoice.paid",
  "invoice.payment_succeeded",
  "customer.subscription.deleted",
]) {
  assert.ok(
    inboundEventExtractor.includes(`"${eventName}"`),
    `L extracteur d evenements Billing V2 doit connaitre ${eventName}.`,
  );
}

// Deux rails partagent le webhook Stripe. `payment_intent.succeeded` regle un
// document commercial ponctuel : Billing V2 ne le connait pas, et le retour
// navigateur Stripe n'est qu'une redirection. Sans ce chemin, une facture
// reglee par carte resterait impayee cote BPCE.
assert.ok(
  !inboundEventExtractor.includes('"payment_intent.succeeded"'),
  "Le rail d abonnement V2 ne doit pas s emparer du reglement d un document.",
);
assert.match(
  documentPaymentService,
  /"payment_intent\.succeeded"/,
  "Le reglement Stripe d un document doit avoir son propre chemin.",
);
assert.match(
  documentPaymentService,
  /ReadDataObjectString\(rawPayload, "invoice"\)[\s\S]{0,200}?return "ignored";/,
  "Un payment_intent rattache a une invoice appartient au rail d abonnement.",
);
assert.match(
  documentPaymentService,
  /ReadDataObjectMetadataString\(rawPayload, "document_id"\)/,
  "Le document regle doit venir de metadata.document_id, jamais d une deduction.",
);
assert.match(
  documentPaymentService,
  /throw new InvalidOperationException\(/,
  "Une confirmation en echec doit lever plutot qu acquitter en 200.",
);
assert.match(
  programCs,
  /ICommercialDocumentStripePaymentService documentPaymentService/,
  "Le webhook Stripe doit brancher le reglement de document.",
);
assert.match(
  invoiceIssuingService,
  /ConfirmPaymentAsync\(\s*string documentId,\s*string correlationId,\s*string paymentMethod,/,
  "ConfirmPaymentAsync doit accepter paymentMethod.",
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
  /ICommercialDocumentStripePaymentService,\s*CommercialDocumentStripePaymentService/,
  "Le service de reglement Stripe des documents doit etre enregistre en DI.",
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
// Le portail ne pilote plus AUCUN abonnement Stripe. Ces helpers ont migre
// dans API-INTERNAL, ou ils disposent des identifiants fournisseur persistes
// et de l'outbox qui rend l'operation rejouable.
assert.doesNotMatch(
  stripeLib,
  /createStripeSubscriptionCheckoutSession|getStripeCheckoutSession|cancelStripeSubscription|scheduleStripeSubscriptionCancellationAtPeriodEnd/,
  "Le portail ne doit plus porter de helper d abonnement Stripe.",
);
assert.doesNotMatch(
  stripeLib,
  /createStripeProduct|createStripePrice/,
  "Le catalogue Stripe legacy a disparu : le rail V2 facture en price_data inline.",
);

// --- Resiliation Stripe, cote API-INTERNAL ---
assert.match(
  cancellationExecutorCs,
  /HttpMethod\.Delete/,
  "Une resiliation immediate doit supprimer l abonnement Stripe.",
);
assert.match(
  cancellationExecutorCs,
  /cancel_at_period_end=true/,
  "Une resiliation a fin de terme doit poser cancel_at_period_end chez Stripe.",
);
assert.match(
  cancellationExecutorCs,
  /HttpStatusCode\.NotFound/,
  "Un abonnement deja absent chez le fournisseur est une convergence atteinte,"
    + " pas un echec a rejouer indefiniment.",
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
  billingV2ReturnRoute,
  /internal\/portal\/billing-v2\/provider-return/,
  "L unique parcours de retour est celui de Billing V2, revalide par l API interne.",
);
assert.match(
  programCs,
  /"\/internal\/admin\/billing-v2\/catalog\/prices\/\{id\}\/provider-mapping"/,
  "Le rattachement d un prix provider doit passer par la version de prix V2,"
    + " pas par une offre.",
);
// Les deux BFF de resiliation sont MINCES. Leur role s arrete a authentifier
// et transmettre ; l appel Stripe appartient a API-INTERNAL.
for (const [label, source] of [
  ["client", clientCancelRoute],
  ["admin", adminCancelRoute],
]) {
  assert.doesNotMatch(
    source,
    /cancelStripeSubscription|scheduleStripeSubscriptionCancellationAtPeriodEnd|api\.stripe\.com/,
    `La route ${label} ne doit plus piloter Stripe depuis le portail.`,
  );
  assert.doesNotMatch(
    source,
    /BILLING_V2_CANCELLATION_NOT_AVAILABLE/,
    `La route ${label} ne doit plus refuser les abonnements Billing V2.`,
  );
}

// --- UI ---
assert.match(
  payButton,
  /stripeEnabled/,
  "PayButton doit accepter stripeEnabled.",
);
assert.match(
  adminPaymentsPage,
  /"Rail"/,
  "La page admin paiements doit exposer une colonne Rail.",
);
assert.match(
  adminSubscriptionsPage,
  /formatSubscriptionRailLabel|Facture locale Kermaria/,
  "La page admin abonnements doit afficher le rail, y compris la facturation locale.",
);
assert.match(
  servicesPage,
  /href="\/souscrire"/,
  "La page services doit renvoyer vers l entree de souscription V2.",
);
assert.match(
  commercialDocumentPage,
  /isStripeConfigured/,
  "La page document doit verifier isStripeConfigured.",
);

// --- Shared types ---
assert.match(
  sharedTypes,
  /export type PaymentRail = "paypal" \| "stripe" \| "billing";/,
  "PaymentRail doit inclure le rail billing.",
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
