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
const listPage = await read("app/formules/page.tsx");
const configuratorPage = await read("app/formules/[code]/page.tsx");
const configurator = await read(
  "components/BillingV2FormuleConfigurator.tsx",
);
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
  /function readSelection/,
  "La route de devis doit reconstruire strictement la selection.",
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

// --- 3. Engagements exposes ----------------------------------------------

assert.match(
  catalogSeed,
  /new\("FLEX", "Sans engagement", 1, 0\)/,
  "Sans engagement a 0 %.",
);
assert.match(
  catalogSeed,
  /new\("TERM-6", "Engagement 6 mois", 6, 1000\)/,
  "Six mois a -10 %.",
);
assert.match(
  catalogSeed,
  /new\("TERM-12", "Engagement 12 mois", 12, 1500\)/,
  "Douze mois a -15 %.",
);

// Le paiement comptant reste masque au lancement.
assert.match(
  catalogService,
  /option_row\.payment_mode = 'monthly'/,
  "Seules les options mensuelles sont projetees.",
);
assert.match(
  catalogService,
  /mapping\.payment_mode = 'monthly'/,
  "Seules les routes de checkout mensuelles sont exposees.",
);
// Les commentaires peuvent expliquer pourquoi le comptant reste masque ; le
// code, lui, ne doit en porter aucune trace.
const catalogSeedCode = catalogSeed
  .split(/\r?\n/)
  .filter((line) => !line.trimStart().startsWith("//"))
  .join("\n");
assert.doesNotMatch(
  catalogSeedCode,
  /upfront/i,
  "Aucune variante comptant dans le repli catalogue.",
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
  /"\/api\/subscriptions\/create"/,
  "Le bouton final doit rejoindre le parcours de souscription existant.",
);
assert.match(
  configurator,
  /"Idempotency-Key": crypto\.randomUUID\(\)/,
  "Le checkout doit porter une cle d'idempotence.",
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
assert.match(
  quoteBuilder,
  /CheckoutCustomConfiguration/,
  "Une configuration personnalisee doit etre signalee comme non souscriptible.",
);

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
  "baselineMonthlyAmountCents",
]) {
  assert.match(
    sharedTypes,
    new RegExp(contract),
    `Le contrat partage ${contract} doit exister.`,
  );
}

console.log("Contrat formules Billing V2 verifie.");
