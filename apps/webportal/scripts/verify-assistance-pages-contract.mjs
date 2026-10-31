import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const read = (path) => readFile(new URL(`../${path}`, import.meta.url), "utf8");
const [pages, hub, audience, sitemap, routes, shell, home, tariffs, servicePage, tariffCatalog, migration, offers, projection, cartAdd] = await Promise.all([
  read("components/AssistancePages.tsx"),
  read("app/assistance-maintenance/page.tsx"),
  read("app/assistance-maintenance/[public]/page.tsx"),
  read("app/sitemap.ts"),
  read("lib/public-route-config.ts"),
  read("components/PublicShell.tsx"),
  read("app/page.tsx"),
  read("app/tarifs/page.tsx"),
  read("app/services/[category]/page.tsx"),
  read("components/PublicCommercialTariffCatalog.tsx"),
  read("../../apps/api-internal/Migrations/MariaDb/103_assistance_catalog.sql"),
  read("app/offres/page.tsx"),
  read("lib/public-commercial-catalog.ts"),
  read("components/BillingV2DirectCartAdd.tsx"),
]);

const prices = new Map([
  ["ASSIST-PERS-ESSENTIEL", 990], ["ASSIST-PERS-SERENITE", 1990],
  ["ASSIST-PERS-PREMIUM", 3990], ["ASSIST-PRO-START", 2280],
  ["ASSIST-PRO-BUSINESS", 4680], ["ASSIST-PRO-PREMIUM", 8280],
]);
for (const [code, amount] of prices) {
  assert.ok(pages.includes(`"${code}"`), `${code} doit figurer sur sa page publique`);
  assert.match(migration, new RegExp(`'${code}'[\\s\\S]{0,100}[' ]${amount}(?: amount_cents)?`), `${code} doit avoir son prix catalogue attendu`);
}
assert.match(pages, /getBillingV2FormulesCatalog\(\)/);
assert.match(pages, /buildPublicCommercialCatalog\(result\.data\)/);
assert.match(pages, /catalog\.services\.find\(\(item\) => item\.id === code\)/);
assert.match(pages, /price\?\.frenchNetAmountCents\s*:\s*price\?\.amountCents/);
assert.match(pages, /Tarif indisponible/);
assert.match(pages, /Informations momentanément indisponibles/);
assert.match(pages, /service\.directlyOrderable/);
assert.match(pages, /\/tarifs#ajouter-/);
assert.match(tariffCatalog, /id=\{`ajouter-\$\{slug\(service\.id\)\}`\}/);
assert.doesNotMatch(pages, /(?:990|1990|3990|2280|4680|8280)\b/, "les montants affichés doivent provenir du catalogue");

assert.match(hub, /buildPublicMetadata/);
assert.match(audience, /buildPublicMetadata/);
assert.match(audience, /notFound\(\)/);
assert.match(audience, /value === "particuliers" \|\| value === "professionnels"/);
for (const path of ["/assistance-maintenance", "/assistance-maintenance/particuliers", "/assistance-maintenance/professionnels"]) {
  assert.ok(sitemap.includes(`path: "${path}"`), `${path} doit être indexable`);
}
assert.ok(routes.includes('"/assistance-maintenance"'));
for (const source of [shell, home, tariffs, servicePage]) assert.ok(source.includes("/assistance-maintenance"), "la navigation doit mener au hub");
assert.doesNotMatch(pages, /<main\b/, "le contenu est déjà dans le main du shell public");
assert.match(pages, /<h1>/);
assert.match(pages, /aria-label="Fil d’Ariane"/);
assert.match(pages, /aria-current="page"/);
assert.match(pages, /aria-labelledby="subscriptions-title"/);
assert.match(offers, /Configurer une offre/, "le parcours des offres historiques reste présent");
assert.match(projection, /directMinimumQuantity: service\.directMinimumQuantity/);
assert.match(projection, /"Assistance": "Assistance et maintenance"/);
assert.match(projection, /directMaximumQuantity: service\.directMaximumQuantity/);
assert.match(cartAdd, /\["ASSIST-PRO-START", "ASSIST-PRO-BUSINESS", "ASSIST-PRO-PREMIUM"\]\.includes\(service\.id\)/);
assert.match(cartAdd, /const quantity = isAssistancePro \? Number\(quantityInput\) : 1/);
assert.match(cartAdd, /Number\.isInteger\(minimumQuantity\)[\s\S]*Number\.isInteger\(maximumQuantity\)/);
assert.match(cartAdd, /minimumQuantity > 0 && maximumQuantity >= minimumQuantity/);
assert.match(cartAdd, /quantity < minimumQuantity \|\| quantity > maximumQuantity/);
assert.match(cartAdd, /min=\{minimumQuantity\} max=\{maximumQuantity\}/);
assert.match(cartAdd, /aria-invalid=\{quantityError\}/);
assert.match(cartAdd, /role="alert"/);
assert.match(cartAdd, /isAssistancePro && !quantityBoundsAvailable/);
assert.match(cartAdd, /serviceCode: service\.id,[\s\S]*?quantity,[\s\S]*?origin: "direct"/);
assert.match(tariffCatalog, /return "\/ poste \/ mois"/);
assert.match(pages, /HT indicatif \/ poste \/ mois/);
assert.match(pages, /15 minutes d’assistance à distance par mois/);
assert.match(pages, /90 minutes d’assistance à distance par poste et par mois/);
assert.match(pages, /Les minutes d’assistance ne se reportent pas/);
assert.match(pages, /<details className=\{styles\.benefits\}>/);
assert.match(migration, /self_service_orderable, public_ordering_mode/);
assert.match(migration, /'quote', 1, 10000, 0, 'active'/);

console.log("Contrat des pages Assistance et maintenance vérifié.");
