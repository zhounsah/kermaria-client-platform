import assert from "node:assert/strict";
import fs from "node:fs";

const storefrontContent = fs.readFileSync(new URL("../lib/storefront-content.ts", import.meta.url), "utf8");
const route = fs.readFileSync(new URL("../app/services/[category]/page.tsx", import.meta.url), "utf8");
const component = fs.readFileSync(new URL("../components/PublicPriorityServicePage.tsx", import.meta.url), "utf8");
const messagingComponent = fs.readFileSync(new URL("../components/PublicMessagingCategoryPage.tsx", import.meta.url), "utf8");
const genericComponent = fs.readFileSync(new URL("../components/PublicStorefrontPage.tsx", import.meta.url), "utf8");
const css = fs.readFileSync(new URL("../app/globals.css", import.meta.url), "utf8");

const prioritySlugs = [
  "messagerie-professionnelle",
  "vpn-entreprise",
  "sauvegarde-externalisee",
  "unifi",
  "infogerance-vps",
  "hebergement-web",
];

const priorityBlock = storefrontContent.match(
  /export const STOREFRONT_PRIORITY_SERVICE_SLUGS = \[([\s\S]*?)\] as const/,
)?.[1] ?? "";

assert.ok(priorityBlock, "La liste des services prioritaires doit exister.");
for (const slug of prioritySlugs) {
  assert.match(priorityBlock, new RegExp(`"${slug}"`));
}
assert.equal(
  (priorityBlock.match(/^\s*"[a-z0-9-]+",?\s*$/gm) ?? []).length,
  6,
  "Le lot C doit rester limite a exactement six pages service.",
);

assert.match(route, /PublicPriorityServicePage/);
assert.match(route, /isStorefrontPriorityServiceSlug\(serviceSlug\)/);
assert.match(route, /serviceSlug=\{serviceSlug\}/);
assert.match(route, /PublicMessagingCategoryPage/);
assert.match(route, /slug === "domaines-messagerie"/);
assert.match(route, /<PublicStorefrontPage/);
assert.match(
  route,
  /selfServiceOrderable=\{selfServiceOrderable \?\? false\}/,
  "Le renderer prioritaire doit rester fail-closed si Billing est indisponible.",
);

assert.match(component, /breadcrumbJsonLd/);
assert.match(component, /resolveStorefrontPublicCta/);
assert.match(component, /resolveStorefrontPublicRelatedLinks/);
assert.match(component, /FORMULA: "Formule disponible"/);
assert.match(component, /HYBRID: "Formule \+ accompagnement"/);
assert.match(component, /QUOTE: "Sur devis"/);
assert.match(component, /VpnComparisonDetails/);
assert.match(component, /storefront-inline-disclosure/);
assert.match(component, /\/vpn-ou-bureau-a-distance-que-choisir/);
assert.match(component, /storefront-priority-commercial-copy/);
assert.doesNotMatch(component, /projection autoritative/i);
assert.doesNotMatch(component, /projection Billing/i);
assert.doesNotMatch(component, /\/formules\//, "Le renderer ne doit pas coder un preset Billing en dur.");
assert.doesNotMatch(component, /\bEUR\b/, "Le renderer ne doit pas contenir de devise codee en dur.");

assert.match(messagingComponent, /messaging-category-page/);
assert.match(messagingComponent, /messaging-problem-grid/);
assert.match(messagingComponent, /\/services\/messagerie-professionnelle/);
assert.match(messagingComponent, /\/services\/gestion-dns-domaines/);
assert.match(messagingComponent, /\/pourquoi-emails-professionnels-arrivent-spam/);
assert.doesNotMatch(messagingComponent, /DOMAIN-MANAGED/);
assert.doesNotMatch(messagingComponent, /projection autoritative/i);
assert.doesNotMatch(messagingComponent, /\/diagnostic/);

assert.match(genericComponent, /export function PublicStorefrontPage/);
assert.match(css, /\.public-main:has\(\.services-page\)\s*\{\s*padding-top: 16px;/);
assert.match(css, /\.storefront-priority-hero h1,[\s\S]*font-size: clamp\(2\.15rem, 3\.7vw, 3\.25rem\)/);
assert.match(css, /\.messaging-problem-grid\s*\{\s*grid-template-columns: repeat\(2, minmax\(0, 1fr\)\)/);
assert.match(css, /@media \(max-width: 820px\)[\s\S]*\.messaging-problem-grid,[\s\S]*grid-template-columns: 1fr/);

console.log("Verification du contrat Storefront lot C reussie.");
