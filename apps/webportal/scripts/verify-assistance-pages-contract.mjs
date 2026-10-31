import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const read = (path) => readFile(new URL(`../${path}`, import.meta.url), "utf8");
const [pages, offers, css, home, shell, tariffs, category, sitemap, routes, projection, cart, contact] = await Promise.all([
  read("components/AssistancePages.tsx"),
  read("lib/assistance-offers.ts"),
  read("components/AssistancePages.module.css"),
  read("app/page.tsx"),
  read("components/PublicShell.tsx"),
  read("app/tarifs/page.tsx"),
  read("app/services/[category]/page.tsx"),
  read("app/sitemap.ts"),
  read("lib/public-route-config.ts"),
  read("lib/public-commercial-catalog.ts"),
  read("components/BillingV2DirectCartAdd.tsx"),
  read("app/contact/page.tsx"),
]);

for (const code of [
  "ASSIST-PERS-ESSENTIEL", "ASSIST-PERS-SERENITE", "ASSIST-PERS-PREMIUM",
  "ASSIST-PRO-START", "ASSIST-PRO-BUSINESS", "ASSIST-PRO-PREMIUM",
]) assert.ok(offers.includes(code), `Formule absente: ${code}`);
assert.ok(offers.includes('"ASSISTANCE-MAINTENANCE"'));
for (const path of [
  "/assistance-maintenance", "/assistance-maintenance/particuliers",
  "/assistance-maintenance/professionnels",
]) assert.ok(sitemap.includes(`path: "${path}"`), `Sitemap: ${path}`);
assert.match(pages, /getBillingV2FormulesCatalog\(\)/);
assert.match(pages, /assistanceDisplayPrice/);
assert.match(offers, /item\.id === offer\.code && item\.selectable/);
assert.ok(pages.indexOf('className={styles.whySubscribe}') < pages.indexOf('className={styles.choiceSection}'));
assert.match(pages, /service\.directlyOrderable/);
assert.match(pages, /Tarif indisponible/);
assert.match(pages, /<details className=\{styles\.benefits\}>/);
assert.doesNotMatch(pages + offers, /(?:990|1990|3990|2280|4680|8280)\b/);
assert.match(pages, /assistanceContactHref\(offer\)/);
assert.match(contact, /resolveAssistanceContactContext/);
assert.match(contact, /key=\{formKey\}/);
assert.match(home, /<AssistanceHomeLink \/>/);
for (const source of [shell, tariffs, category, routes]) assert.ok(source.includes("/assistance-maintenance"));
assert.match(css, /var\(--site-ink/);
assert.match(css, /var\(--site-blue/);
assert.match(css, /var\(--site-cream/);
assert.match(css, /prefers-reduced-motion: reduce/);
assert.doesNotMatch(css, /#28645e|#eff5f4|linear-gradient\(130deg/);
assert.match(projection, /"Assistance": "Assistance et maintenance"/);
assert.match(projection, /directMinimumQuantity: service\.directMinimumQuantity/);
assert.match(cart, /const quantity = isAssistancePro \? Number\(quantityInput\) : 1/);
assert.match(cart, /includes\(tierCode\)/);
assert.match(cart, /service\.id === ASSISTANCE_SERVICE_CODE \? orderAssistanceTiers\(tiers\) : tiers/);
assert.match(await read("components/PublicCommercialTariffCatalog.tsx"), /assistance \? orderAssistanceTiers\(service\.tiers\.filter/);

console.log("Contrat Assistance sur la refonte PR #1 vérifié.");
