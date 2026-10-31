import assert from "node:assert/strict";
import { ASSISTANCE_SERVICE_CODE, assistanceCartHref, assistanceContactHref, assistanceDisplayPrice, assistanceOffersFor, findAssistanceOffer, orderAssistanceTiers, resolveAssistanceContactContext } from "../lib/assistance-offers.ts";

const cases = [
  ["ASSIST-PERS-ESSENTIEL", "particuliers", "essentiel", "Essentiel"],
  ["ASSIST-PERS-SERENITE", "particuliers", "serenite", "Sérénité"],
  ["ASSIST-PERS-PREMIUM", "particuliers", "premium", "Premium"],
  ["ASSIST-PRO-START", "professionnels", "pro-start", "Pro Start"],
  ["ASSIST-PRO-BUSINESS", "professionnels", "pro-business", "Pro Business"],
  ["ASSIST-PRO-PREMIUM", "professionnels", "pro-premium", "Pro Premium"],
];
const tiers = cases.map(([code, , , label], index) => ({ code, label, publicSelectable: true, monthlyAmountCents: 1000 + index * 100, monthlyFrenchNetAmountCents: 800 + index * 100 }));
const service = { code: ASSISTANCE_SERVICE_CODE, name: "Assistance et maintenance", publicVisible: true, tiers };
const catalog = { source: "database", currency: "EUR", presets: [], commitments: [], services: [service] };
const original = structuredClone(catalog);

for (const [code, audience, anchor, name] of cases) {
  const offer = findAssistanceOffer(`  ${code.toLowerCase()}  `);
  const context = resolveAssistanceContactContext(code, catalog);
  assert.equal(context?.code, code);
  assert.equal(context.name, name);
  assert.equal(context.href, `/assistance-maintenance/${audience}#${anchor}`);
  assert.ok(context.message.includes(name));
  assert.ok(context.message.includes(audience === "professionnels" ? "Nombre de postes concernés" : "Nombre d’ordinateurs concernés"));
  assert.equal(new URL(assistanceContactHref(offer), "https://example.test").searchParams.get("assistance"), code);
  const cartUrl = new URL(assistanceCartHref(offer), "https://example.test");
  assert.equal(cartUrl.pathname, "/tarifs");
  assert.equal(cartUrl.searchParams.get("assistance"), code);
  assert.equal(cartUrl.hash, "#ajouter-assistance-maintenance");
}
for (const value of [undefined, null, true, 123, {}, [], "", "inconnu", "ASSIST-PRO-START#other"]) {
  assert.equal(findAssistanceOffer(value), null);
  assert.equal(resolveAssistanceContactContext(value, catalog), null);
}
const code = "ASSIST-PRO-BUSINESS";
for (const candidate of [null, { ...catalog, source: "unavailable" }, { ...catalog, services: [] },
  { ...catalog, services: [{ ...service, publicVisible: false }] },
  { ...catalog, services: [{ ...service, tiers: tiers.filter((tier) => tier.code !== code) }] },
  { ...catalog, services: [{ ...service, tiers: tiers.map((tier) => tier.code === code ? { ...tier, publicSelectable: false } : tier) }] },
]) assert.equal(resolveAssistanceContactContext(code, candidate), null);

const custom = { ...catalog, services: [{ ...service, tiers: tiers.map((tier) => tier.code === code ? { ...tier, label: "Suivi des ateliers" } : tier) }] };
assert.equal(resolveAssistanceContactContext(code, custom).name, "Suivi des ateliers");
assert.deepEqual(catalog, original);

const personal = findAssistanceOffer(cases[0][0]);
const professional = findAssistanceOffer(code);
const priced = { recurringPrice: { amountCents: 12345, frenchNetAmountCents: 10287, currency: "EUR" } };
assert.deepEqual(assistanceDisplayPrice(personal, priced), { amountCents: 12345, currency: "EUR" });
assert.deepEqual(assistanceDisplayPrice(professional, priced), { amountCents: 10287, currency: "EUR" });
assert.equal(assistanceDisplayPrice(professional, { recurringPrice: { amountCents: 12345, currency: "EUR" } }), null);
for (const invalid of [undefined, null, 0, -1, 1.5, NaN, Infinity, "12345"]) {
  assert.equal(assistanceDisplayPrice(personal, { recurringPrice: { amountCents: invalid, currency: "EUR" } }), null);
}
assert.equal(assistanceDisplayPrice(personal, { recurringPrice: null }), null);
const projected = { id: ASSISTANCE_SERVICE_CODE, tiers: tiers.map((tier) => ({ id: tier.code, label: tier.label, selectable: tier.publicSelectable, recurringPrice: { amountCents: tier.monthlyAmountCents, frenchNetAmountCents: tier.monthlyFrenchNetAmountCents, currency: "EUR" } })) };
const interleaved = [0, 3, 1, 4, 2, 5].map((index) => projected.tiers[index]);
assert.deepEqual(orderAssistanceTiers(interleaved).map((tier) => tier.id), cases.map(([code]) => code));
assert.deepEqual(interleaved.map((tier) => tier.id), [0, 3, 1, 4, 2, 5].map((index) => cases[index][0]), "Le tri ne modifie pas la source.");
for (const audience of ["particuliers", "professionnels"]) {
  const available = assistanceOffersFor([projected], audience);
  assert.equal(available.length, 3);
  assert.deepEqual(available.map(({ offer, tier }) => assistanceDisplayPrice(offer, tier).amountCents),
    audience === "particuliers" ? [1000, 1100, 1200] : [1100, 1200, 1300]);
}
assert.equal(assistanceOffersFor([{ ...projected, tiers: projected.tiers.map((tier, index) => ({ ...tier, selectable: index !== 0 })) }], "particuliers").length, 2);
assert.equal(assistanceOffersFor([], "particuliers").length, 0);
console.log("Contexte contact assistance : un service, six paliers, visibilité et prix vérifiés.");
