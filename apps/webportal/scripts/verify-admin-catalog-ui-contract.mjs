import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

import {
  basisPointsToPercent,
  centsToEuros,
  eurosToCents,
  percentToBasisPoints,
} from "../lib/admin-catalog-units.ts";
import {
  classifyAdminPrice,
  currentPriceForSelection,
  startingMonthlyPriceCents,
} from "../lib/admin-catalog-presenters.ts";

for (const [percent, basisPoints] of [
  ["0", 0], ["10", 1000], ["12,5", 1250], ["20", 2000],
  ["33,33", 3333], ["100", 10000],
]) {
  assert.equal(percentToBasisPoints(percent), basisPoints, percent);
  assert.equal(percentToBasisPoints(percent.replace(",", ".")), basisPoints, `${percent} point`);
  assert.equal(percentToBasisPoints(basisPointsToPercent(basisPoints)), basisPoints, `${percent} round trip`);
}
assert.equal(percentToBasisPoints("100,01"), null);
assert.equal(percentToBasisPoints("12,345"), null);
assert.equal(percentToBasisPoints("-1"), null);
assert.equal(eurosToCents("7,00"), 700);
assert.equal(eurosToCents("12.50"), 1250);
assert.equal(eurosToCents("1,999"), null);
assert.equal(centsToEuros(1250), "12,50");

const asOf = "2026-08-25T12:00:00.000Z";
const price = (overrides = {}) => ({
  id: crypto.randomUUID(), serviceId: "service", tierId: null,
  priceCode: "TEST", priceVersion: 1, amountCents: 1000, currency: "EUR",
  billingCadence: "monthly", chargeTrigger: "initial_subscription",
  taxRateBasisPoints: 2000, validFrom: "2026-01-01T00:00:00.000Z",
  validUntil: null, status: "active", createdByReference: null,
  supersedesPriceId: null, createdAt: "2026-01-01T00:00:00.000Z",
  providerMappings: [], ...overrides,
});
assert.equal(classifyAdminPrice(price(), asOf), "current");
assert.equal(classifyAdminPrice(price({ validFrom: "2026-09-01T00:00:00.000Z" }), asOf), "scheduled");
assert.equal(classifyAdminPrice(price({ validUntil: "2026-08-01T00:00:00.000Z" }), asOf), "historical");
assert.equal(classifyAdminPrice(price({ status: "inactive" }), asOf), "historical");

const service = {
  id: "service", code: "SERVICE", name: "Service", description: null,
  category: null, billingType: "recurring", defaultScopeType: "subscription",
  pricingModel: "tiered", mandatoryForSubscription: false,
  discountEligible: true, publicVisible: true, selfServiceOrderable: true,
  status: "active", displayOrder: 0, updatedByReference: null,
  flatPrices: [price({ amountCents: 900, validUntil: "2026-08-01T00:00:00.000Z" })],
  tiers: [{ id: "tier", serviceId: "service", code: "TIER", name: "Tier",
    publicLabel: null, description: null, numericValue: null, unit: null,
    publicSelectable: true, status: "active", displayOrder: 0, attributes: [],
    prices: [price({ amountCents: 700 }), price({ amountCents: 500, validFrom: "2026-09-01T00:00:00.000Z" })] }],
};
assert.equal(startingMonthlyPriceCents(service, asOf), 700, "prix a partir de courant uniquement");
const serviceWithInactiveCheaperTier = {
  ...service,
  tiers: [
    ...service.tiers,
    { ...service.tiers[0], id: "inactive-tier", code: "INACTIVE", status: "inactive", prices: [price({ amountCents: 100 })] },
  ],
};
assert.equal(startingMonthlyPriceCents(serviceWithInactiveCheaperTier, asOf), 700, "inactive tier excluded from starting price");
const selectionPrices = [
  price({ id: "monthly-initial", taxRateBasisPoints: 2000 }),
  price({ id: "monthly-change", chargeTrigger: "subscription_change", taxRateBasisPoints: 1000 }),
  price({ id: "one-time-initial", billingCadence: "one_time", taxRateBasisPoints: 0 }),
];
assert.equal(currentPriceForSelection(selectionPrices, asOf, "monthly", "subscription_change")?.taxRateBasisPoints, 1000);
assert.equal(currentPriceForSelection(selectionPrices, asOf, "one_time", "initial_subscription")?.taxRateBasisPoints, 0);

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}
const commands = await read("lib/billing-v2-catalog-commands.ts");
assert.match(commands, /case "service\.create"/);
assert.match(commands, /case "tier\.create"/);
assert.doesNotMatch(commands, /price\.update/);
const serviceCreateCommand = commands.slice(commands.indexOf("function buildServiceCreate"), commands.indexOf("function buildServiceUpdate"));
const tierCreateCommand = commands.slice(commands.indexOf("function buildTierCreate"), commands.indexOf("function buildPricePublish"));
assert.match(serviceCreateCommand, /path: "\/services"/);
assert.doesNotMatch(serviceCreateCommand, /publicVisible|selfServiceOrderable|status:/, "la création d’un service ne peut pas le publier");
assert.match(tierCreateCommand, /path: "\/services\/" \+ serviceId \+ "\/tiers"/);
assert.doesNotMatch(tierCreateCommand, /publicSelectable|status:/, "la création d’un palier ne peut pas le publier");

const apiProgram = await read("../../apps/api-internal/Program.cs");
const administrationService = await read("../../apps/api-internal/Services/BillingV2CatalogAdministrationService.cs");
assert.match(apiProgram, /"\/internal\/admin\/billing-v2\/catalog\/services"[\s\S]{0,900}CreateServiceAsync/);
assert.match(apiProgram, /"\/internal\/admin\/billing-v2\/catalog\/services\/\{id\}\/tiers"[\s\S]{0,900}CreateTierAsync/);
assert.match(administrationService, /CreateServiceAsync[\s\S]{0,2600}@public_visible", 0\)[\s\S]{0,300}@status", "inactive"\)/);
assert.match(administrationService, /CreateTierAsync[\s\S]{0,2800}@public_selectable", 0\)[\s\S]{0,300}@status", "inactive"\)/);

for (const page of [
  "app/admin/catalog/services/[id]/page.tsx",
  "app/admin/catalog/services/new/page.tsx",
  "app/admin/catalog/formules/[id]/page.tsx",
  "app/admin/catalog/formules/new/page.tsx",
  "app/admin/catalog/engagements/[id]/page.tsx",
  "app/admin/catalog/engagements/new/page.tsx",
  "app/admin/catalog/integrations/page.tsx",
]) {
  assert.match(await read(page), /await requireAdminSession\(\)/, page);
}
const serviceEditor = await read("components/admin/catalog/ServiceCatalogEditor.tsx");
for (const tab of ["essential", "tiers", "pricing", "commercialization"]) {
  assert.match(serviceEditor, new RegExp(`tab=${tab}`), tab);
}
const catalogUi = await read("components/admin/catalog/AdminCatalogUi.tsx");
assert.match(catalogUi, /beforeunload/);
assert.match(catalogUi, /document\.addEventListener\("click"/);
assert.match(catalogUi, /window\.confirm\(UNSAVED_CHANGES_MESSAGE\)/);
assert.match(serviceEditor, /<ImmutableCode value=\{service\.code\}/);
assert.match(serviceEditor, /Paramètres techniques/);
const tiersEditor = await read("components/admin/catalog/ServiceTiersPanel.tsx");
assert.match(tiersEditor, /selected\.attributes/);
assert.match(tiersEditor, /valueNumeric: undefined, valueText: undefined/);
assert.match(tiersEditor, /confirmDiscardDraft/);
const formulaEditor = await read("components/admin/catalog/FormulaCatalogEditor.tsx");
assert.ok(formulaEditor.includes("useUnsavedChangesGuard(itemDirty)"));
assert.ok(formulaEditor.includes("confirmDiscardItem"));
assert.ok(formulaEditor.includes("button button-danger button-small"));
assert.match(formulaEditor, /baselineMonthlyAmountCents/);
assert.doesNotMatch(formulaEditor, /reduce\([^)]*amount|amountCents\s*\+/);
const pricingEditor = await read("components/admin/catalog/ServicePricingPanel.tsx");
assert.ok(pricingEditor.includes("useUnsavedChangesGuard(revisionDirty)"));
assert.ok(pricingEditor.includes("currentPriceForSelection"));
assert.ok(!pricingEditor.includes("current[0]?.taxRateBasisPoints"));
const integrations = await read("components/admin/catalog/CatalogIntegrations.tsx");
assert.match(integrations, /price_data/);
assert.match(integrations, /name === "paypal"/);
assert.ok(integrations.includes("useUnsavedChangesGuard(mappingDirty)"));
assert.ok(integrations.includes("confirmDiscardMapping"));

console.log("Contrat UI du catalogue admin verifie : conversions, fenetres, routes, tabs et autorite serveur.");
