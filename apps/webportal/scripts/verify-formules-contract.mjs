import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

// --- Artefacts ---
const sharedTypes = await read("../../packages/shared/src/index.ts");
const catalogSeed = await read(
  "../../apps/api-internal/Services/BillingV2PublicCatalogSeed.cs",
);
const catalogService = await read(
  "../../apps/api-internal/Services/BillingV2PublicCatalogService.cs",
);
const selectionPolicy = await read(
  "../../apps/api-internal/Services/BillingV2PublicSelectionPolicy.cs",
);
const quoteBuilder = await read(
  "../../apps/api-internal/Services/BillingV2PublicQuoteBuilder.cs",
);
const catalogModel = await read(
  "../../apps/api-internal/Services/BillingV2PublicCatalogModel.cs",
);
const programCs = await read("../../apps/api-internal/Program.cs");
const internalApi = await read("lib/internal-api.ts");
const helpers = await read("lib/billing-v2-formules.ts");
const quoteRoute = await read("app/api/formules/devis/route.ts");
const subscribeRoute = await read("app/api/formules/souscrire/route.ts");
const selectionReader = await read("lib/billing-v2-selection.ts");
const checkoutService = await read(
  "../../apps/api-internal/Services/BillingV2AuthoritativeCheckoutService.cs",
);
const nativeResolver = await read(
  "../../apps/api-internal/Services/BillingV2NativeSelectionResolver.cs",
);
const listPage = await read("app/formules/page.tsx");
const configuratorPage = await read("app/formules/[code]/page.tsx");
const configurator = await read(
  "components/BillingV2FormuleConfigurator.tsx",
);
const appShell = await read("components/AppShell.tsx");
const loginPage = await read("app/login/page.tsx");
const loginForm = await read("components/LoginForm.tsx");
const publicRouteConfig = await read("lib/public-route-config.ts");
const offersPage = await read("app/offres/page.tsx");
const sitemap = await read("app/sitemap.ts");

// --- 1. Le serveur reste la seule autorite financiere ---------------------

assert.match(
  quoteBuilder,
  /pricing\.Calculate\(new BillingV2PricingRequest\(/,
  "Le devis doit passer par BillingV2PricingEngine.",
);

assert.doesNotMatch(
  helpers,
  /amountCents\s*\+|\+\s*.*amountCents|reduce\(/,
  "Le webportal ne doit additionner aucun montant.",
);

assert.doesNotMatch(
  configurator,
  /monthlyAfterDiscountCents\s*=|amountCents\s*\*|\*\s*quantity/,
  "Le configurateur affiche les montants, il ne les calcule pas.",
);

assert.doesNotMatch(
  quoteRoute,
  /amountCents|monthlyAfterDiscountCents|totalDueNowCents/,
  "La route de devis ne doit relayer aucun montant venu du navigateur.",
);

assert.match(
  quoteRoute,
  /readBillingV2SelectionPayload/,
  "La route de devis doit reconstruire strictement la selection.",
);
assert.match(
  subscribeRoute,
  /readBillingV2SelectionPayload/,
  "La souscription doit reconstruire la selection avec le meme lecteur.",
);
assert.doesNotMatch(
  subscribeRoute,
  /amountCents|totalDueNow[^C]|monthlyAfterDiscountCents|discountBasisPoints/,
  "La souscription ne relaie aucun montant venu du navigateur.",
);
// Le lecteur partage est la seule porte d'entree : tout champ absent de cette
// liste ne peut pas atteindre API-INTERNAL.
assert.doesNotMatch(
  selectionReader,
  /amount|cents|price|discount/i,
  "Le lecteur de selection n'accepte aucun champ tarifaire.",
);

// La charge utile acceptee par l'API ne porte aucun champ tarifaire.
const selectionInputBlock = catalogModel.slice(
  catalogModel.indexOf("class BillingV2PublicSelectionInput"),
  catalogModel.indexOf("public sealed record BillingV2PublicQuoteLine"),
);
assert.ok(
  selectionInputBlock.length > 0,
  "Le contrat d'entree public doit exister.",
);
assert.doesNotMatch(
  selectionInputBlock,
  /Amount|Cents|Price/i,
  "Aucun montant ne doit etre accepte depuis le navigateur.",
);

// --- 2. Les quatre formules publiques ------------------------------------

for (const presetCode of [
  "pack-dossier-securise",
  "pack-acces-distance",
  "pack-bureau-windows-distance",
  "pack-pro-association",
]) {
  assert.match(
    catalogSeed,
    new RegExp(presetCode),
    `Le repli catalogue doit porter la formule ${presetCode}.`,
  );
}

// Les prix du repli doivent rester ceux de la migration 048.
const migration = await read(
  "../../apps/api-internal/Migrations/MariaDb/048_billing_v2_catalog_seed.sql",
);
const seededPrices = [
  ["BASE-SERVICE-MONTHLY-EUR-V1", 690],
  ["STORAGE-PERSONAL-32-MONTHLY-EUR-V1", 300],
  ["STORAGE-PERSONAL-64-MONTHLY-EUR-V1", 500],
  ["BACKUP-PERSONAL-32-MONTHLY-EUR-V1", 200],
  ["BACKUP-PERSONAL-64-MONTHLY-EUR-V1", 300],
  ["STORAGE-SHARED-128-MONTHLY-EUR-V1", 890],
  ["BACKUP-SHARED-128-MONTHLY-EUR-V1", 500],
  ["VPN-ACCESS-ESSENTIAL-MONTHLY-EUR-V1", 390],
  ["VPN-ACCESS-PLUS-MONTHLY-EUR-V1", 590],
  ["RDS-ACCESS-MONTHLY-EUR-V1", 1590],
  ["USER-ADDITIONAL-MONTHLY-EUR-V1", 390],
  ["SUPPORT-PLUS-MONTHLY-EUR-V1", 990],
];

for (const [priceCode, amountCents] of seededPrices) {
  assert.match(
    migration,
    new RegExp(`'${priceCode}'[^\\r\\n]*?\\b${amountCents}\\b`),
    `La migration 048 doit porter ${priceCode} a ${amountCents} centimes.`,
  );
  assert.match(
    catalogSeed,
    new RegExp(`\\b${amountCents}\\b`),
    `Le repli catalogue doit reprendre ${amountCents} centimes.`,
  );
}

// --- 3. Engagements et modes de reglement --------------------------------

// La matrice remise x mode de reglement doit rester celle de la migration 048.
for (const [term, mode, basisPoints] of [
  ["FLEX", "monthly", 0],
  ["TERM-6", "monthly", 1000],
  ["TERM-6", "upfront", 1500],
  ["TERM-12", "monthly", 1500],
  ["TERM-12", "upfront", 2000],
]) {
  // La premiere ligne du seed est aliasee (`'FLEX' AS term_code`), les
  // suivantes non : on verifie donc la coexistence sur une meme ligne.
  const seeded = migration
    .split(String.fromCharCode(10))
    .some(
      (line) =>
        line.includes(`'${term}'`)
        && line.includes(`'${mode}'`)
        && new RegExp(String.raw`(^|[ ,])${basisPoints}([ ,]|$)`).test(line),
    );
  assert.ok(
    seeded,
    `La migration 048 doit porter ${term}/${mode} a ${basisPoints} points.`,
  );
  const seedMode = mode === "monthly" ? "Monthly" : "Upfront";
  assert.ok(
    catalogSeed.includes(`BillingV2PaymentModes.${seedMode}, ${basisPoints})`),
    `Le repli catalogue doit reprendre ${term}/${mode}.`,
  );
}

// Les drapeaux du catalogue restent l'autorite : une duree qui n'autorise pas
// un mode de reglement ne doit pas pouvoir l'exposer.
assert.match(
  catalogService,
  /term\.allow_monthly_payment = 1/,
  "Le mensuel reste conditionne au drapeau du catalogue.",
);
assert.match(
  catalogService,
  /term\.allow_upfront_payment = 1/,
  "Le comptant reste conditionne au drapeau du catalogue.",
);
assert.match(
  selectionPolicy,
  /BILLING_V2_PUBLIC_PAYMENT_MODE_UNAVAILABLE/,
  "Un mode de reglement non ouvert doit etre refuse en ferme.",
);


// --- 4. Dependances du catalogue respectees ------------------------------

assert.match(
  selectionPolicy,
  /ResolveTierByNumericValue\(\s*catalog,\s*BillingV2PublicCatalogCodes\.BackupPersonal/,
  "Le palier de sauvegarde personnelle suit la capacite couverte.",
);
assert.match(
  selectionPolicy,
  /BILLING_V2_PUBLIC_SHARED_BACKUP_WITHOUT_STORAGE/,
  "La sauvegarde partagee exige un espace partage.",
);
assert.match(
  selectionPolicy,
  /requirePublic\s*&&\s*!tier\.PublicSelectable/,
  "Un palier non public ne doit jamais etre selectionnable.",
);

// --- 5. Le checkout rejoint le parcours authoritative existant -----------

assert.match(
  configurator,
  /"\/api\/formules\/souscrire"/,
  "Le bouton final doit rejoindre le parcours de souscription V2 native.",
);
assert.match(
  subscribeRoute,
  /"\/internal\/portal\/billing-v2\/subscriptions\/checkout"/,
  "La souscription doit passer par le checkout authoritative existant.",
);
assert.match(
  configurator,
  /"Idempotency-Key": crypto\.randomUUID\(\)/,
  "Le checkout doit porter une cle d'idempotence.",
);
assert.match(
  configurator,
  /resolvePortalAreaUrl\([\s\S]*"client"[\s\S]*continuationPath/,
  "Une session absente sur www doit poursuivre le configurateur sur l'hote client.",
);
assert.match(
  publicRouteConfig,
  /hostname === family\.client[\s\S]*isClientCheckoutContinuationPath\(pathname\)/,
  "Seul l'hote client doit pouvoir servir localement la continuation /formules.",
);
assert.match(
  publicRouteConfig,
  /isClientCheckoutContinuationPath[\s\S]*\[a-z0-9-\]\+\$/,
  "Le chemin de reprise doit etre borne a un unique code de formule.",
);
assert.match(
  appShell,
  /keepAuthenticatedCheckoutShell[\s\S]*portalArea === "client"[\s\S]*client_user/,
  "Le configurateur servi sur dashboard doit reprendre le shell de la session client.",
);
assert.match(
  loginPage,
  /resolveClientCheckoutContinuationPath\(query\.next\)/,
  "La page de connexion doit valider strictement le chemin de reprise.",
);
assert.match(
  loginForm,
  /result\.user\.role === "client_user" && continuationPath[\s\S]*resolvePortalAreaUrl/,
  "Apres connexion client, le formulaire doit reprendre le configurateur demande.",
);
assert.doesNotMatch(
  configurator,
  /stripe\.com|stripePriceId|price_/i,
  "Aucune logique Stripe ne doit etre recreee cote front.",
);
assert.doesNotMatch(
  quoteBuilder,
  /stripePriceId|price_id/i,
  "Aucun identifiant de prix provider ne doit reapparaitre.",
);
assert.match(
  quoteBuilder,
  /BillingV2AuthoritativeCheckoutReadiness/,
  "Le devis doit remonter le motif du gate authoritative.",
);
assert.doesNotMatch(
  subscribeRoute,
  /stripe\.com|stripePriceId|price_id|price_data/i,
  "Aucune logique Stripe ne doit etre recreee dans le BFF.",
);
assert.doesNotMatch(
  nativeResolver,
  /stripePriceId|stripe_price|provider_external_id|price_data/i,
  "La resolution native ne connait aucun identifiant de prix fournisseur.",
);

// --- 5 bis. Souscription V2 native ---------------------------------------

// Le checkout authoritative doit accepter une selection sans offre legacy,
// tout en conservant le chemin historique.
assert.match(
  checkoutService,
  /BillingV2PublicSelection\? Selection/,
  "Le checkout authoritative doit accepter une selection V2 native.",
);
assert.match(
  checkoutService,
  /BILLING_V2_CHECKOUT_AMBIGUOUS_SELECTION/,
  "Une demande portant les deux identites doit etre refusee.",
);
assert.match(
  checkoutService,
  /BILLING_V2_LEGACY_OFFER_MAPPING_NOT_FOUND/,
  "Le parcours legacy doit rester en place.",
);
// Le Pricing Engine n'est pas duplique : la composition native retombe sur le
// meme calcul que le preset.
assert.match(
  checkoutService,
  /_pricing\.Calculate\(new BillingV2PricingRequest\(/,
  "Le checkout doit recalculer le prix avec le Pricing Engine.",
);
assert.ok(
  (checkoutService.match(/_pricing\.Calculate\(/g) ?? []).length === 1,
  "Un seul point de calcul tarifaire dans le checkout authoritative.",
);
// L'ancre d'idempotence devient la configuration elle-meme.
assert.match(
  checkoutService,
  /BillingV2CheckoutSelectionFingerprint/,
  "L'identite metier d'une demande doit etre l'empreinte de configuration.",
);
assert.match(
  nativeResolver,
  /BillingV2PublicSelectionPolicy\.Resolve/,
  "La selection doit etre revalidee cote serveur avant toute ecriture.",
);
assert.match(
  nativeResolver,
  /BillingV2ServicePriceResolutionPolicy\.Resolve/,
  "Les prix natifs doivent passer par la resolution d'ambiguite partagee.",
);
for (const forbiddenWrite of [
  "INSERT INTO",
  "UPDATE ",
  "DELETE FROM",
  "ALTER TABLE",
  "CREATE TABLE",
]) {
  assert.ok(
    !nativeResolver.includes(forbiddenWrite),
    `La resolution native doit rester en lecture seule (${forbiddenWrite}).`,
  );
}

// --- 6. Projection strictement en lecture --------------------------------

for (const forbiddenWrite of [
  "INSERT INTO",
  "UPDATE ",
  "DELETE FROM",
  "ALTER TABLE",
  "CREATE TABLE",
]) {
  assert.ok(
    !catalogService.includes(forbiddenWrite),
    `Le catalogue public ne doit contenir aucune ecriture (${forbiddenWrite}).`,
  );
}

assert.match(
  catalogService,
  /information_schema\.tables/,
  "La precondition de schema doit etre verifiee en lecture seule.",
);

// --- 7. Cablage des pages -------------------------------------------------

assert.match(
  programCs,
  /"\/internal\/portal\/billing-v2\/formules"/,
  "L'endpoint catalogue doit exister.",
);
assert.match(
  programCs,
  /"\/internal\/portal\/billing-v2\/formules\/devis"/,
  "L'endpoint de devis doit exister.",
);
assert.match(
  internalApi,
  /getBillingV2FormulesCatalog/,
  "Le webportal doit lire le catalogue via API-INTERNAL.",
);
assert.match(
  internalApi,
  /EMPTY_BILLING_V2_CATALOG/,
  "Le repli webportal doit etre vide, jamais tarifaire.",
);
assert.match(
  listPage,
  /Configurer/,
  "La page formules doit proposer l'action Configurer.",
);
assert.match(
  configuratorPage,
  /BillingV2FormuleConfigurator/,
  "La page de configuration doit monter le configurateur.",
);
assert.match(
  offersPage,
  /href="\/formules"/,
  "La page offres doit renvoyer vers les formules.",
);
assert.match(
  sitemap,
  /path: "\/formules"/,
  "Le hub des formules doit etre declare au sitemap.",
);

// --- 8. Contrats partages -------------------------------------------------

for (const contract of [
  "BillingV2PublicCatalog",
  "BillingV2PublicPreset",
  "BillingV2PublicQuote",
  "BillingV2PublicSelection",
  "BillingV2PublicPaymentOption",
  "baselineMonthlyAmountCents",
  "commitmentSavingsCents",
  "paymentMode",
]) {
  assert.match(
    sharedTypes,
    new RegExp(contract),
    `Le contrat partage ${contract} doit exister.`,
  );
}

console.log("Contrat formules Billing V2 verifie.");
