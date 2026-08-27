import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

import {
  DEFAULT_STOREFRONT_SERVICES_CATEGORY_LINKS,
  DEFAULT_STOREFRONT_SERVICES_PROBLEM_ENTRIES,
  parseStorefrontServicesLandingContent,
  STOREFRONT_SERVICES_CATEGORY_DESTINATIONS,
  STOREFRONT_SERVICES_PROBLEM_DESTINATIONS,
} from "../lib/storefront-content.ts";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const legacy = {
  seoTitle: "Services IT geres pour petites structures",
  seoDescription: "Une description suffisamment longue pour valider le contrat de la page Services Zachary IT.",
  title: "L'informatique dont votre activite a besoin. Geree pour vous.",
  lead: "Un contenu historique suffisamment long pour rester compatible pendant la transition du lot A.",
  ctaLabel: "Demander un audit",
  ctaHref: "/diagnostic",
  sections: [
    {
      heading: "Des services modulaires",
      bodyMarkdown: "Une brique precise ou un ensemble coherent selon le besoin.",
    },
  ],
  faq: [
    { question: "Puis-je choisir un service ?", answer: "Oui, selon le besoin et ses dependances." },
    { question: "Tout est-il en ligne ?", answer: "Non, certaines prestations restent sur devis." },
  ],
  relatedLinks: [
    { label: "Cloud", href: "/services/cloud-hebergement" },
    { label: "Messagerie", href: "/services/domaines-messagerie" },
    { label: "Reseau", href: "/services/reseau-securite" },
    { label: "Tarifs", href: "/tarifs" },
  ],
};

assert.equal(
  parseStorefrontServicesLandingContent(JSON.stringify(legacy)),
  null,
  "L'ancien JSON ne doit pas satisfaire le contrat strict.",
);

const transitional = parseStorefrontServicesLandingContent(JSON.stringify(legacy), true);
assert.ok(transitional, "Le fallback de transition doit accepter l'ancien contenu.");
assert.deepEqual(
  transitional.problemEntries.map((entry) => entry.href),
  [...STOREFRONT_SERVICES_PROBLEM_DESTINATIONS],
);
assert.deepEqual(
  transitional.relatedLinks.map((entry) => entry.href),
  [...STOREFRONT_SERVICES_CATEGORY_DESTINATIONS],
);
assert.equal(transitional.sections.length, 1);
assert.equal(transitional.sections[0].heading, "Des services modulaires, pas un catalogue figé");
assert.match(transitional.lead, /Partez de votre besoin/);
assert.doesNotMatch(JSON.stringify(transitional.sections), /Ce que Zachary IT prend en charge|Choisir le bon point de départ/);

const modern = {
  ...legacy,
  problemEntries: DEFAULT_STOREFRONT_SERVICES_PROBLEM_ENTRIES,
  relatedLinks: DEFAULT_STOREFRONT_SERVICES_CATEGORY_LINKS,
};
const strict = parseStorefrontServicesLandingContent(JSON.stringify(modern));
assert.ok(strict, "Le nouveau JSON Services doit satisfaire le contrat strict.");
assert.equal(strict.problemEntries.length, 6);
assert.equal(strict.relatedLinks.length, 4);

const directConfigurator = structuredClone(modern);
directConfigurator.problemEntries[0] = {
  ...directConfigurator.problemEntries[0],
  href: "/formules/pack-acces-distance",
};
assert.equal(
  parseStorefrontServicesLandingContent(JSON.stringify(directConfigurator)),
  null,
  "Une carte besoin ne doit jamais pointer directement vers un configurateur Billing.",
);

const duplicateProblem = structuredClone(modern);
duplicateProblem.problemEntries[1] = {
  ...duplicateProblem.problemEntries[1],
  href: duplicateProblem.problemEntries[0].href,
};
assert.equal(
  parseStorefrontServicesLandingContent(JSON.stringify(duplicateProblem)),
  null,
  "Les six destinations probleme doivent rester uniques.",
);

const servicesPage = await read("app/services/page.tsx");
const renderer = await read("components/PublicServicesLandingPage.tsx");
const serviceComponents = await read("components/PublicServiceComponents.tsx");
const adminForm = await read("components/AdminStorefrontContentForm.tsx");
const apiValidator = await read("../api-internal/Services/ManagedContentService.cs");
const seed = await read("../api-internal/Services/StorefrontContentSeed.cs");
const styles = await read("app/globals.css");

assert.match(servicesPage, /portalArea === "public" \|\| portalArea === "local"/);
assert.match(servicesPage, /requireClientSession\(\)/);
assert.match(servicesPage, /<PublicServicesLandingPage/);
assert.match(servicesPage, /parseStorefrontServicesLandingContent\(contentResult\.data\.bodyMarkdown, true\)/);
assert.doesNotMatch(servicesPage, /resolveStorefrontServicesLandingActions|getBillingV2FormulesCatalog|<PublicStorefrontPage/);

assert.match(renderer, /<h1>\{content\.title\}<\/h1>/);
assert.match(renderer, /Quel problème cherchez-vous à résoudre/);
assert.match(renderer, /content\.problemEntries\.map/);
assert.match(renderer, /<ServiceCategoryCard/);
assert.match(renderer, /breadcrumbJsonLd/);
assert.match(renderer, /resolveStorefrontPublicCta\(content, false\)/);
assert.doesNotMatch(renderer, /Comparer les formules|\/formules\//);
assert.match(renderer, /aria-label=\{`Voir le bon point de départ : \$\{entry\.title\}`\}/);
assert.match(serviceComponents, /aria-label=\{`Découvrir \$\{category\.shortTitle\}`\}/);

assert.match(adminForm, /content\.key === "storefront:services"/);
assert.match(adminForm, /parseStorefrontServicesLandingContent\(content\.bodyMarkdown, true\)/);
assert.match(adminForm, /<legend>Problèmes \/ besoins<\/legend>/);
assert.match(adminForm, /STOREFRONT_SERVICES_PROBLEM_DESTINATIONS/);
assert.match(adminForm, /STOREFRONT_SERVICES_CATEGORY_DESTINATIONS/);

assert.match(apiValidator, /ValidateStorefrontJson\(definition\.Key, bodyMarkdown\)/);
assert.match(apiValidator, /definitionKey == "storefront:services"/);
assert.match(apiValidator, /HasServicesLandingProblemEntries/);
assert.match(apiValidator, /HasServicesLandingCategories/);
assert.match(apiValidator, /GetArrayLength\(\) != 6/);
assert.match(apiValidator, /GetArrayLength\(\) != 4/);
for (const href of STOREFRONT_SERVICES_PROBLEM_DESTINATIONS) {
  assert.ok(apiValidator.includes(`"${href}"`), `Destination problème absente du validateur API : ${href}`);
}
for (const href of STOREFRONT_SERVICES_CATEGORY_DESTINATIONS) {
  assert.ok(apiValidator.includes(`"${href}"`), `Destination catégorie absente du validateur API : ${href}`);
}

assert.match(seed, /\["storefront:services"\] = PageOf\(/);
assert.match(seed, /problemEntries:/);
assert.match(seed, /P\("Je dois travailler à distance"/);
assert.match(seed, /L\("Support & IT", "\/services\/support-it"\)/);

assert.match(styles, /\.service-overview-grid\.services-problem-grid/);
assert.match(styles, /@media \(max-width: 820px\)[\s\S]*repeat\(2, minmax\(0, 1fr\)\)/);
assert.match(styles, /@media \(max-width: 560px\)[\s\S]*services-problem-grid[\s\S]*grid-template-columns: 1fr/);

console.log("Contrat landing /services orientee probleme verifie.");
