import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const projection = await read("lib/public-commercial-catalog.ts");
const tariffsPage = await read("app/tarifs/page.tsx");
const tariffsComponent = await read("components/PublicCommercialTariffCatalog.tsx");
const orderingModel = await read("lib/public-commercial-ordering.ts");
const shared = await read("../../packages/shared/src/index.ts");

assert.match(shared, /type PublicCommercialPriceType = "fixed" \| "from" \| "quote"/);
assert.match(shared, /type PublicCommercialOrderingMode = "direct" \| "offer_component" \| "quote"/);
assert.match(shared, /type PublicCommercialCatalog =/);
assert.match(shared, /initialFees: PublicCommercialMoney\[\]/);
assert.match(shared, /orderingMode: PublicCommercialOrderingMode/);
assert.match(shared, /publicOrderingMode: PublicCommercialOrderingMode \| null/);
assert.match(shared, /offerCount: number/);
assert.match(shared, /directlyOrderable: boolean/);
assert.match(shared, /requiresQuote: boolean/);

assert.match(projection, /buildPublicCommercialCatalog/);
assert.match(projection, /catalog\.services[\s\S]*filter\(\(service\) => service\.publicVisible\)/);
assert.match(projection, /service\.flatMonthlyAmountCents/);
assert.match(projection, /tier\.monthlyAmountCents/);
assert.match(projection, /billingCadence === "one_time"[\s\S]*chargeTrigger === "initial_subscription"/);
assert.match(projection, /TAX_NOTICE = "Montants affichés hors taxes applicables\."/);
assert.match(projection, /autorite fiscale suffisante/);
assert.match(projection, /ne signifie pas qu'un service est achetable seul/);
assert.match(projection, /resolvePublicCommercialOrdering/);
assert.match(projection, /service\.publicOrderingMode/);
assert.match(projection, /resolveLegacyOrderingMode/);
assert.match(projection, /countPublicOffersForService/);
assert.match(orderingModel, /requestedMode \?\? input\.legacyMode/);
assert.match(orderingModel, /href: "\/offres"/);
assert.match(orderingModel, /directCta/);
assert.doesNotMatch(projection, /Configurer le service|Configurer une offre/);
assert.doesNotMatch(projection, /(?:290|490|790|990|1490|1990|2290|2990|4990|6990)\s*(?:[,;\)])/);
assert.doesNotMatch(projection, /BillingV2PricingEngine|Calculate\(/);

assert.match(tariffsPage, /buildPublicCommercialCatalog\(billingCatalogResult\.data\)/);
assert.match(tariffsPage, /beforeSections/);
assert.match(tariffsPage, /compactHero/);
assert.match(tariffsPage, /showHeroActions=\{false\}/);
assert.doesNotMatch(tariffsPage, /flatMap\(\(service\)/);
assert.doesNotMatch(tariffsPage, /formatCents\(/);
assert.match(tariffsComponent, /aria-pressed/);
assert.match(tariffsComponent, /Catalogue tarifaire/);
assert.match(tariffsComponent, /Voir les options et paliers/);
assert.match(tariffsComponent, /frais de mise en service/);
assert.match(tariffsComponent, /catalog\.taxNotice/);
assert.match(tariffsComponent, /taxNotice/);
assert.doesNotMatch(tariffsComponent, /Hors taxes applicables|Montants affichés hors taxes applicables/);
assert.match(tariffsComponent, /Sur devis/);

console.log("Contrat PublicCommercialCatalog et /tarifs vérifié.");
