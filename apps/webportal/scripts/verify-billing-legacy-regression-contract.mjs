import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const publicPackOfferMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/023_public_pack_offers.sql",
);
const catalogTopologyMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/033_catalog_service_topology.sql",
);
const stripeWebhookMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/017_stripe_webhook_events.sql",
);
const paypalWebhookMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/014_paypal_webhook_events.sql",
);
const provisioningMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/021_subscription_provisioning.sql",
);
const activeProvisioningIdempotencyMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/046_subscription_provisioning_active_idempotency.sql",
);
const billingV2DormantSchemaMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/047_billing_v2_schema_dormant.sql",
);
const billingV2CatalogSeedMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/048_billing_v2_catalog_seed.sql",
);
const subscriptionBillingPriceLocksMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/049_subscription_billing_price_locks.sql",
);
const billingV2PaymentAgreementIdempotencyMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/050_billing_v2_payment_agreement_idempotency.sql",
);
const billingV2ProvisioningReadinessMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/051_billing_v2_provisioning_readiness.sql",
);
const billingV2OutboxIdempotencyMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/052_billing_v2_outbox_idempotency.sql",
);
const billingV2ProviderCheckoutSessionsMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/053_billing_v2_provider_checkout_sessions.sql",
);
const billingV2ProviderInboundEventsMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/054_billing_v2_provider_inbound_events.sql",
);
const billingV2AuthoritativeCheckoutRequestsMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/055_billing_v2_authoritative_checkout_requests.sql",
);
const sharedTypes = await read("../../packages/shared/src/index.ts");
const publicPacks = await read("lib/public-packs.ts");
const subscribeCreateRoute = await read("app/api/subscriptions/create/route.ts");
const subscribeButton = await read("components/SubscribeButton.tsx");
const billingV2ReturnRoute = await read(
  "app/api/subscriptions/billing-v2/return/route.ts",
);
const billingV2AdminReadinessRoute = await read(
  "app/api/admin/billing-v2/readiness/route.ts",
);
const billingV2AdminReadinessPage = await read(
  "app/admin/billing-v2/page.tsx",
);
const adminNavigation = await read("components/AdminNavigation.tsx");
const webInternalApi = await read("lib/internal-api.ts");
const adminContracts = await read(
  "../../apps/api-internal/Contracts/AdminContracts.cs",
);
const subscriptionContracts = await read(
  "../../apps/api-internal/Contracts/SubscriptionContracts.cs",
);
const stripeReturnRoute = await read(
  "app/api/subscriptions/stripe/return/route.ts",
);
const subscriptionService = await read(
  "../../apps/api-internal/Services/SubscriptionService.cs",
);
const cartService = await read(
  "../../apps/api-internal/Services/CartService.cs",
);
const catalogConfigurationService = await read(
  "../../apps/api-internal/Services/CatalogConfigurationService.cs",
);
const billingCatalog = await read(
  "../../apps/api-internal/Services/BillingCatalog.cs",
);
const billingV2PricingEngine = await read(
  "../../apps/api-internal/Services/BillingV2PricingEngine.cs",
);
const billingV2ProvisioningShadowService = await read(
  "../../apps/api-internal/Services/Provisioning/BillingV2ProvisioningShadowService.cs",
);
const billingV2ProvisioningService = await read(
  "../../apps/api-internal/Services/Provisioning/BillingV2ProvisioningService.cs",
);
const clientServiceCatalogService = await read(
  "../../apps/api-internal/Services/ClientServiceCatalogService.cs",
);
const billingV2NewSubscriptionService = await read(
  "../../apps/api-internal/Services/BillingV2NewSubscriptionService.cs",
);
const billingV2ProviderService = await read(
  "../../apps/api-internal/Services/BillingV2ProviderService.cs",
);
const billingV2LaunchReadinessService = await read(
  "../../apps/api-internal/Services/BillingV2LaunchReadinessService.cs",
);
const billingV2AdminReadinessService = await read(
  "../../apps/api-internal/Services/BillingV2AdminReadinessService.cs",
);
const billingV2CheckoutReadinessService = await read(
  "../../apps/api-internal/Services/BillingV2CheckoutReadinessService.cs",
);
const billingV2CheckoutPlanner = await read(
  "../../apps/api-internal/Services/BillingV2CheckoutPlanner.cs",
);
const billingV2ProviderCheckoutCommandService = await read(
  "../../apps/api-internal/Services/BillingV2ProviderCheckoutCommandService.cs",
);
const billingV2ProviderCheckoutExecutor = await read(
  "../../apps/api-internal/Services/BillingV2ProviderCheckoutExecutor.cs",
);
const billingV2ProviderOutboxDispatcher = await read(
  "../../apps/api-internal/Services/BillingV2ProviderOutboxDispatcher.cs",
);
const billingV2ProviderInboundEventService = await read(
  "../../apps/api-internal/Services/BillingV2ProviderInboundEventService.cs",
);
const billingV2AuthoritativeCheckoutService = await read(
  "../../apps/api-internal/Services/BillingV2AuthoritativeCheckoutService.cs",
);
const billingV2StripeRail = await read(
  "../../apps/api-internal/Services/BillingV2StripeRail.cs",
);
const webRuntimeConfig = await read("lib/runtime-config.ts");
const apiProgram = await read("../../apps/api-internal/Program.cs");
const billingV2PricingTests = await read(
  "../../tests/api-internal/BillingV2PricingTests.cs",
);
const billingV2ProvisioningShadowTests = await read(
  "../../tests/api-internal/BillingV2ProvisioningShadowTests.cs",
);
const billingV2NewSubscriptionTests = await read(
  "../../tests/api-internal/BillingV2NewSubscriptionTests.cs",
);
const billingLegacyIdempotencyTests = await read(
  "../../tests/api-internal/BillingLegacyIdempotencyTests.cs",
);
const rootPackageJson = await read("../../package.json");
const subscriptionRepository = await read(
  "../../apps/api-internal/Data/Repositories/MariaDbSubscriptionRepository.cs",
);
const commercialRepository = await read(
  "../../apps/api-internal/Data/Repositories/MariaDbCommercialRepository.cs",
);
const renewalWorker = await read(
  "../../apps/api-internal/Services/BillingSubscriptionRenewalWorker.cs",
);
const stripeWebhookService = await read(
  "../../apps/api-internal/Services/StripeWebhookService.cs",
);
const paypalWebhookService = await read(
  "../../apps/api-internal/Services/PayPalWebhookService.cs",
);
const topologyService = await read(
  "../../apps/api-internal/Services/Provisioning/CommercialOfferTopologyService.cs",
);
const provisioningManager = await read(
  "../../apps/api-internal/Services/Provisioning/SubscriptionProvisioningManager.cs",
);
const provisioningService = await read(
  "../../apps/api-internal/Services/Provisioning/ProvisioningService.cs",
);
const provisioningActionRepository = await read(
  "../../apps/api-internal/Data/Repositories/MariaDbSubscriptionProvisioningActionRepository.cs",
);
const subscriptionProvisioningConfiguration = await read(
  "../../apps/api-internal/Data/Configuration/SubscriptionProvisioningRuntimeConfiguration.cs",
);
const envExample = await read("../../.env.example");
const billingV2ReadinessChecks = await read(
  "../../docs/billing-v2/READINESS-CHECKS.sql",
);
const billingV2Readme = await read("../../docs/billing-v2/README.md");
const billingV2MigrationPlan = await read(
  "../../docs/billing-v2/MIGRATION-PLAN.md",
);
const billingV2Rollback = await read("../../docs/billing-v2/ROLLBACK.md");
const billingV2TestPlan = await read("../../docs/billing-v2/TEST-PLAN.md");

const knownLegacyBugs = [];

function noteLegacyBug(code, message) {
  knownLegacyBugs.push({ code, message });
}

const expectedPacks = [
  ["61000000-0000-0000-0000-000000000101", "PACK-DOSSIER-1M-MENS", 900, 1500, 1, 1, "monthly", "pack-dossier-securise"],
  ["61000000-0000-0000-0000-000000000102", "PACK-DOSSIER-6M-MENS", 810, 1500, 1, 6, "monthly", "pack-dossier-securise"],
  ["61000000-0000-0000-0000-000000000103", "PACK-DOSSIER-6M-COMPT", 4860, 1500, 6, 6, "upfront", "pack-dossier-securise"],
  ["61000000-0000-0000-0000-000000000104", "PACK-DOSSIER-12M-MENS", 720, 1500, 1, 12, "monthly", "pack-dossier-securise"],
  ["61000000-0000-0000-0000-000000000105", "PACK-DOSSIER-12M-COMPT", 8640, 1500, 12, 12, "upfront", "pack-dossier-securise"],
  ["61000000-0000-0000-0000-000000000106", "PACK-ACCES-1M-MENS", 1900, 2500, 1, 1, "monthly", "pack-acces-distance"],
  ["61000000-0000-0000-0000-000000000107", "PACK-ACCES-6M-MENS", 1710, 2500, 1, 6, "monthly", "pack-acces-distance"],
  ["61000000-0000-0000-0000-000000000108", "PACK-ACCES-6M-COMPT", 10260, 2500, 6, 6, "upfront", "pack-acces-distance"],
  ["61000000-0000-0000-0000-000000000109", "PACK-ACCES-12M-MENS", 1520, 2500, 1, 12, "monthly", "pack-acces-distance"],
  ["61000000-0000-0000-0000-000000000110", "PACK-ACCES-12M-COMPT", 18240, 2500, 12, 12, "upfront", "pack-acces-distance"],
  ["61000000-0000-0000-0000-000000000111", "PACK-BUREAU-1M-MENS", 3500, 3500, 1, 1, "monthly", "pack-bureau-windows-distance"],
  ["61000000-0000-0000-0000-000000000112", "PACK-BUREAU-6M-MENS", 3150, 3500, 1, 6, "monthly", "pack-bureau-windows-distance"],
  ["61000000-0000-0000-0000-000000000113", "PACK-BUREAU-6M-COMPT", 18900, 3500, 6, 6, "upfront", "pack-bureau-windows-distance"],
  ["61000000-0000-0000-0000-000000000114", "PACK-BUREAU-12M-MENS", 2800, 3500, 1, 12, "monthly", "pack-bureau-windows-distance"],
  ["61000000-0000-0000-0000-000000000115", "PACK-BUREAU-12M-COMPT", 33600, 3500, 12, 12, "upfront", "pack-bureau-windows-distance"],
  ["61000000-0000-0000-0000-000000000116", "PACK-PRO-1M-MENS", 4900, 4900, 1, 1, "monthly", "pack-pro-association"],
  ["61000000-0000-0000-0000-000000000117", "PACK-PRO-6M-MENS", 4410, 4900, 1, 6, "monthly", "pack-pro-association"],
  ["61000000-0000-0000-0000-000000000118", "PACK-PRO-6M-COMPT", 26460, 4900, 6, 6, "upfront", "pack-pro-association"],
  ["61000000-0000-0000-0000-000000000119", "PACK-PRO-12M-MENS", 3920, 4900, 1, 12, "monthly", "pack-pro-association"],
  ["61000000-0000-0000-0000-000000000120", "PACK-PRO-12M-COMPT", 47040, 4900, 12, 12, "upfront", "pack-pro-association"],
].map(([
  id,
  externalReference,
  priceAmountCents,
  setupFeeAmountCents,
  billingIntervalMonths,
  commitmentMonths,
  paymentMode,
  publicPackCode,
]) => ({
  id,
  externalReference,
  priceAmountCents,
  setupFeeAmountCents,
  billingIntervalMonths,
  commitmentMonths,
  paymentMode,
  publicPackCode,
}));

const actualPacks = parseCommercialOfferInsert(publicPackOfferMigration);

assert.equal(
  actualPacks.length,
  20,
  "La migration legacy doit continuer a declarer exactement 20 offres PACK-*.",
);

for (const expected of expectedPacks) {
  const actual = actualPacks.find(
    (offer) => offer.external_reference === expected.externalReference,
  );
  assert.ok(actual, `${expected.externalReference} doit exister.`);
  assert.equal(actual.id, expected.id, `${expected.externalReference}: id fige.`);
  assert.equal(
    actual.price_amount_cents,
    expected.priceAmountCents,
    `${expected.externalReference}: prix checkout HT fige.`,
  );
  assert.equal(
    actual.setup_fee_amount_cents,
    expected.setupFeeAmountCents,
    `${expected.externalReference}: frais de mise en service figes.`,
  );
  assert.equal(
    actual.billing_interval_months,
    expected.billingIntervalMonths,
    `${expected.externalReference}: intervalle de facturation fige.`,
  );
  assert.equal(
    actual.commitment_months,
    expected.commitmentMonths,
    `${expected.externalReference}: duree d'engagement figee.`,
  );
  assert.equal(
    actual.payment_mode,
    expected.paymentMode,
    `${expected.externalReference}: payment_mode fige.`,
  );
  assert.equal(
    actual.public_pack_code,
    expected.publicPackCode,
    `${expected.externalReference}: public_pack_code fige.`,
  );
  assert.equal(actual.billing_cadence, "monthly");
  assert.equal(actual.currency, "EUR");
  assert.equal(actual.tax_rate_basis_points, 2000);
  assert.equal(
    actual.price_amount_cents + actual.setup_fee_amount_cents,
    expected.priceAmountCents + expected.setupFeeAmountCents,
    `${expected.externalReference}: premier debit checkout fige.`,
  );
}

assert.deepEqual(
  actualPacks.map((offer) => offer.external_reference),
  expectedPacks.map((offer) => offer.externalReference),
  "L'ordre commercial des 20 PACK-* doit rester stable.",
);
assert.match(
  sharedTypes,
  /function getPublicPackDiscountPercent[\s\S]*case 6:[\s\S]*return 10;[\s\S]*case 12:[\s\S]*return 20;/,
  "Le manifeste public legacy doit conserver les remises affichees 6/12 mois.",
);
assert.match(
  sharedTypes,
  /monthlyPriceAmountCents\s*=\s*[\s\S]*Math\.round\(billingPriceAmountCents \/ commitmentMonths\)/,
  "Le prix mensuel affiche pour un upfront doit venir du prix de checkout divise par l'engagement.",
);
assert.match(
  sharedTypes,
  /firstChargeAmountCents:\s*billingPriceAmountCents \+ setupFeeAmountCents/,
  "Le premier debit affiche doit additionner prix de checkout et mise en service.",
);
assert.match(
  publicPacks,
  /resolvePackSelection\(catalog, selection\)/,
  "La selection pack webportal doit etre resolue depuis le catalogue actif.",
);

assert.match(
  subscribeCreateRoute,
  /mode === "live" \? offer\.stripePriceIdLive : offer\.stripePriceIdTest/,
  "Le checkout Stripe doit selectionner stripePriceIdLive/Test selon STRIPE_MODE.",
);
assert.match(
  subscribeCreateRoute,
  /mode === "live" \? offer\.paypalPlanIdLive : offer\.paypalPlanIdSandbox/,
  "Le checkout PayPal doit selectionner paypalPlanIdLive/Sandbox selon PAYPAL_MODE.",
);
assert.match(
  subscribeCreateRoute,
  /offer\.billingCadence !== "monthly"[\s\S]*!activePriceId[\s\S]*offer\.status !== "active"/,
  "Le checkout doit refuser une offre inactive, non mensuelle ou sans id fournisseur actif.",
);
assert.match(
  subscribeCreateRoute,
  /setupFeeAmountCents:\s*offer\.setupFeeAmountCents \?\? 0/,
  "Le checkout Stripe abonnement doit transmettre les frais de mise en service.",
);
assert.match(
  subscriptionService,
  /IBillingCatalog _billingCatalog[\s\S]*_billingCatalog\.ResolveSubscribableOfferAsync/,
  "SubscriptionService doit deleguer la resolution d'offre au catalogue Billing de compatibilite.",
);
assert.match(
  billingCatalog,
  /public interface IBillingCatalog[\s\S]*ResolveSubscribableOfferAsync[\s\S]*ResolveProviderExternalId/,
  "IBillingCatalog doit exposer les resolutions d'offre et d'identifiants fournisseur.",
);
assert.match(
  billingCatalog,
  /LegacyBillingCatalogAdapter[\s\S]*ICommercialRepository[\s\S]*GetClientCatalogAsync[\s\S]*GetAdminCatalogAsync/,
  "LegacyBillingCatalogAdapter doit encapsuler les lectures legacy commercial_offers via ICommercialRepository.",
);
assert.match(
  billingCatalog,
  /V2BillingCatalogAdapter[\s\S]*billing_v2_legacy_offer_mappings/,
  "V2BillingCatalogAdapter doit lire le catalogue V2 uniquement depuis les tables billing_v2_*.",
);
assert.match(billingCatalog, /V2BillingCatalogAdapter[\s\S]*billing_v2_service_prices/);
assert.match(billingCatalog, /V2BillingCatalogAdapter[\s\S]*billing_v2_preset_items/);
assert.match(
  billingCatalog,
  /ShadowBillingCatalogAdapter[\s\S]*var legacy = await _legacy\.ResolveSubscribableOfferAsync[\s\S]*await CompareResolvedOfferAsync[\s\S]*return legacy/,
  "Le shadow catalog doit toujours retourner le resultat legacy autoritaire.",
);
assert.match(
  billingCatalog,
  /BILLING_V2_CATALOG_SHADOW_MODE[\s\S]*CatalogShadowModeEnabled/,
  "Le shadow catalog V2 doit etre active par configuration explicite.",
);
assert.doesNotMatch(
  billingCatalog,
  /^\s*(INSERT|UPDATE|DELETE|DROP|ALTER)\b/im,
  "Les adapters Billing Catalog ne doivent pas ecrire en base.",
);
assert.match(
  billingCatalog,
  /string\.Equals\(rail, "stripe"[\s\S]*offer\.StripePriceIdLive[\s\S]*offer\.StripePriceIdTest[\s\S]*string\.Equals\(rail, "billing"[\s\S]*string\.Empty[\s\S]*offer\.PayPalPlanIdLive[\s\S]*offer\.PayPalPlanIdSandbox/,
  "L'adapter doit resoudre les ids Stripe/PayPal avec la meme logique live/test.",
);
assert.match(
  billingCatalog,
  /string\.Equals\(rail, "billing"[\s\S]*string\.Empty/,
  "Le rail billing local doit rester sans id fournisseur externe.",
);
assert.match(
  billingCatalog,
  /LegacyBillingCatalogAdapter[\s\S]*ICommercialRepository/,
  "La lecture catalogue autoritaire doit rester le legacy.",
);

const billingV2DormantTables = billingV2DormantSchemaMigration.match(
  /^CREATE TABLE IF NOT EXISTS billing_v2_[a-z_]+/gm,
) ?? [];
const billingV2StatementBreaks = billingV2DormantSchemaMigration.match(
  /^-- statement-break$/gm,
) ?? [];
assert.equal(
  billingV2DormantTables.length,
  23,
  "La migration Billing V2 dormante doit creer les 23 tables de schema attendues.",
);
assert.equal(
  billingV2StatementBreaks.length,
  23,
  "La migration Billing V2 dormante doit etre decoupee pour le runner MariaDB.",
);
assert.doesNotMatch(
  billingV2DormantSchemaMigration,
  /^\s*(INSERT|UPDATE|DELETE|DROP|ALTER)\b/im,
  "La migration Billing V2 dormante ne doit pas semer, modifier ou supprimer de donnees.",
);
assert.doesNotMatch(
  billingV2DormantSchemaMigration,
  /^CREATE OR REPLACE VIEW\b/im,
  "La migration Billing V2 dormante ne doit pas encore introduire de vues de calcul actives.",
);
assert.doesNotMatch(
  billingV2DormantSchemaMigration,
  /\s(?:FLOAT|DOUBLE)\s/i,
  "La migration Billing V2 ne doit pas utiliser de types flottants pour l'argent.",
);
assert.match(
  billingV2DormantSchemaMigration,
  /CREATE TABLE IF NOT EXISTS billing_v2_subscription_items[\s\S]*amount_cents_snapshot\s+BIGINT\s+NOT NULL/,
  "Les items d'abonnement V2 doivent stocker un snapshot de prix en centimes entiers.",
);
assert.match(
  billingV2DormantSchemaMigration,
  /CREATE TABLE IF NOT EXISTS billing_v2_subscription_price_locks[\s\S]*amount_cents\s+BIGINT\s+NOT NULL/,
  "Les price locks V2 doivent pouvoir figer un montant contractuel en centimes.",
);
assert.match(
  billingV2DormantSchemaMigration,
  /CREATE TABLE IF NOT EXISTS billing_v2_provider_price_mappings[\s\S]*UNIQUE KEY uq_billing_v2_provider_price_mapping/,
  "Les mappings provider V2 doivent separer les ids Stripe\/PayPal des prix metier.",
);
assert.match(
  billingV2PaymentAgreementIdempotencyMigration,
  /GROUP BY provider, environment, provider_subscription_id[\s\S]*HAVING COUNT\(\*\) > 1/,
  "La migration d'idempotence provider V2 doit documenter la detection prealable des doublons.",
);
assert.match(
  billingV2PaymentAgreementIdempotencyMigration,
  /ADD UNIQUE KEY IF NOT EXISTS[\s\S]*uq_billing_v2_payment_agreements_provider_subscription[\s\S]*\(provider, environment, provider_subscription_id\)/,
  "Les accords provider V2 doivent interdire deux abonnements locaux pour le meme abonnement fournisseur.",
);
assert.match(
  billingV2DormantSchemaMigration,
  /CREATE TABLE IF NOT EXISTS billing_v2_commitment_payment_options[\s\S]*payment_mode\s+VARCHAR\(24\)[\s\S]*discount_basis_points\s+INT\s+NOT NULL/,
  "Les remises V2 doivent dependre de l'engagement et du mode de paiement.",
);
assert.doesNotMatch(
  billingV2PricingEngine,
  /\b(decimal|double|float)\b/i,
  "Le moteur pricing Billing V2 doit utiliser uniquement de l'arithmetique entiere.",
);
assert.match(
  billingV2PricingEngine,
  /amountCents \* basisPoints \+ 5000L\) \/ BasisPointDenominator/,
  "Le moteur pricing Billing V2 doit arrondir les basis points en centimes entiers.",
);
assert.match(
  billingV2PricingEngine,
  /MinimumCommitmentBasisPoints = 4500/,
  "Le moteur pricing Billing V2 doit appliquer le plancher contractuel 45%.",
);
assert.match(
  billingV2PricingEngine,
  /BillingV2PriceLockTypes\.MonthlyRecurring[\s\S]*lockSnapshot\.AmountCents/,
  "Le moteur pricing Billing V2 doit supporter le price lock mensuel.",
);
assert.match(
  billingV2PricingEngine,
  /BillingV2PriceLockTypes\.UpfrontPrepaid[\s\S]*PayableRecurringAmountCents: 0/,
  "Le moteur pricing Billing V2 doit respecter l'upfront deja paye sans refacturer.",
);
assert.match(
  billingV2PricingEngine,
  /CalculateUpfrontUpgradeProration[\s\S]*requestedMonthlyAmountCents\s*<=\s*purchasedMonthlyAmountCents[\s\S]*\?\s*0/,
  "Le moteur pricing Billing V2 ne doit pas rembourser automatiquement une reduction upfront.",
);
assert.doesNotMatch(
  renewalWorker,
  /IBillingV2PricingEngine|BillingV2PricingEngine/,
  "Le worker de renouvellement legacy ne doit pas encore corriger le repricing via Billing V2.",
);
assert.match(
  billingV2PricingTests,
  /VerifyMonthlyDiscountAndOneTimeExclusion[\s\S]*VerifyPriceLocksOverrideDynamicPricing[\s\S]*VerifySnapshotsAreUsedAsContractualPrices/,
  "Les tests Billing V2 doivent couvrir remise, one-time, price lock et snapshots.",
);
assert.match(
  rootPackageJson,
  /--billing-v2-pricing/,
  "La suite billing legacy doit executer les tests pricing Billing V2.",
);
assert.match(
  billingCatalog,
  /BILLING_V2_PROVISIONING_SHADOW_MODE/,
  "Le shadow provisioning Billing V2 doit etre activable par flag dedie et desactive par defaut.",
);
assert.match(
  billingV2ProvisioningShadowService,
  /IBillingV2ProvisioningShadowService[\s\S]*BillingV2ProvisioningShadowCalculator[\s\S]*target_type = 'ad_group'/,
  "Le shadow provisioning V2 doit calculer les groupes AD depuis les mappings et regles V2 sans action externe.",
);
assert.match(
  subscriptionProvisioningConfiguration,
  /DefaultGroupsByOfferExternalReference[\s\S]*PACK-BUREAU-1M-MENS[\s\S]*GG_RDS[\s\S]*GG_VPN/,
  "Le legacy provisioning garde ses mappings historiques AD autoritaires.",
);
assert.match(
  billingV2CatalogSeedMigration,
  /INSERT INTO billing_v2_provisioning_rules[\s\S]*VPN-ACCESS[\s\S]*GG_VPN[\s\S]*INSERT INTO billing_v2_provisioning_rules[\s\S]*RDS-ACCESS[\s\S]*GG_RDS/,
  "Le seed V2 doit declarer les regles shadow AD VPN/RDS.",
);
assert.match(
  billingV2CatalogSeedMigration,
  /nextcloud_user_quota[\s\S]*tier_numeric_value[\s\S]*nextcloud_shared_quota/,
  "Le seed V2 doit preparer les regles de quota Nextcloud sans les executer.",
);
assert.match(
  billingV2ProvisioningReadinessMigration,
  /CREATE TABLE IF NOT EXISTS billing_v2_provisioning_client_readiness[\s\S]*ready_for_v2_provisioning[\s\S]*add_only_mode[\s\S]*last_shadow_matches_legacy[\s\S]*unresolved_mismatch_count/,
  "Le provisioning V2 reel doit exiger une readiness client explicite et fail-closed.",
);
assert.match(
  billingV2ReadinessChecks,
  /FROM subscriptions subscription[\s\S]*INNER JOIN customers customer[\s\S]*COALESCE\(customer\.is_demo, FALSE\) = FALSE[\s\S]*real_customer_subscription_count/,
  "Les controles pre-activation doivent verifier en lecture seule les vrais abonnements clients hors demo.",
);
assert.doesNotMatch(
  billingV2ReadinessChecks,
  /demo_converted_at\s+IS\s+NULL/i,
  "Un client converti depuis une demo mais non-demo ne doit pas etre exclu du comptage reel par defaut.",
);
assert.match(
  billingV2LaunchReadinessService,
  /SUM\(CASE WHEN COALESCE\(customer\.is_demo, FALSE\) = FALSE[\s\S]*real_count[\s\S]*SUM\(CASE WHEN COALESCE\(customer\.is_demo, FALSE\) = TRUE[\s\S]*demo_count/,
  "Le service readiness lancement V2 doit compter separement vrais abonnements et demos en lecture seule.",
);
// Detecteur ancre en debut de ligne, comme les autres controles de ce
// fichier : une mutation SQL est une instruction, pas un appel de methode.
// La forme non ancree confondait `List<T>.Insert(...)` avec un INSERT SQL.
assert.doesNotMatch(
  billingV2LaunchReadinessService,
  /^\s*(INSERT|UPDATE|DELETE|DROP|ALTER)\b/im,
  "La readiness lancement V2 ne doit pas modifier les donnees.",
);
assert.match(
  adminContracts,
  /BillingV2AdminRuntimeFlags[\s\S]*BillingV2AdminLaunchReadiness[\s\S]*BlockingRealSubscriptions[\s\S]*BillingV2AdminBlockingLegacySubscription[\s\S]*BillingV2AdminProviderReadiness[\s\S]*BillingV2AdminReadinessSnapshot/,
  "Les contrats admin doivent exposer un snapshot de readiness Billing V2 lisible sans secrets.",
);
assert.match(
  billingV2AdminReadinessService,
  /IBillingV2AdminReadinessService[\s\S]*information_schema\.tables[\s\S]*billing_v2_provider_price_mappings[\s\S]*BillingV2AdminReadinessGate\.ResolveReasonCode/,
  "La readiness admin Billing V2 doit verifier schema, mappings provider et gate fail-closed en lecture seule.",
);
assert.match(
  billingV2AdminReadinessService,
  /BillingV2AdminReadinessMapper[\s\S]*ToAdminLaunchReadiness[\s\S]*BlockingRealSubscriptions[\s\S]*BillingV2AdminBlockingLegacySubscription/,
  "La readiness admin Billing V2 doit mapper la preuve launch, y compris les abonnements reels bloquants.",
);
assert.match(
  billingV2LaunchReadinessService,
  /BlockingRealSubscriptions[\s\S]*COALESCE\(customer\.is_demo, FALSE\) = FALSE[\s\S]*LIMIT 50/,
  "La readiness launch Billing V2 doit exposer les abonnements reels bloquants sans traiter les demos comme contrats reels.",
);
assert.match(
  billingV2AdminReadinessService,
  /BILLING_V2_ADMIN_READY_FOR_FIRST_SUBSCRIPTION[\s\S]*BILLING_V2_ADMIN_REAL_LEGACY_SUBSCRIPTIONS_PRESENT[\s\S]*BILLING_V2_ADMIN_LAUNCH_READINESS_UNVERIFIED[\s\S]*BILLING_V2_ADMIN_NO_PROVIDER_READY/,
  "La readiness admin Billing V2 doit distinguer premier abonnement autorisable, contrats reels presents, preuve SQL manquante et providers incomplets.",
);
assert.doesNotMatch(
  billingV2AdminReadinessService,
  /^\s*(INSERT|UPDATE|DELETE|DROP|ALTER)\b/im,
  "La readiness admin Billing V2 ne doit pas muter la base.",
);
assert.doesNotMatch(
  billingV2AdminReadinessService,
  /SecretKey|ClientSecret|PAYPAL_CLIENT_SECRET|STRIPE_SECRET_KEY/i,
  "La readiness admin Billing V2 ne doit exposer aucun secret provider.",
);
assert.match(
  apiProgram,
  /\/internal\/admin\/billing-v2\/readiness[\s\S]*IBillingV2AdminReadinessService[\s\S]*ResolvePortalSessionAsync[\s\S]*PortalRoles\.InternalAdmin[\s\S]*CheckAsync/,
  "L'endpoint interne admin readiness Billing V2 doit etre authentifie et strictement consultatif.",
);
const billingV2AdminReadinessEndpointStart = apiProgram.indexOf(
  '"/internal/admin/billing-v2/readiness"',
);
const billingV2AdminReadinessEndpointEnd = apiProgram.indexOf(
  '"/internal/admin/ad/status"',
  billingV2AdminReadinessEndpointStart,
);
assert.ok(
  billingV2AdminReadinessEndpointStart >= 0
    && billingV2AdminReadinessEndpointEnd > billingV2AdminReadinessEndpointStart,
  "Le bloc endpoint admin readiness Billing V2 doit etre identifiable.",
);
assert.doesNotMatch(
  apiProgram.slice(
    billingV2AdminReadinessEndpointStart,
    billingV2AdminReadinessEndpointEnd,
  ),
  /auditService|RecordAsync|INSERT|UPDATE|DELETE|HttpClient|ProvisioningService/,
  "L'endpoint admin readiness Billing V2 ne doit pas ecrire d'audit, appeler provider ou declencher du provisioning.",
);
assert.match(
  billingV2AdminReadinessRoute,
  /handleAdminGet<Record<string, unknown>>[\s\S]*\/internal\/admin\/billing-v2\/readiness/,
  "La route BFF admin doit exposer le snapshot Billing V2 via le proxy admin existant.",
);
assert.match(
  billingV2Readme,
  /\/admin\/billing-v2[\s\S]*\/api\/admin\/billing-v2\/readiness[\s\S]*\/internal\/admin\/billing-v2\/readiness[\s\S]*Un simple flag global n'est jamais suffisant/,
  "Le README Billing V2 doit documenter la readiness admin et l'absence d'activation par simple flag.",
);
assert.match(
  billingV2Readme,
  /054_billing_v2_provider_inbound_events\.sql[\s\S]*055_billing_v2_authoritative_checkout_requests\.sql/,
  "Le README Billing V2 doit lister les migrations provider inbound et checkout autoritaire.",
);
assert.match(
  billingV2MigrationPlan,
  /BILLING_V2_AUTHORITATIVE_CHECKOUT_BFF_ENABLED=true[\s\S]*BILLING_V2_PROVIDER_OUTBOX_ENABLED=true[\s\S]*BILLING_V2_PROVIDER_EXECUTOR_ENABLED=true[\s\S]*\/admin\/billing-v2[\s\S]*BILLING_V2_ADMIN_READY_FOR_FIRST_SUBSCRIPTION/,
  "Le plan de migration doit expliciter les flags et le snapshot admin requis avant le premier vrai abonnement V2.",
);
assert.match(
  billingV2MigrationPlan,
  /\/api\/subscriptions\/create[\s\S]*chemin legacy[\s\S]*\/internal\/portal\/billing-v2\/subscriptions\/checkout[\s\S]*outbox sont atomiques/,
  "Le plan de migration doit décrire le checkout BFF flaggé et le fallback legacy.",
);
assert.match(
  billingV2Rollback,
  /BILLING_V2_AUTHORITATIVE_CHECKOUT_BFF_ENABLED=false[\s\S]*BILLING_V2_PROVIDER_OUTBOX_ENABLED=false[\s\S]*BILLING_V2_PROVIDER_EXECUTOR_ENABLED=false[\s\S]*BILLING_V2_PROVISIONING_ENABLED=false/,
  "Le rollback Billing V2 doit documenter la fermeture de tous les flags d'effet externe.",
);
assert.match(
  billingV2Rollback,
  /Ne jamais supprimer un objet Stripe\/PayPal[\s\S]*billing_v2_provider_events[\s\S]*DROP[\s\S]*DELETE[\s\S]*factures historiques/,
  "Le rollback Billing V2 doit interdire suppression provider et migrations destructives.",
);
assert.match(
  billingV2TestPlan,
  /Readiness premier abonnement V2[\s\S]*flag BFF off[\s\S]*\/admin\/billing-v2[\s\S]*retry checkout V2/,
  "Le plan de tests Billing V2 doit couvrir readiness admin, flags et retry checkout du premier abonnement.",
);
assert.match(
  webInternalApi,
  /BillingV2AdminBlockingLegacySubscription[\s\S]*BillingV2AdminReadinessSnapshot[\s\S]*getAdminBillingV2Readiness[\s\S]*\/internal\/admin\/billing-v2\/readiness/,
  "Le BFF web doit typer et charger le snapshot readiness Billing V2, y compris les abonnements reels bloquants, sans appel direct a MariaDB.",
);
assert.match(
  adminNavigation,
  /\/admin\/billing-v2[\s\S]*Billing V2/,
  "La readiness Billing V2 doit etre accessible depuis la navigation admin.",
);
assert.match(
  billingV2AdminReadinessPage,
  /requireAdminSession[\s\S]*getAdminBillingV2Readiness[\s\S]*canRequestFirstRealSubscription[\s\S]*reasonCode[\s\S]*launchReadiness[\s\S]*blockingRealSubscriptions[\s\S]*providers/,
  "La page admin Billing V2 doit afficher le snapshot de readiness et les contrats reels bloquants pour validation humaine.",
);
assert.doesNotMatch(
  billingV2AdminReadinessPage,
  /fetch\(|method=["']?(?:POST|PUT|PATCH|DELETE)|handleAdminMutation|activateBilling|enableBilling|requestFirstRealSubscription/,
  "La page admin Billing V2 doit rester strictement read-only.",
);
assert.match(
  envExample,
  /BILLING_V2_PROVISIONING_ENABLED=false/,
  "Le provisioning V2 reel doit etre desactive par defaut.",
);
assert.match(
  billingV2ProvisioningService,
  /ProvisioningEnabled[\s\S]*BillingV2ProvisioningReadinessGate\.Evaluate[\s\S]*Legacy provisioning remains authoritative/,
  "Le provisioning V2 reel doit rester derriere flag et gate de readiness stricte.",
);
assert.match(
  billingV2ProvisioningService,
  /ResolveManagedGroupsForExecution[\s\S]*decision\.AddOnlyMode[\s\S]*desiredGroups[\s\S]*managedGroups/,
  "Le provisioning V2 reel doit demarrer en add-only avant d'autoriser les retraits.",
);
assert.match(
  billingV2ProvisioningService,
  /LEFT JOIN billing_v2_subscription_item_provisioning[\s\S]*provisioning_item_id[\s\S]*RuleType: string\.Empty[\s\S]*TargetType: string\.Empty/,
  "Le provisioning V2 doit rendre detectables les items actifs sans etat de provisioning au lieu de les ignorer.",
);
assert.match(
  billingV2ProvisioningService,
  /IBillingV2NextcloudQuotaProvider[\s\S]*DormantBillingV2NextcloudQuotaProvider[\s\S]*BILLING_V2_NEXTCLOUD_QUOTA_PROVIDER_NOT_CONFIGURED/,
  "Le quota Nextcloud doit etre represente par un provider dormant explicite sans integration runtime supposee.",
);
assert.match(
  apiProgram,
  /IBillingV2NextcloudQuotaProvider[\s\S]*DormantBillingV2NextcloudQuotaProvider\.Instance/,
  "Le provider quota Nextcloud enregistre doit rester dormant par defaut.",
);
assert.match(
  billingV2ProvisioningService,
  /plan\.NextcloudQuotas\.Count > 0[\s\S]*CheckReadiness\(plan\.NextcloudQuotas\)[\s\S]*Legacy provisioning remains authoritative/,
  "Le provisioning V2 doit bloquer les quotas Nextcloud tant qu'aucun provider fiable ne peut les appliquer.",
);
assert.match(
  billingV2ProvisioningShadowTests,
  /VerifyTwentyLegacyPacksResolveExpectedAdGroups[\s\S]*packs\.Length == 20[\s\S]*VerifyMissingV2RuleWouldBeDetected[\s\S]*VerifyClientServiceCatalogShadowCoversLegacyReferences[\s\S]*VerifyClientServiceCatalogShadowDetectsMissingMapping/,
  "Les tests shadow provisioning doivent couvrir les 20 PACK-*, les mappings incomplets et les droits visibles portail.",
);
assert.match(
  billingV2ProvisioningShadowTests,
  /VerifyProvisioningReadinessAllowsOnlyCompleteReadyMatch[\s\S]*VerifyProvisioningReadinessDeniesMismatch[\s\S]*VerifyProvisioningReadinessDeniesIncompleteMaterialization[\s\S]*VerifyProvisioningReadinessDeniesUnknownRuleOrGroup[\s\S]*VerifyProvisioningReadinessDeniesFlagOff[\s\S]*VerifyFirstActivationIsAddOnly[\s\S]*VerifyProvisioningRetryKeepsSameGateDecision[\s\S]*VerifyProvisioningPlannerFlagsMissingItemProvisioning[\s\S]*VerifyNextcloudQuotaRulesAreCalculatedButNotAdGroups[\s\S]*VerifyDormantNextcloudQuotaProviderBlocksExecution/,
  "Les tests readiness provisioning V2 doivent couvrir match, mismatch, legacy incomplet, inconnus, flag off, add-only, retry, items non materialises et quotas Nextcloud dormants.",
);
assert.match(
  subscriptionService,
  /SyncFromLegacySubscriptionAsync[\s\S]*TryReconcileProvisioningAsync/,
  "Le statut V2 local doit etre synchronise avant toute tentative de provisioning.",
);
assert.match(
  provisioningManager,
  /CreateRequestedAsync[\s\S]*MarkStartedAsync[\s\S]*_v2Provisioning\.TryReconcileAsync/,
  "L'idempotence existante des actions doit etre acquise avant tout appel provisioning V2.",
);
assert.match(
  clientServiceCatalogService,
  /IBillingV2ClientServiceCatalogShadowService[\s\S]*CompareV2ShadowAsync[\s\S]*return services/,
  "ClientServiceCatalogService doit conserver le catalogue legacy autoritaire apres comparaison shadow V2.",
);
assert.match(
  rootPackageJson,
  /--billing-v2-provisioning-shadow/,
  "La suite billing legacy doit executer les tests shadow provisioning Billing V2.",
);
assert.match(
  billingCatalog,
  /BILLING_V2_NEW_SUBSCRIPTIONS_ENABLED/,
  "Les nouveaux abonnements Billing V2 doivent rester derriere un flag desactive par defaut.",
);
assert.match(
  billingCatalog,
  /BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED[\s\S]*BILLING_V2_FIRST_REAL_SUBSCRIPTION_APPROVED/,
  "Le checkout V2 autoritaire doit exiger des flags dedies desactives par defaut.",
);
assert.match(
  envExample,
  /BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED=false[\s\S]*BILLING_V2_FIRST_REAL_SUBSCRIPTION_APPROVED=false[\s\S]*BILLING_V2_PROVIDER_OUTBOX_ENABLED=false[\s\S]*BILLING_V2_PROVIDER_EXECUTOR_ENABLED=false/,
  "Les flags de checkout V2 autoritaire doivent etre documentes comme inactifs par defaut.",
);
assert.match(
  billingV2CheckoutReadinessService,
  /IBillingV2CheckoutReadinessService[\s\S]*IBillingV2LaunchReadinessService[\s\S]*VerifyPriceMappingsReadyAsync[\s\S]*BillingV2CheckoutReadinessGate\.Evaluate/,
  "La readiness checkout V2 doit composer verification read-only des abonnements reels et mappings provider.",
);
assert.match(
  billingV2CheckoutReadinessService,
  /AuthoritativeCheckoutEnabled[\s\S]*FirstRealSubscriptionApproved[\s\S]*ProviderOutboxEnabled[\s\S]*ProviderExecutorEnabled[\s\S]*NoRealCustomerSubscriptions[\s\S]*VerifiedAgainstPersistentSql[\s\S]*providerMappings\.Ready/,
  "Le checkout V2 autoritaire doit etre fail-closed sans flags checkout/provider, validation humaine, absence de contrats reels verifiee et mappings complets.",
);
assert.doesNotMatch(
  subscribeCreateRoute,
  /BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED|BILLING_V2_NEW_SUBSCRIPTIONS_ENABLED|BILLING_V2_FIRST_REAL_SUBSCRIPTION_APPROVED/,
  "La route de checkout publique ne doit pas lire les flags API Billing V2 autoritaires directement.",
);
assert.match(
  subscribeCreateRoute,
  /isBillingV2AuthoritativeCheckoutBffEnabled[\s\S]*useBillingV2AuthoritativeCheckout/,
  "La route de checkout publique ne peut preparer Billing V2 que derriere le flag BFF dedie.",
);
assert.match(
  subscriptionService,
  /CreatePendingAsync[\s\S]*CreateForNewSubscriptionAsync[\s\S]*CreateBilledPendingAsync[\s\S]*CreateForNewSubscriptionAsync/,
  "Les creations de nouveaux abonnements doivent pouvoir materialiser un contrat V2 opt-in.",
);
assert.match(
  billingV2NewSubscriptionService,
  /NewSubscriptionsEnabled[\s\S]*billing_v2_subscriptions[\s\S]*billing_v2_subscription_items[\s\S]*billing_v2_subscription_item_provisioning/,
  "Le service nouveaux abonnements V2 doit creer le contrat local, les items et l'etat provisioning sans action externe.",
);
assert.doesNotMatch(
  billingV2NewSubscriptionService,
  /billing_v2_provider_price_mappings|createStripe|createPayPal|INSERT INTO billing_v2_payment_agreements/,
  "La tranche nouveaux abonnements V2 doit deleguer les accords provider sans appel Stripe/PayPal ni mapping prix provider.",
);
assert.match(
  billingV2ProviderService,
  /IBillingV2ProviderAgreementService[\s\S]*RecordFromLegacySubscriptionAsync[\s\S]*BillingV2ProviderAgreementPlanner\.PlanFromLegacy/,
  "La couche provider V2 doit etre dediee et appelee depuis la materialisation locale.",
);
assert.match(
  billingV2ProviderService,
  /INSERT INTO billing_v2_payment_agreements[\s\S]*provider_subscription_id[\s\S]*ON DUPLICATE KEY UPDATE/,
  "La couche provider V2 locale doit enregistrer les abonnements fournisseur de facon idempotente.",
);
assert.match(
  billingV2ProviderService,
  /EnsurePaymentAgreementIdempotencyAsync[\s\S]*provider_subscription_id = @provider_subscription_id[\s\S]*deja associe a un autre contrat local/,
  "La couche provider V2 locale doit detecter un abonnement fournisseur deja lie a un autre contrat.",
);
assert.doesNotMatch(
  billingV2ProviderService,
  /createStripe|createPayPal|HttpClient|https:\/\/api\.stripe|https:\/\/api-m\.paypal/,
  "La couche provider V2 locale ne doit pas appeler Stripe ou PayPal a ce stade.",
);
assert.match(
  billingV2ProviderService,
  /VerifyPriceMappingsReadyAsync[\s\S]*billing_v2_provider_price_mappings[\s\S]*BillingV2ProviderPriceMappingGate\.Evaluate/,
  "La couche provider V2 doit verifier les mappings service_price -> provider avant un futur checkout V2.",
);
assert.match(
  billingV2ProviderService,
  /WHEN provider = 'stripe' THEN external_price_id[\s\S]*WHEN provider = 'paypal' THEN external_plan_id[\s\S]*AS provider_external_id/,
  "La couche provider V2 doit resoudre l'id externe depuis external_price_id ou external_plan_id selon le rail.",
);
assert.match(
  billingV2CheckoutPlanner,
  /BillingV2CheckoutPlanner[\s\S]*readiness\.Authorized[\s\S]*ProviderExternalId[\s\S]*BillingV2CheckoutPlan/,
  "Le futur checkout V2 doit disposer d'un plan local fail-closed avant tout appel provider.",
);
assert.doesNotMatch(
  billingV2CheckoutPlanner,
  /createStripe|createPayPal|HttpClient|https:\/\/api\.stripe|https:\/\/api-m\.paypal/,
  "Le plan checkout V2 local ne doit pas appeler Stripe ou PayPal.",
);
assert.match(
  billingV2OutboxIdempotencyMigration,
  /GROUP BY idempotency_key_hash[\s\S]*HAVING COUNT\(\*\) > 1[\s\S]*ADD COLUMN IF NOT EXISTS idempotency_key_hash CHAR\(64\)[\s\S]*ADD UNIQUE KEY IF NOT EXISTS uq_billing_v2_outbox_idempotency/,
  "La migration 052 doit rendre l'outbox provider V2 idempotente avec une precondition anti-doublons.",
);
assert.match(
  billingV2ProviderCheckoutSessionsMigration,
  /CREATE TABLE IF NOT EXISTS billing_v2_provider_checkout_sessions[\s\S]*provider_checkout_id[\s\S]*provider_subscription_id[\s\S]*approval_url[\s\S]*UNIQUE KEY uq_billing_v2_provider_checkout_idempotency/,
  "La migration 053 doit stocker idempotemment les sessions checkout provider V2 locales.",
);
assert.match(
  billingV2ProviderInboundEventsMigration,
  /GROUP BY provider, environment, provider_checkout_id[\s\S]*HAVING COUNT\(\*\) > 1[\s\S]*GROUP BY provider, environment, provider_subscription_id[\s\S]*HAVING COUNT\(\*\) > 1[\s\S]*CREATE TABLE IF NOT EXISTS billing_v2_provider_events[\s\S]*UNIQUE KEY uq_billing_v2_provider_events_provider_event/,
  "La migration 054 doit preparer des retours\/webhooks provider V2 entrants idempotents avec preconditions anti-doublons.",
);
assert.match(
  billingV2AuthoritativeCheckoutRequestsMigration,
  /CREATE TABLE IF NOT EXISTS billing_v2_authoritative_checkout_requests[\s\S]*UNIQUE KEY uq_billing_v2_authoritative_checkout_request[\s\S]*billing_v2_subscriptions[\s\S]*billing_v2_outbox_events/,
  "La migration 055 doit stocker idempotemment les demandes checkout V2 autoritaires et lier abonnement V2 et outbox.",
);
assert.match(
  billingV2ProviderCheckoutCommandService,
  /IBillingV2ProviderCheckoutCommandService[\s\S]*billing_v2_outbox_events[\s\S]*idempotency_key_hash[\s\S]*ON DUPLICATE KEY UPDATE[\s\S]*billing_v2_audit_log/,
  "La commande provider V2 doit inserer outbox et audit localement avec idempotence.",
);
assert.match(
  billingV2ProviderCheckoutCommandService,
  /AuthoritativeCheckoutEnabled[\s\S]*request\.Readiness\.Authorized[\s\S]*ComputeIdempotencyHash[\s\S]*billing-v2-provider-checkout/,
  "La commande provider V2 doit rester fail-closed et retry-safe.",
);
assert.doesNotMatch(
  billingV2ProviderCheckoutCommandService,
  /createStripe|createPayPal|HttpClient|https:\/\/api\.stripe|https:\/\/api-m\.paypal|STRIPE_SECRET_KEY|PAYPAL_CLIENT_SECRET/,
  "La commande provider V2 ne doit contenir aucun appel ou secret Stripe/PayPal.",
);
assert.match(
  billingCatalog,
  /BILLING_V2_PROVIDER_EXECUTOR_ENABLED/,
  "L'executor provider V2 doit rester derriere un flag dedie.",
);
assert.match(
  billingV2ProviderCheckoutExecutor,
  /DisabledBillingV2ProviderCheckoutExecutor[\s\S]*CanExecute => false[\s\S]*BillingV2StripeCheckoutRequestBuilder[\s\S]*Idempotency-Key[\s\S]*BillingV2PayPalSubscriptionRequestBuilder[\s\S]*PayPal-Request-Id/,
  "L'executor provider V2 doit avoir un mode disabled par defaut et des builders idempotents Stripe/PayPal.",
);
assert.match(
  billingV2ProviderCheckoutExecutor,
  /SecretKey[\s\S]*ClientSecret/,
  "L'executor provider V2 doit rester le seul composant V2 autorise a manipuler les secrets provider.",
);
assert.match(
  apiProgram,
  /ProviderExecutorEnabled[\s\S]*new BillingV2ProviderCheckoutExecutor[\s\S]*DisabledBillingV2ProviderCheckoutExecutor\.Instance/,
  "L'executor provider V2 doit etre remplace par DisabledBillingV2ProviderCheckoutExecutor tant que le flag est off.",
);
assert.match(
  billingV2ProviderOutboxDispatcher,
  /IBillingV2ProviderCheckoutExecutor[\s\S]*_executor\.CanExecute[\s\S]*ReadPendingEventsAsync[\s\S]*ExecuteAsync[\s\S]*BeginTransactionAsync[\s\S]*RecordProviderCheckoutResultAsync[\s\S]*UpdateOutboxEventAsync[\s\S]*billing_v2\.provider_checkout\.create_requested/,
  "Le dispatcher outbox provider V2 doit lire l'outbox, appeler l'executor apres readiness, puis enregistrer resultat et outbox atomiquement.",
);
assert.match(
  billingV2ProviderOutboxDispatcher,
  /INSERT INTO billing_v2_provider_checkout_sessions[\s\S]*ON DUPLICATE KEY UPDATE[\s\S]*INSERT INTO billing_v2_payment_agreements[\s\S]*provider_subscription_id/,
  "Le dispatcher outbox provider V2 doit persister la session checkout et l'accord provider local quand le provider retourne deja une subscription.",
);
assert.match(
  billingV2ProviderOutboxDispatcher,
  /BillingV2ProviderCheckoutSessionPolicy\.Evaluate[\s\S]*FailClosed[\s\S]*ReadProviderCheckoutSessionAsync[\s\S]*FOR UPDATE/,
  "Le dispatcher outbox provider V2 doit verrouiller la session locale et refuser explicitement un retry provider divergent.",
);
assert.match(
  billingV2ProviderOutboxDispatcher,
  /FailClosed[\s\S]*"failed"/,
  "Un conflit de session provider V2 doit etre terminal et visible, pas rejoue en boucle.",
);
assert.match(
  billingV2ProviderOutboxDispatcher,
  /BillingV2ProviderCheckoutSessionPolicy[\s\S]*BILLING_V2_PROVIDER_CHECKOUT_SESSION_CONFLICT[\s\S]*ProviderCheckoutId[\s\S]*ProviderSubscriptionId[\s\S]*ApprovalUrl/,
  "La policy de session provider V2 doit comparer les IDs Stripe/PayPal et l'URL d'approbation deja materialises.",
);
assert.match(
  billingV2ProviderOutboxDispatcher,
  /BillingV2ProviderOutboxGate[\s\S]*ProviderOutboxEnabled[\s\S]*providerExecutorConfigured[\s\S]*BILLING_V2_PROVIDER_OUTBOX_EXECUTOR_NOT_CONFIGURED/,
  "Le dispatcher outbox provider V2 doit rester bloque sans flag/executor et appliquer une politique retry-safe.",
);
assert.match(
  billingV2ProviderOutboxDispatcher,
  /BillingV2ProviderOutboxDispatchPolicy[\s\S]*result\.Succeeded[\s\S]*"processed"[\s\S]*"pending"[\s\S]*retryDelay/,
  "Le dispatcher outbox provider V2 doit appliquer une politique retry-safe.",
);
assert.match(
  billingV2ProviderOutboxDispatcher,
  /TryClaimOutboxEventAsync[\s\S]*status = 'processing'[\s\S]*available_at = DATE_ADD\(UTC_TIMESTAMP\(6\), INTERVAL 5 MINUTE\)[\s\S]*status IN \('pending', 'processing'\)/,
  "Le dispatcher outbox provider V2 doit revendiquer localement un evenement avant tout appel Stripe/PayPal pour eviter deux executions concurrentes.",
);
assert.match(
  billingV2ProviderOutboxDispatcher,
  /BillingV2ProviderOutboxClaimPolicy[\s\S]*ProcessingStatus = "processing"[\s\S]*status is "pending" or ProcessingStatus[\s\S]*availableAtUtc <= nowUtc/,
  "La policy de claim outbox provider V2 doit autoriser pending et processing expire, mais pas un processing actif.",
);
assert.match(
  billingV2ProviderInboundEventService,
  /IBillingV2ProviderInboundEventService[\s\S]*AuthoritativeCheckoutEnabled[\s\S]*BILLING_V2_PROVIDER_INBOUND_GATE_CLOSED/,
  "Le service d'evenements entrants provider V2 doit rester bloque par gate avant activation.",
);
assert.match(
  billingV2ProviderInboundEventService,
  /billing_v2_provider_events[\s\S]*ON DUPLICATE KEY UPDATE/,
  "Le service d'evenements entrants provider V2 doit stocker les events provider avec idempotence.",
);
assert.match(
  billingV2ProviderInboundEventService,
  /existingEvent\.Status[\s\S]*"processed"[\s\S]*BILLING_V2_PROVIDER_EVENT_ALREADY_PROCESSED[\s\S]*ShouldAttemptProcessedReplay\(existingEvent\.ReasonCode\)/,
  "Le service d'evenements entrants provider V2 doit rendre les replays deja reussis idempotents et ne retenter le provisioning que pour une activation deja traitee.",
);
assert.match(
  billingV2ProviderInboundEventService,
  /"failed"[\s\S]*plan\.ReasonCode[\s\S]*BillingV2ProviderInboundEventPlanner/,
  "Le service d'evenements entrants provider V2 doit laisser les events echoues rejouables via le planner.",
);
assert.match(
  billingV2ProviderInboundEventService,
  /BothPresentAndDifferent[\s\S]*BILLING_V2_PROVIDER_CHECKOUT_ID_CONFLICT[\s\S]*BILLING_V2_PROVIDER_SUBSCRIPTION_ID_CONFLICT/,
  "Le planner provider inbound V2 doit refuser les IDs provider contradictoires au lieu d'activer un abonnement local.",
);
assert.match(
  billingV2ProviderInboundEventService,
  /ResolveLocalStateAsync[\s\S]*billing_v2_provider_checkout_sessions[\s\S]*billing_v2_payment_agreements[\s\S]*ApplyPlanAsync[\s\S]*billing_v2_audit_log/,
  "Le service d'evenements entrants provider V2 doit rattacher session checkout, accord provider, abonnement local et audit.",
);
assert.match(
  billingV2ProviderInboundEventService,
  /IBillingV2ProvisioningService _provisioning/,
  "Le provider inbound V2 doit dependre de la gate de provisioning V2, pas d'un moteur AD direct.",
);
assert.match(
  billingV2ProviderInboundEventService,
  /TryTriggerProvisioningAsync[\s\S]*TryReconcileActivatedSubscriptionAsync/,
  "Le provider inbound V2 doit tenter le provisioning V2 via le service dedie apres activation locale.",
);
assert.match(
  billingV2ProviderInboundEventService,
  /BillingV2ProviderInboundProvisioningPolicy\.ShouldAttempt[\s\S]*TryTriggerProvisioningAsync/,
  "Le provider inbound V2 doit tenter le provisioning V2 apres activation locale, via la gate existante et sans nouveau moteur AD.",
);
assert.match(
  billingV2ProviderInboundEventService,
  /ShouldAttemptProcessedReplay[\s\S]*BILLING_V2_PROVIDER_SUBSCRIPTION_ACTIVATED/,
  "Le provider inbound V2 ne doit retenter le provisioning d'un event deja processed que pour une activation provider.",
);
assert.match(
  billingV2ProviderInboundEventService,
  /BillingV2ProviderInboundProvisioningFailurePolicy[\s\S]*ShouldKeepProviderEventProcessed[\s\S]*OperationCanceledException/,
  "Le provider inbound V2 doit conserver l'evenement provider traite si le provisioning post-commit echoue, hors annulation explicite.",
);
assert.match(
  billingV2ProvisioningService,
  /TryReconcileActivatedSubscriptionAsync[\s\S]*LoadActiveSubscriptionCustomerIdAsync[\s\S]*GetCustomerUserLinksAsync[\s\S]*BillingV2ProvisioningReadinessGate\.Evaluate[\s\S]*!decision\.AddOnlyMode[\s\S]*_provisioningService\.ReconcileAsync/,
  "Le provisioning V2 pur doit etre tentable apres activation provider, fail-closed, add-only et via ProvisioningService.",
);
assert.doesNotMatch(
  billingV2ProviderInboundEventService,
  /AddUserToGroupAsync|RemoveUserFromGroupAsync|IAdGroupProvisioner/,
  "Le provider inbound V2 ne doit pas ecrire AD directement.",
);
assert.match(
  billingV2AuthoritativeCheckoutService,
  /IBillingV2AuthoritativeCheckoutService[\s\S]*BillingV2AuthoritativeCheckoutGate[\s\S]*NewSubscriptionsEnabled[\s\S]*AuthoritativeCheckoutEnabled[\s\S]*FirstRealSubscriptionApproved/,
  "Le checkout V2 autoritaire local doit rester derriere les flags nouveaux abonnements, checkout et validation humaine.",
);
// Phase 2 : l'ordre a change car la demande de checkout reference desormais
// l'intention (subscription_change_id) et l'evenement financier
// (billing_event_id). L'abonnement doit donc exister avant l'intention, et
// l'intention avant le BillingEvent. La propriete verifiee reste la meme :
// tout est ecrit dans UNE seule transaction.
assert.match(
  billingV2AuthoritativeCheckoutService,
  /BeginTransactionAsync[\s\S]*InsertSubscriptionAsync[\s\S]*TryInsertIntentAsync[\s\S]*InsertFinalizedBillingEventAsync[\s\S]*InsertCheckoutRequestAsync[\s\S]*InsertItemAsync[\s\S]*InsertSubscriptionPriceLockAsync[\s\S]*InsertOutboxEventAsync[\s\S]*InsertAuditAsync[\s\S]*MarkCheckoutRequestQueuedAsync[\s\S]*CommitAsync/,
  "Le checkout V2 autoritaire local doit orchestrer intention/BillingEvent/abonnement/items/price lock/outbox/audit dans une transaction.",
);
assert.match(
  billingV2AuthoritativeCheckoutService,
  /FindIntentByHashAsync[\s\S]*FindOpenIntentForSelectionAsync/,
  "Le checkout V2 doit resoudre l'intention serveur par cle puis par selection metier, jamais depuis un etat navigateur.",
);
assert.doesNotMatch(
  billingV2StripeRail,
  /line_items\[\d*\]\[price\]|\[price\]"\]\s*=/,
  "Le rail Stripe V2 ne doit jamais laisser un price_id externe fixer le total contractuel.",
);
assert.match(
  billingV2StripeRail,
  /price_data\]\[unit_amount\][\s\S]*billingEvent\.TotalAmountCents|TotalAmountCents[\s\S]*price_data\]\[unit_amount\]/,
  "Le montant envoye a Stripe doit provenir du BillingEvent finalise.",
);
assert.match(
  billingV2AuthoritativeCheckoutService,
  /InsertSubscriptionPriceLockAsync[\s\S]*billing_v2_subscription_price_locks[\s\S]*source_legacy_offer_id[\s\S]*reason/,
  "Le checkout V2 autoritaire doit persister un price lock contractuel local.",
);
assert.match(
  billingV2AuthoritativeCheckoutService,
  /BillingV2AuthoritativeCheckoutPriceLockPolicy[\s\S]*BillingV2PriceLockTypes\.UpfrontPrepaid[\s\S]*UpfrontRecurringAmountCents[\s\S]*BillingV2PriceLockTypes\.MonthlyRecurring[\s\S]*PayableRecurringAmountCents/,
  "Le checkout V2 autoritaire doit creer un price lock contractuel depuis le prix calcule a la souscription.",
);
assert.match(
  billingV2AuthoritativeCheckoutService,
  /INSERT IGNORE INTO billing_v2_authoritative_checkout_requests/,
  "Le checkout V2 autoritaire local doit dedupliquer la demande par cle applicative.",
);
assert.match(
  billingV2AuthoritativeCheckoutService,
  /billing_v2_subscriptions[\s\S]*billing_v2_subscription_items[\s\S]*billing_v2_outbox_events[\s\S]*billing_v2_audit_log/,
  "Le checkout V2 autoritaire local doit materialiser les tables V2 attendues.",
);
assert.match(
  billingV2AuthoritativeCheckoutService,
  /BillingV2CheckoutReadinessRequest[\s\S]*BillingV2CheckoutPlanner\.Plan[\s\S]*BillingV2ProviderCheckoutCommandPlanner\.Plan/,
  "Le checkout V2 autoritaire local doit reutiliser readiness, planner checkout et planner outbox provider existants.",
);
assert.doesNotMatch(
  billingV2AuthoritativeCheckoutService,
  /CreatePendingAsync|CreateBilledPendingAsync|ISubscriptionService|HttpClient|https:\/\/api\.stripe|https:\/\/api-m\.paypal|ProvisioningService/,
  "Le checkout V2 autoritaire local ne doit pas creer de subscription legacy, appeler Stripe/PayPal ou declencher le provisioning.",
);
assert.doesNotMatch(
  billingV2ProviderInboundEventService,
  /HttpClient|https:\/\/api\.stripe|https:\/\/api-m\.paypal|STRIPE_SECRET_KEY|PAYPAL_CLIENT_SECRET/,
  "Le service d'evenements entrants provider V2 ne doit pas appeler Stripe/PayPal ni contenir de secret provider.",
);
assert.doesNotMatch(
  billingV2ProviderOutboxDispatcher,
  /createStripe|createPayPal|HttpClient|https:\/\/api\.stripe|https:\/\/api-m\.paypal|STRIPE_SECRET_KEY|PAYPAL_CLIENT_SECRET/,
  "Le dispatcher outbox provider V2 ne doit pas contenir directement les appels ou secrets Stripe/PayPal.",
);
assert.match(
  apiProgram,
  /if \(billingV2RuntimeConfiguration\.ProviderOutboxEnabled\)[\s\S]*AddHostedService<BillingV2ProviderOutboxWorker>/,
  "Le worker outbox provider V2 doit etre enregistre uniquement quand le flag dedie est actif.",
);
assert.match(
  apiProgram,
  /IBillingV2ProviderInboundEventService[\s\S]*BillingV2ProviderInboundEventService/,
  "Le service provider inbound V2 doit etre enregistre en DI sans route publique active.",
);
assert.match(
  apiProgram,
  /IBillingV2AuthoritativeCheckoutService[\s\S]*BillingV2AuthoritativeCheckoutService/,
  "Le service checkout V2 autoritaire doit etre enregistre en DI sans route publique active.",
);
assert.match(
  apiProgram,
  /IBillingV2AdminReadinessService[\s\S]*BillingV2AdminReadinessService/,
  "Le service readiness admin Billing V2 doit etre enregistre en DI pour validation humaine avant premier abonnement.",
);
assert.match(
  subscriptionContracts,
  /BillingV2AuthoritativeCheckoutPayload[\s\S]*LegacyOfferId[\s\S]*Provider[\s\S]*IdempotencyKey[\s\S]*SuccessUrl[\s\S]*CancelUrl[\s\S]*BillingV2AuthoritativeCheckoutResponse[\s\S]*ApprovalUrl[\s\S]*BillingV2ProviderReturnPayload/,
  "Le contrat API interne checkout V2 autoritaire doit exposer une cle d'idempotence, le resultat outbox local, l'URL provider locale et le retour provider V2.",
);
assert.match(
  apiProgram,
  /\/internal\/portal\/billing-v2\/subscriptions\/checkout[\s\S]*IBillingV2AuthoritativeCheckoutService[\s\S]*ResolveClientSessionAsync[\s\S]*BillingV2AuthoritativeCheckoutPayload[\s\S]*CreateAsync/,
  "L'endpoint interne checkout V2 autoritaire doit etre sessionne, gate via service et audite sans exposer d'effet externe.",
);
assert.match(
  apiProgram,
  /BILLING_V2_CHECKOUT_NOT_READY[\s\S]*billing_v2\.authoritative_checkout_requested/,
  "L'endpoint interne checkout V2 autoritaire doit exposer un refus explicite et auditer les demandes acceptees.",
);
const billingV2BffCheckoutStart = subscribeCreateRoute.indexOf(
  "if (useBillingV2AuthoritativeCheckout)",
);
const billingV2BffCheckoutEnd = subscribeCreateRoute.indexOf(
  'if (rail === "stripe")',
  billingV2BffCheckoutStart,
);
assert.ok(
  billingV2BffCheckoutStart >= 0
    && billingV2BffCheckoutEnd > billingV2BffCheckoutStart,
  "Le bloc BFF Billing V2 checkout doit etre identifiable.",
);
const billingV2BffCheckoutBlock = subscribeCreateRoute.slice(
  billingV2BffCheckoutStart,
  billingV2BffCheckoutEnd,
);
assert.doesNotMatch(
  billingV2BffCheckoutBlock,
  /createStripe|createPayPal|window\.location/,
  "Le BFF public subscriptions/create ne doit pas declencher Stripe/PayPal directement depuis le chemin V2 autoritaire.",
);
assert.match(
  webRuntimeConfig,
  /isBillingV2AuthoritativeCheckoutBffEnabled[\s\S]*BILLING_V2_AUTHORITATIVE_CHECKOUT_BFF_ENABLED/,
  "Le BFF doit avoir un flag dedie pour tenter le checkout V2 autoritaire.",
);
assert.match(
  envExample,
  /BILLING_V2_AUTHORITATIVE_CHECKOUT_BFF_ENABLED=false/,
  "Le flag BFF checkout V2 autoritaire doit etre false par defaut.",
);
assert.match(
  subscribeButton,
  /useRef<string \| null>\(null\)[\s\S]*Idempotency-Key[\s\S]*getOrCreateIdempotencyKey[\s\S]*crypto\?\.randomUUID/,
  "Le bouton de souscription doit fournir une cle d'idempotence stable par tentative checkout.",
);
assert.match(
  subscribeButton,
  /BillingV2PendingProviderSessionCode[\s\S]*BILLING_V2_CHECKOUT_PENDING_PROVIDER_SESSION[\s\S]*BillingV2PendingProviderMaxAttempts[\s\S]*for \([\s\S]*attempt <= BillingV2PendingProviderMaxAttempts[\s\S]*requestBffJson<SubscribeResponse>[\s\S]*requestInit[\s\S]*shouldRetryBillingV2PendingProviderSession\(result, attempt\)[\s\S]*waitForBillingV2PendingProviderRetry/,
  "Le bouton de souscription doit retenter un pending provider V2 de facon bornee avec la meme requete locale.",
);
assert.match(
  subscribeButton,
  /const idempotencyKey = getOrCreateIdempotencyKey[\s\S]*const requestInit[\s\S]*"Idempotency-Key": idempotencyKey[\s\S]*for \(/,
  "Le retry pending provider V2 doit reutiliser la meme cle d'idempotence au lieu d'en creer une par tentative.",
);
assert.match(
  billingV2BffCheckoutBlock,
  /request\.headers\.get\("Idempotency-Key"\)\?\.trim\(\)[\s\S]*BILLING_V2_IDEMPOTENCY_KEY_REQUIRED[\s\S]*status: 400/,
  "Le chemin BFF Billing V2 doit refuser une demande sans cle d'idempotence explicite.",
);
assert.doesNotMatch(
  billingV2BffCheckoutBlock,
  /bff-\$\{correlationId\}|correlationId\}`/,
  "Le chemin BFF Billing V2 ne doit pas inventer une cle d'idempotence depuis le correlation id.",
);
assert.match(
  billingV2BffCheckoutBlock,
  /Idempotency-Key[\s\S]*\/internal\/portal\/billing-v2\/subscriptions\/checkout[\s\S]*result\.approvalUrl[\s\S]*approveUrl: result\.approvalUrl[\s\S]*BILLING_V2_CHECKOUT_PENDING_PROVIDER_SESSION/,
  "Le BFF subscriptions/create doit appeler l'endpoint interne V2 derriere flag, rediriger seulement avec une URL provider deja locale, sinon rester en attente.",
);
assert.match(
  billingV2AuthoritativeCheckoutRequestsMigration,
  /Verification prealable[\s\S]*GROUP BY customer_id, idempotency_key[\s\S]*request_fingerprint_hash[\s\S]*UNIQUE KEY uq_billing_v2_authoritative_checkout_customer_key[\s\S]*UPDATE billing_v2_authoritative_checkout_requests[\s\S]*SHA2\(CONCAT_WS[\s\S]*ADD UNIQUE KEY IF NOT EXISTS[\s\S]*uq_billing_v2_authoritative_checkout_customer_key/,
  "La migration checkout V2 autoritaire doit stocker et backfiller une empreinte de demande pour detecter les replays divergents.",
);
assert.match(
  billingV2AuthoritativeCheckoutService,
  /ComputeRequestFingerprintHash[\s\S]*EnsureSameIdempotentRequest[\s\S]*BILLING_V2_AUTHORITATIVE_CHECKOUT_IDEMPOTENCY_CONFLICT[\s\S]*MatchesRequestFingerprint/,
  "Le checkout V2 autoritaire doit refuser un replay idempotent dont les parametres metier different.",
);
assert.match(
  billingV2AuthoritativeCheckoutService,
  /ReadCheckoutRequestOrNullAsync[\s\S]*WHERE customer_id = @customer_id[\s\S]*AND idempotency_key = @idempotency_key[\s\S]*ORDER BY created_at ASC, id ASC/,
  "Le checkout V2 autoritaire doit rechercher une cle d'idempotence par client avant provider/environnement.",
);
assert.match(
  billingV2AuthoritativeCheckoutService,
  /ReadApprovalUrlAsync[\s\S]*approval_url[\s\S]*billing_v2_provider_checkout_sessions[\s\S]*status = 'pending_approval'/,
  "Le checkout V2 autoritaire doit relire l'URL provider uniquement depuis la session checkout locale persistante.",
);
assert.match(
  billingV2AuthoritativeCheckoutService,
  // Phase 2 : la reponse de rejeu est centralisee dans BuildExistingResultAsync,
  // la variable locale s'appelle desormais `approvalUrl`. La propriete verifiee
  // ne change pas : l'URL provider n'est exposee que sur `Created: false`.
  /ReadApprovalUrlAsync[\s\S]*return new BillingV2AuthoritativeCheckoutResult\([\s\S]*Created: false[\s\S]*approvalUrl/,
  "L'URL provider V2 ne doit etre exposee qu'au retry idempotent d'une demande deja materialisee.",
);
assert.match(
  billingV2AuthoritativeCheckoutService,
  // Phase 2 : le rejeu est resolu par l'intention serveur, plus par la seule
  // demande de checkout. La propriete verifiee est inchangee et meme renforcee :
  // la resolution precede toute lecture mapping/pricing courante.
  /FindIntentByHashAsync[\s\S]*FindOpenIntentForSelectionAsync[\s\S]*BuildExistingResultAsync[\s\S]*ReadMappingAsync/,
  "Un retry checkout V2 deja materialise doit etre resolu avant les lectures mapping/pricing courantes.",
);
assert.match(
  apiProgram,
  /BillingV2AuthoritativeCheckoutResponse\([\s\S]*result\.ApprovalUrl[\s\S]*context\.GetCorrelationId\(\)/,
  "L'endpoint interne checkout V2 doit transmettre l'URL provider locale sans appel externe supplementaire.",
);
assert.match(
  subscribeCreateRoute,
  /provider=stripe[\s\S]*session_id=\{CHECKOUT_SESSION_ID\}[\s\S]*provider=paypal/,
  "Le checkout V2 BFF doit preparer des URLs de retour Stripe et PayPal rattachables localement.",
);
assert.match(
  billingV2ReturnRoute,
  /\/internal\/portal\/billing-v2\/provider-return[\s\S]*providerCheckoutId[\s\S]*providerSubscriptionId[\s\S]*isSuccessfulReturn/,
  "La route BFF retour Billing V2 doit passer par l'API interne et exiger un rattachement local reussi.",
);
assert.match(
  apiProgram,
  /\/internal\/portal\/billing-v2\/provider-return[\s\S]*ResolveClientSessionAsync[\s\S]*BillingV2ProviderReturnPayload[\s\S]*CreateProviderReturn[\s\S]*ProcessAsync/,
  "L'endpoint interne retour provider V2 doit etre sessionne et reutiliser le service inbound idempotent.",
);
assert.match(
  apiProgram,
  /TryCreatePayPalWebhook[\s\S]*billingV2InboundService\.ProcessAsync[\s\S]*webhookService\.ProcessAsync/,
  "Le webhook PayPal interne doit tenter V2 uniquement via extracteur marque avant de conserver le chemin legacy.",
);
assert.match(
  apiProgram,
  /TryCreateStripeWebhook[\s\S]*billingV2InboundService\.ProcessAsync[\s\S]*webhookService\.ProcessAsync/,
  "Le webhook Stripe interne doit tenter V2 uniquement via extracteur marque avant de conserver le chemin legacy.",
);
assert.match(
  billingV2ProviderInboundEventService,
  /ExpectedCustomerId[\s\S]*LocalSubscriptionId[\s\S]*subscription\.customer_id = @expected_customer_id[\s\S]*subscription\.id = @local_subscription_id[\s\S]*TryCreateStripeWebhook[\s\S]*billing_v2_subscription_id[\s\S]*LocalSubscriptionId: metadataSubscriptionId[\s\S]*TryCreatePayPalWebhook[\s\S]*custom_id[\s\S]*LocalSubscriptionId: customId/,
  "Le provider inbound V2 doit filtrer les retours portail par client, rattacher les webhooks marques a l'abonnement local V2 et ne pas detourner les webhooks legacy sans marqueur V2.",
);
assert.match(
  billingV2ProviderInboundEventService,
  /ON DUPLICATE KEY UPDATE[\s\S]*event_type = IF\([\s\S]*status = 'processed'[\s\S]*VALUES\(event_type\)[\s\S]*provider_checkout_id = IF\([\s\S]*VALUES\(provider_checkout_id\)[\s\S]*provider_subscription_id = IF\([\s\S]*VALUES\(provider_subscription_id\)[\s\S]*payload_text = IF\([\s\S]*VALUES\(payload_text\)[\s\S]*status = IF\([\s\S]*'processing'[\s\S]*last_error = IF\(/,
  "Un provider event V2 deja processed doit rester idempotent, mais un event failed/skipped doit rafraichir son payload stocke avant retry.",
);
assert.match(
  subscribeCreateRoute,
  /!useBillingV2AuthoritativeCheckout[\s\S]*isStripeConfigured[\s\S]*!useBillingV2AuthoritativeCheckout[\s\S]*isPayPalConfigured/,
  "Les checks secrets Stripe/PayPal du BFF doivent rester sur le chemin legacy, pas sur le chemin provider V2 API.",
);
assert.match(
  billingV2NewSubscriptionTests,
  /VerifyPresetPlannerCreatesPrimaryUserItems[\s\S]*VerifyPresetPlannerCreatesAdditionalUserEntitlement[\s\S]*VerifyPayPalPaymentAgreementUsesLegacySubscriptionId[\s\S]*VerifyStripePaymentAgreementUsesLegacySubscriptionId[\s\S]*VerifyBillingRailDoesNotInventProviderAgreement[\s\S]*VerifyProviderPriceMappingsMustCoverAllServicePrices[\s\S]*VerifyProviderPriceMappingsDetectMissingServicePrice[\s\S]*VerifyProviderPriceMappingsDetectAmbiguousServicePrice[\s\S]*VerifyLaunchReadinessIgnoresDemoSubscriptions[\s\S]*VerifyLaunchReadinessBlocksRealCustomerSubscriptions[\s\S]*VerifyAuthoritativeCheckoutRequiresDedicatedFlag[\s\S]*VerifyAuthoritativeCheckoutRequiresHumanApproval[\s\S]*VerifyAuthoritativeCheckoutRequiresProviderOutbox[\s\S]*VerifyAuthoritativeCheckoutRequiresProviderExecutor[\s\S]*VerifyAuthoritativeCheckoutBlocksRealLegacySubscriptions[\s\S]*VerifyAuthoritativeCheckoutRequiresVerifiedLaunchSnapshot[\s\S]*VerifyAuthoritativeCheckoutBlocksIncompleteProviderMappings[\s\S]*VerifyAuthoritativeCheckoutBlocksWithoutV2DocumentIssuer[\s\S]*VerifyAuthoritativeCheckoutAllowsWhenV2DocumentIssuerReady[\s\S]*VerifyDocumentSnapshotPlannerPreservesContractedFinancials[\s\S]*VerifyDocumentSnapshotPlannerUsesPriceLockInsteadOfCurrentItems[\s\S]*VerifyProviderPriceMappingsExposeResolvedProviderIds[\s\S]*VerifyCheckoutPlannerRequiresReadiness[\s\S]*VerifyCheckoutPlannerBuildsLocalProviderPlan[\s\S]*VerifyProviderCheckoutCommandRequiresReadiness[\s\S]*VerifyProviderCheckoutCommandUsesStableIdempotency[\s\S]*VerifyProviderCheckoutCommandPayloadContainsResolvedProviderLines[\s\S]*VerifyStripeCheckoutRequestBuilderUsesResolvedPricesAndIdempotency[\s\S]*VerifyPayPalSubscriptionRequestBuilderUsesSinglePlanAndIdempotency[\s\S]*VerifyPayPalSubscriptionRequestBuilderRejectsMultiplePlans[\s\S]*VerifyProviderOutboxWorkerRequiresDedicatedFlag[\s\S]*VerifyProviderOutboxWorkerRequiresExecutor[\s\S]*VerifyProviderOutboxClaimPolicyClaimsPendingAndExpiredProcessing[\s\S]*VerifyProviderOutboxClaimPolicyBlocksActiveProcessing[\s\S]*VerifyProviderOutboxDispatchPolicyMarksSuccessProcessed[\s\S]*VerifyProviderOutboxDispatchPolicyRetriesFailures[\s\S]*VerifyDisabledProviderExecutorCannotExecute[\s\S]*VerifyStripeProviderExecutorUsesFakeHttpAndParsesCheckoutAsync[\s\S]*VerifyPayPalProviderExecutorUsesFakeHttpAndParsesSubscriptionAsync[\s\S]*VerifyProviderInboundReturnLinksLocalSession[\s\S]*VerifyProviderInboundReturnDoesNotTriggerProvisioning[\s\S]*VerifyProviderInboundProcessedReturnDoesNotRetryProvisioning[\s\S]*VerifyProviderInboundEventIsIdempotentAfterSuccess[\s\S]*VerifyProviderInboundProcessedActivationCanRetryProvisioning[\s\S]*VerifyProviderInboundFailedEventCanBeRetried[\s\S]*VerifyProviderInboundActivationCanTriggerProvisioningRetry[\s\S]*VerifyProviderInboundProvisioningFailureKeepsProviderEventProcessed[\s\S]*VerifyProviderInboundProvisioningCancellationCanBubble[\s\S]*VerifyProviderInboundActivationDoesNotDowngradeActiveSubscription[\s\S]*VerifyProviderInboundActivationRequiresProviderSubscriptionId[\s\S]*VerifyProviderInboundRejectsDivergentProviderSubscriptionId[\s\S]*VerifyProviderReturnExtractorScopesToClient[\s\S]*VerifyStripeWebhookExtractorRequiresBillingV2Marker[\s\S]*VerifyStripeWebhookExtractorReadsV2CheckoutSession[\s\S]*VerifyPayPalWebhookExtractorRequiresV2CustomId[\s\S]*VerifyAuthoritativeCheckoutLocalGateRequiresAllFlags[\s\S]*VerifyAuthoritativeCheckoutLocalGateRequiresPersistentSql[\s\S]*VerifyAuthoritativeCheckoutLocalGateRequiresIdempotencyKey[\s\S]*VerifyAuthoritativeCheckoutIdempotencyFingerprintBindsRequest[\s\S]*VerifyAuthoritativeCheckoutCreatesContractualPriceLocks[\s\S]*VerifyAuthoritativeCheckoutLocalGateAllowsReadyRequest/,
  "Les tests nouveaux abonnements V2 doivent couvrir le plan d'items, utilisateurs, accords provider locaux, mappings provider, readiness de lancement, gate checkout autoritaire, plan checkout local, commande outbox provider, builders executor, dispatcher retry-safe, worker fail-closed et evenements entrants provider.",
);
assert.match(
  billingV2NewSubscriptionTests,
  /VerifyAdminReadinessRequiresNoRealLegacySubscriptions[\s\S]*VerifyAdminReadinessRequiresVerifiedLaunchSnapshot[\s\S]*VerifyAdminReadinessRequiresProviderReady[\s\S]*VerifyAdminReadinessExposesBlockingSubscriptions[\s\S]*VerifyAdminReadinessExposesOperationalLimitations[\s\S]*VerifyAdminReadinessBlocksFirstRealSubscriptionWithoutV2InvoicePath[\s\S]*VerifyAdminReadinessAllowsFirstRealSubscriptionWhenHardBlockersCleared/,
  "Les tests nouveaux abonnements V2 doivent couvrir la readiness admin, les abonnements reels bloquants et les limites operationnelles pour le premier vrai abonnement.",
);
assert.match(
  rootPackageJson,
  /--billing-v2-new-subscription/,
  "La suite billing legacy doit executer les tests nouveaux abonnements Billing V2.",
);

const billingV2SeedPriceCodes = billingV2CatalogSeedMigration.match(
  /'[A-Z0-9-]+-(?:MONTHLY|ONE-TIME)-EUR-V1'/g,
) ?? [];
const billingV2SeedPackMappings = billingV2CatalogSeedMigration.match(
  /'PACK-[A-Z0-9-]+'/g,
) ?? [];
const billingV2SeedPresetCodes = billingV2CatalogSeedMigration.match(
  /'pack-[a-z-]+'/g,
) ?? [];
const billingV2SeedLegacyServiceMappings = billingV2CatalogSeedMigration.match(
  /^\s*\('[A-Z0-9-]+',\s*'(?:direct|storage_increment|dependent_tier|absorbed_in_base|legacy_one_time_entitlement)'/gm,
) ?? [];
const billingV2SeedStatements = billingV2CatalogSeedMigration
  .split(/^-- statement-break$/m)
  .map((statement) => statement.trim())
  .filter(Boolean);
const billingV2SeedStatementStarts = billingV2SeedStatements.map(
  (statement) => statement.match(/^(SET NAMES utf8mb4|INSERT (?:IGNORE )?INTO billing_v2_)/gm) ?? [],
);
assert.equal(
  new Set(billingV2SeedPriceCodes).size,
  31,
  "Le seed Billing V2 doit declarer les 31 prix candidats versionnes V1.",
);
assert.equal(
  new Set(billingV2SeedPackMappings).size,
  20,
  "Le seed Billing V2 doit mapper les 20 PACK-* legacy.",
);
assert.equal(
  new Set(billingV2SeedPresetCodes).size,
  4,
  "Le seed Billing V2 doit creer les 4 presets commerciaux candidats.",
);
assert.equal(
  billingV2SeedLegacyServiceMappings.length,
  10,
  "Le seed Billing V2 doit declarer les 10 mappings de briques techniques legacy.",
);
assert.equal(
  billingV2SeedStatements.length,
  62,
  "Le seed Billing V2 doit contenir 62 statements executables pour le runner MariaDB.",
);
assert.ok(
  billingV2SeedStatementStarts.every((starts) => starts.length === 1),
  "Chaque chunk du seed Billing V2 doit contenir exactement un statement SQL executable.",
);
assert.doesNotMatch(
  billingV2CatalogSeedMigration,
  /^\s*(UPDATE|DELETE|DROP|ALTER)\b/im,
  "Le seed Billing V2 ne doit pas modifier ou supprimer de donnees existantes.",
);
assert.doesNotMatch(
  billingV2CatalogSeedMigration,
  /\bcommercial_offers\b/i,
  "Le seed Billing V2 ne doit pas lire directement commercial_offers.",
);
assert.doesNotMatch(
  billingV2CatalogSeedMigration,
  /\s(?:FLOAT|DOUBLE)\s/i,
  "Le seed Billing V2 ne doit pas utiliser de types flottants pour l'argent.",
);
assert.match(
  billingV2CatalogSeedMigration,
  /BASE-SERVICE en V2 ; pas de ligne facturable/,
  "Le decoupage du seed Billing V2 ne doit pas couper les textes SQL contenant un point-virgule.",
);
for (const expectedOption of [
  /'FLEX'(?:\s+AS term_code)?, 'monthly'(?:\s+AS payment_mode)?, 0(?:\s+AS discount_basis_points)?/,
  /'TERM-6', 'monthly', 1000/,
  /'TERM-12', 'monthly', 1500/,
  /'TERM-6', 'upfront', 1500/,
  /'TERM-12', 'upfront', 2000/,
]) {
  assert.match(
    billingV2CatalogSeedMigration,
    expectedOption,
    "Le seed Billing V2 doit conserver les remises documentees par engagement et mode de paiement.",
  );
}
assert.match(
  topologyService,
  /IBillingCatalog _billingCatalog[\s\S]*_billingCatalog\.GetAdminCatalogAsync/,
  "La topologie provisioning doit lire les offres via IBillingCatalog.",
);
assert.match(
  cartService,
  /IBillingCatalog _billingCatalog[\s\S]*_billingCatalog\.GetClientCatalogAsync/,
  "Le panier doit lire sa vision catalogue via IBillingCatalog.",
);
assert.doesNotMatch(
  cartService,
  /ICommercialService _catalog|_catalog\.GetClientCatalogAsync/,
  "Le panier ne doit plus lire le catalogue metier via ICommercialService.",
);
assert.match(
  catalogConfigurationService,
  /IBillingCatalog _billingCatalog[\s\S]*_billingCatalog\.GetClientCatalogAsync/,
  "Le configurateur doit lire sa vision catalogue via IBillingCatalog.",
);
assert.doesNotMatch(
  catalogConfigurationService,
  /ICommercialService _commercialService|_commercialService\.GetClientCatalogAsync/,
  "Le configurateur ne doit plus lire le catalogue metier via ICommercialService.",
);

assert.match(
  subscriptionService,
  /CreatePendingAsync[\s\S]*ResolveSubscribableOfferAsync[\s\S]*_repository\.CreatePendingAsync/,
  "La creation d'abonnement doit passer par la resolution d'offre subscribable.",
);
assert.match(
  subscriptionService,
  /rail == "stripe" \? null : lookup\.ExternalPlanId[\s\S]*rail == "stripe" \? lookup\.ExternalPlanId : null/,
  "CreatePendingAsync doit snapshotter les ids fournisseur dans les colonnes du bon rail.",
);
assert.match(
  subscriptionRepository,
  /INSERT INTO subscriptions[\s\S]*commercial_offer_id[\s\S]*paypal_plan_id[\s\S]*stripe_price_id[\s\S]*public_pack_code[\s\S]*setup_fee_amount_cents[\s\S]*billing_interval_months[\s\S]*commitment_months[\s\S]*payment_mode/,
  "La creation d'abonnement doit conserver les snapshots legacy critiques.",
);
assert.match(
  subscriptionService,
  /if \(string\.Equals\([\s\S]*current\.Status,[\s\S]*"active"[\s\S]*return current;/,
  "ActivateAsync doit rester idempotent si l'abonnement est deja actif.",
);
assert.match(
  subscriptionService,
  /_repository\.ActivateAsync[\s\S]*TryReconcileProvisioningAsync/,
  "L'activation doit declencher le reconcile provisioning legacy.",
);

assert.match(
  renewalWorker,
  /subscription\.Rail,[\s\S]*"billing"/,
  "Le worker de renouvellement doit cibler uniquement le rail billing local.",
);
assert.match(
  renewalWorker,
  /CreateBillingDocumentForSubscriptionAsync[\s\S]*subscription\.CommercialOfferId/,
  "Le renouvellement legacy doit facturer l'offre commerciale rattachee.",
);
assert.match(
  commercialRepository,
  /CreateBillingDocumentForSubscriptionAsync[\s\S]*ReadOfferDetailsAsync\([\s\S]*request\.OfferId/,
  "La creation de document de renouvellement doit relire l'offre legacy.",
);
assert.match(
  subscriptionBillingPriceLocksMigration,
  /CREATE TABLE IF NOT EXISTS subscription_billing_price_locks[\s\S]*UNIQUE KEY uq_subscription_billing_price_locks_active[\s\S]*\(subscription_id, active_lock_slot\)/,
  "La migration 049 doit garantir un seul price lock actif par abonnement.",
);
assert.match(
  subscriptionBillingPriceLocksMigration,
  /INSERT INTO subscription_billing_price_locks[\s\S]*ROW_NUMBER\(\) OVER[\s\S]*commercial_documents document[\s\S]*commercial_document_lines line[\s\S]*commercial_document_line_subscriptions/,
  "La migration 049 doit deriver le backfill depuis les premieres lignes historiques liees a l'abonnement.",
);
assert.doesNotMatch(
  subscriptionBillingPriceLocksMigration,
  /JOIN\s+commercial_offers|offer\.price_amount_cents/i,
  "La migration 049 ne doit pas utiliser le prix courant commercial_offers comme preuve contractuelle.",
);
assert.match(
  subscriptionBillingPriceLocksMigration,
  /CREATE TABLE IF NOT EXISTS subscription_billing_price_lock_review_required[\s\S]*missing_reliable_historical_price/,
  "La migration 049 doit rendre detectables les abonnements sans historique fiable.",
);
assert.match(
  subscriptionService,
  /CreatePendingAsync[\s\S]*EnsurePriceLockAsync\([\s\S]*legacy_subscription_created[\s\S]*CreateBilledPendingAsync[\s\S]*EnsurePriceLockAsync\([\s\S]*legacy_subscription_created[\s\S]*ActivateAsync[\s\S]*legacy_subscription_activated/,
  "La creation et l'activation d'abonnement doivent creer le price lock contractuel.",
);
assert.match(
  commercialRepository,
  /CreateBillingDocumentForSubscriptionAsync[\s\S]*ReadActiveSubscriptionPriceLockAsync[\s\S]*RecordSubscriptionPriceLockReviewRequiredAsync[\s\S]*Cannot create a subscription renewal document without an active contractual price lock/,
  "Le renouvellement legacy sans price lock actif doit etre bloque et marque pour revue.",
);
assert.doesNotMatch(
  commercialRepository,
  /priceLock\?\.UnitPriceCents\s*\?\?\s*offer\.PriceAmountCents/,
  "Le renouvellement legacy ne doit plus fallback silencieusement sur commercial_offers.price_amount_cents.",
);
assert.doesNotMatch(
  commercialRepository,
  /legacy_renewal_first_lock/,
  "Le renouvellement legacy ne doit pas creer silencieusement un price lock depuis l'offre courante.",
);
assert.match(
  billingLegacyIdempotencyTests,
  /VerifyRenewalUsesSubscriptionPriceLockAsync[\s\S]*PriceAmountCents = 7777[\s\S]*UnitPriceCents == 1190[\s\S]*VerifyRenewalWithoutPriceLockIsBlockedAsync[\s\S]*VerifyBackfillUsesHistoricalLineInsteadOfCurrentOfferAsync[\s\S]*currentOfferPriceCents[\s\S]*VerifyBackfillRequiresManualReviewWhenHistoryIsMissing[\s\S]*VerifyNewSubscriptionCreatesPriceLockFromResolvedOfferAsync/,
  "Les tests executables doivent couvrir renouvellement au lock, blocage sans lock, prix historique, revue manuelle et nouveaux abonnements.",
);

assert.match(
  stripeReturnRoute,
  /getStripeCheckoutSession[\s\S]*checkoutSession\.subscriptionId/,
  "Le retour Stripe doit relire la Checkout Session fournisseur.",
);
assert.match(
  stripeReturnRoute,
  /\/internal\/portal\/subscriptions[\s\S]*stripeSubscriptionId/,
  "Le retour Stripe doit retrouver ou persister l'abonnement local avec le stripeSubscriptionId.",
);
assert.match(
  stripeReturnRoute,
  /findReturnedSubscription\([\s\S]*"stripe"[\s\S]*stripeSubscriptionId/,
  "Le retour Stripe doit relire les abonnements locaux et reutiliser un stripeSubscriptionId existant avant creation.",
);
assert.match(
  stripeReturnRoute,
  /return-approved/,
  "Le retour Stripe doit marquer l'abonnement comme approuve apres persistance.",
);
if (!/findReturnedSubscription\([\s\S]*"stripe"/.test(stripeReturnRoute)) {
  noteLegacyBug(
    "LEGACY_STRIPE_RETURN_NOT_IDEMPOTENT",
    "Un double retour Stripe reposte /internal/portal/subscriptions sans lookup local prealable de stripeSubscriptionId.",
  );
}

assert.match(
  stripeWebhookMigration,
  /UNIQUE KEY ux_stripe_webhook_events_event_id \(event_id\)/,
  "Stripe event_id doit rester unique.",
);
assert.match(
  stripeWebhookService,
  /existing is not null[\s\S]*existing\.Status is not "failed"[\s\S]*Duplicate Stripe webhook event[\s\S]*return new StripeWebhookProcessingResult/,
  "Un webhook Stripe deja traite doit etre ignore.",
);
assert.match(
  stripeWebhookService,
  /Retrying Stripe webhook event[\s\S]*after prior status/,
  "Un webhook Stripe failed doit pouvoir etre retente.",
);
assert.match(
  stripeWebhookService,
  /HasProcessedInvoiceSuccessEventAsync[\s\S]*return "ignored"/,
  "Les evenements Stripe invoice success doubles doivent etre ignores par invoice id.",
);
assert.match(
  paypalWebhookMigration,
  /UNIQUE KEY ux_paypal_webhook_events_event_id \(event_id\)/,
  "PayPal event_id doit rester unique.",
);
assert.match(
  paypalWebhookService,
  /existing is not null[\s\S]*Duplicate PayPal webhook event[\s\S]*return new PayPalWebhookProcessingResult/,
  "Un webhook PayPal deja recu doit etre ignore.",
);
assert.match(
  paypalWebhookService,
  /existing\.Status is "failed"[\s\S]*Retrying PayPal webhook event/,
  "Un webhook PayPal failed doit pouvoir etre retente.",
);
if (!/existing\.Status is not "failed"|Retrying PayPal webhook event/.test(paypalWebhookService)) {
  noteLegacyBug(
    "LEGACY_PAYPAL_FAILED_WEBHOOK_NOT_RETRIED",
    "Un webhook PayPal marque failed est ignore au retry car tout event_id existant retourne immediatement.",
  );
}

assert.match(
  catalogTopologyMigration,
  /WHEN 'pack-acces-distance' THEN '\["STOCK-PERSO-32","SAVE-PERSO","ACCES-VPN","SUPERV-SERVICE","SUPPORT-LV1"\]'/,
  "Le pack Acces doit porter ACCES-VPN dans les references techniques legacy.",
);
assert.match(
  catalogTopologyMigration,
  /WHEN 'pack-bureau-windows-distance' THEN '\["STOCK-PERSO-32","SAVE-PERSO","ACCES-VPN","ACCES-RDS","SUPERV-SERVICE","SUPPORT-LV1"\]'/,
  "Le pack Bureau doit porter ACCES-VPN et ACCES-RDS.",
);
assert.match(
  catalogTopologyMigration,
  /WHEN 'pack-pro-association' THEN '\["USER-ADD","STOCK-PERSO-32","STOCK-SUP-32","ACCES-VPN","SAVE-PERSO","SUPERV-SERVICE","SUPPORT-LV1","DOC-TECH"\]'/,
  "Le pack Pro legacy doit conserver ses briques techniques historiques.",
);
assert.match(
  catalogTopologyMigration,
  /WHEN 'ACCES-VPN' THEN '\["GG_VPN"\]'[\s\S]*WHEN 'ACCES-RDS' THEN '\["GG_RDS"\]'[\s\S]*WHEN 'NEXTCLOUD' THEN '\["GG_NextCloud"\]'/,
  "Les services techniques VPN/RDS/Nextcloud doivent rester mappes aux groupes AD legacy.",
);
assert.match(
  topologyService,
  /ResolveTechnicalRefsForOffer\(offer\)[\s\S]*ResolveServiceGroups\(snapshot, technicalServiceReference\)/,
  "Le provisioning doit resoudre les groupes depuis les references techniques, pas depuis le nom commercial.",
);
assert.match(
  subscriptionProvisioningConfiguration,
  /ACCES-RDS[\s\S]*GG_RDS[\s\S]*ACCES-VPN[\s\S]*GG_VPN[\s\S]*NEXTCLOUD[\s\S]*GG_NextCloud/,
  "La configuration runtime doit conserver les mappings service -> groupes AD pour RDS, VPN et Nextcloud.",
);
assert.match(
  envExample,
  /SUBSCRIPTION_PROVISIONING_GROUPS__ACCES-RDS=GG_RDS[\s\S]*SUBSCRIPTION_PROVISIONING_GROUPS__ACCES-VPN=GG_VPN[\s\S]*SUBSCRIPTION_PROVISIONING_SERVICE_GROUPS__NEXTCLOUD=GG_NextCloud/,
  ".env.example doit documenter les mappings AD legacy RDS/VPN et l'exemple Nextcloud.",
);
assert.match(
  provisioningManager,
  /activeSubscriptions[\s\S]*ResolveMappedGroupsAsync[\s\S]*reconciledGroups\.Add/,
  "Le reconcile doit proteger l'union des groupes de tous les abonnements actifs du client.",
);
assert.match(
  provisioningManager,
  /context\.MappedGroups\.Count == 0[\s\S]*context\.ReconciledGroups\.Count == 0[\s\S]*PROVISIONING_NOT_REQUIRED|PROVISIONING_MAPPING_EMPTY/,
  "Un mapping vide doit etre classe sans side effect utile.",
);
assert.match(
  provisioningService,
  /request\.ManagedGroupSamAccountNames\.Count == 0[\s\S]*PROVISIONING_MAPPING_EMPTY/,
  "Un reconcile sans groupes manages doit rester no-op.",
);
assert.match(
  provisioningService,
  /RequiresConfiguredGroupDistinguishedNames[\s\S]*PROVISIONING_GROUP_NOT_CONFIGURED/,
  "Un groupe AD manage sans DN configure doit bloquer le reconcile avant modification.",
);
assert.match(
  provisioningService,
  /var shouldAdd = desiredGroups\.Contains\(groupSamAccountName\);[\s\S]*var operation = shouldAdd \? "add" : "remove";/,
  "Le comportement legacy retire les groupes manages absents du desir final.",
);
assert.match(
  provisioningService,
  /ExecuteWithRetryAsync[\s\S]*AD_UNAVAILABLE/,
  "Les operations AD doivent etre retry-safe en cas d'indisponibilite temporaire.",
);

assert.match(
  provisioningMigration,
  /idempotency_key_hash CHAR\(64\)/,
  "Les actions de provisioning doivent conserver une empreinte d'idempotence.",
);
assert.match(
  provisioningManager,
  /ComputeIdempotencyKeyHash\(context\)/,
  "Le manager doit calculer une cle d'idempotence pour le contexte de reconcile.",
);
assert.match(
  provisioningActionRepository,
  /INSERT INTO ad_actions[\s\S]*idempotency_key_hash/,
  "Les actions AD doivent stocker idempotency_key_hash.",
);
assert.match(
  activeProvisioningIdempotencyMigration,
  /status IN \('requested', 'running'\)[\s\S]*HAVING COUNT\(\*\) > 1[\s\S]*idempotency_active_hash CHAR\(64\)[\s\S]*UNIQUE KEY IF NOT EXISTS ux_ad_actions_active_idempotency/,
  "La migration AD doit verifier les doublons actifs avant d'ajouter l'unicite active.",
);
assert.match(
  provisioningActionRepository,
  /exception\.Number == 1062[\s\S]*FindActiveByHashAsync/,
  "Le repository AD doit absorber la collision unique concurrente.",
);
assert.match(
  provisioningActionRepository,
  /FindActiveByHashAsync[\s\S]*idempotency_active_hash[\s\S]*status IN \('requested', 'running'\)/,
  "Le repository AD doit relire l'action active et absorber la collision unique concurrente.",
);
assert.match(
  provisioningActionRepository,
  /idempotency_active_hash = NULL/,
  "Une action AD terminee doit liberer la cle active pour permettre un retry futur.",
);
assert.match(
  provisioningManager,
  /!actionCreate\.Created[\s\S]*skipped duplicate active action[\s\S]*return BuildSummary/,
  "Le manager AD doit court-circuiter l'execution quand une action active equivalente existe deja.",
);
if (
  !/ux_ad_actions_active_idempotency/i.test(activeProvisioningIdempotencyMigration)
  && !/ON DUPLICATE KEY|SELECT[\s\S]*idempotency_key_hash/i.test(
    provisioningActionRepository,
  )
) {
  noteLegacyBug(
    "LEGACY_PROVISIONING_ACTIONS_NOT_DEDUPED",
    "idempotency_key_hash est indexe mais ni unique ni relu avant insert, donc deux demandes identiques peuvent creer deux actions ad_actions.",
  );
}

if (knownLegacyBugs.length > 0) {
  console.warn("Bugs legacy documentes par le test:");
  for (const bug of knownLegacyBugs) {
    console.warn(`- ${bug.code}: ${bug.message}`);
  }
}

console.log(
  "Verification du contrat de non-regression Billing legacy reussie.",
);

function parseCommercialOfferInsert(sql) {
  const insertMatch = sql.match(
    /INSERT INTO commercial_offers\s*\(([\s\S]*?)\)\s*VALUES/i,
  );
  assert.ok(insertMatch, "INSERT INTO commercial_offers doit exister.");

  const columns = insertMatch[1]
    .split(",")
    .map((column) => column.trim())
    .filter(Boolean);
  const valuesStart = insertMatch.index + insertMatch[0].length;
  const duplicateIndex = sql.indexOf("ON DUPLICATE KEY UPDATE", valuesStart);
  assert.ok(duplicateIndex > valuesStart, "La clause ON DUPLICATE doit exister.");

  const valuesBody = sql.slice(valuesStart, duplicateIndex);
  const tuples = extractTopLevelTuples(valuesBody);
  return tuples.map((tuple) => {
    const values = splitSqlFields(tuple).map(parseSqlValue);
    assert.equal(
      values.length,
      columns.length,
      "Chaque tuple commercial_offers doit avoir le bon nombre de colonnes.",
    );
    return Object.fromEntries(
      columns.map((column, index) => [column, values[index]]),
    );
  });
}

function extractTopLevelTuples(source) {
  const tuples = [];
  let inString = false;
  let depth = 0;
  let start = -1;

  for (let index = 0; index < source.length; index++) {
    const char = source[index];
    if (char === "'") {
      if (inString && source[index + 1] === "'") {
        index++;
        continue;
      }
      inString = !inString;
      continue;
    }
    if (inString) {
      continue;
    }
    if (char === "(") {
      if (depth === 0) {
        start = index + 1;
      }
      depth++;
      continue;
    }
    if (char === ")") {
      depth--;
      if (depth === 0 && start >= 0) {
        tuples.push(source.slice(start, index));
        start = -1;
      }
    }
  }

  return tuples;
}

function splitSqlFields(tuple) {
  const fields = [];
  let inString = false;
  let depth = 0;
  let start = 0;

  for (let index = 0; index < tuple.length; index++) {
    const char = tuple[index];
    if (char === "'") {
      if (inString && tuple[index + 1] === "'") {
        index++;
        continue;
      }
      inString = !inString;
      continue;
    }
    if (inString) {
      continue;
    }
    if (char === "(") {
      depth++;
      continue;
    }
    if (char === ")") {
      depth--;
      continue;
    }
    if (char === "," && depth === 0) {
      fields.push(tuple.slice(start, index).trim());
      start = index + 1;
    }
  }

  fields.push(tuple.slice(start).trim());
  return fields;
}

function parseSqlValue(rawValue) {
  const value = rawValue.trim();
  if (value === "NULL") {
    return null;
  }
  if (/^-?\d+$/.test(value)) {
    return Number(value);
  }
  if (value.startsWith("'") && value.endsWith("'")) {
    return value.slice(1, -1).replaceAll("''", "'");
  }
  return value;
}
