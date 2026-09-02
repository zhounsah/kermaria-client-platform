import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

import {
  resolveStorefrontBreadcrumb,
  STOREFRONT_SERVICE_SLUGS,
 } from "../lib/storefront-content.ts";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const routes = new Map([
  ["/services", "Services"],
  ["/services/cloud-hebergement", "Cloud & Hébergement"],
  ["/services/domaines-messagerie", "Domaines & Messagerie"],
  ["/services/reseau-securite", "Réseau & Sécurité"],
  ["/services/support-it", "Support & IT"],
  ["/services/vps", "VPS"],
  ["/services/infogerance-vps", "Infogérance VPS"],
  ["/services/hebergement-web", "Hébergement web"],
  ["/services/maintenance-linux", "Maintenance Linux"],
  ["/services/maintenance-wordpress", "Maintenance WordPress"],
  ["/services/sauvegarde-externalisee", "Sauvegarde externalisée"],
  ["/services/supervision-informatique", "Supervision informatique"],
  ["/services/supervision-nas", "Supervision NAS"],
  ["/services/vpn-entreprise", "VPN entreprise"],
  ["/services/bureau-windows-distance", "Bureau Windows à distance"],
  ["/services/unifi", "UniFi"],
  ["/services/firewall", "Firewall"],
  ["/services/cloudflare-waf", "Cloudflare WAF"],
  ["/services/gestion-dns-domaines", "Gestion DNS & domaines"],
  ["/services/messagerie-professionnelle", "Messagerie professionnelle"],
  ["/tarifs", "Tarifs"],
]);

assert.equal(routes.size, 21);
assert.equal(STOREFRONT_SERVICE_SLUGS.length, 15);

for (const [pathname, currentName] of routes) {
  const breadcrumb = resolveStorefrontBreadcrumb(pathname);
  assert.ok(breadcrumb, `${pathname} doit résoudre un fil d’Ariane.`);
  assert.equal(breadcrumb.at(-1)?.name, currentName);
  assert.equal(breadcrumb.at(-1)?.path, pathname);

  if (pathname.startsWith("/services/")) {
    assert.deepEqual(breadcrumb[0], { name: "Services", path: "/services" });
    assert.equal(breadcrumb.length, 2);
  } else {
    assert.equal(breadcrumb.length, 1);
  }
}

assert.equal(resolveStorefrontBreadcrumb("/services/inconnu"), null);
assert.equal(resolveStorefrontBreadcrumb("/diagnostic"), null);

const component = await read("components/PublicServiceComponents.tsx");
const renderer = await read("components/PublicStorefrontPage.tsx");
const servicesPage = await read("app/services/page.tsx");
const serviceRoute = await read("app/services/[category]/page.tsx");
const tarifsPage = await read("app/tarifs/page.tsx");

assert.match(component, /<nav aria-label="Fil d’Ariane" className="service-breadcrumb">/);
assert.ok(component.includes('<span aria-current="page">{item.name}</span>'));
assert.ok(component.includes('<Link href={item.path}>{item.name}</Link>'));
assert.match(renderer, /<ServiceBreadcrumb items=\{breadcrumbItems\} \/>/);
assert.ok(renderer.includes('<JsonLd data={breadcrumbJsonLd(PUBLIC_SITE_URL, [...breadcrumbItems])} />'));
assert.match(servicesPage, /resolveStorefrontBreadcrumb\("\/services"\)/);
assert.match(tarifsPage, /resolveStorefrontBreadcrumb\("\/tarifs"\)/);
assert.match(serviceRoute, /resolveStorefrontBreadcrumb\(`\/services\/\${slug}`\)/);
assert.match(
  serviceRoute,
  /path: `\/services\/\${slug}`/,
  "La canonical dynamique des pages Storefront doit rester alignée sur la route.",
);

// --- Balisage FAQPage -------------------------------------------------
//
// Les cinq rendus publics affichent des questions frequentes administrables.
// Le balisage doit decrire ces memes questions, et rien d'autre : une FAQ
// balisee mais absente de la page est un motif d'action manuelle Google.

// `lib/seo.tsx` porte du JSX : il n'est pas importable directement par node.
// Le depot le verifie deja par lecture de source (`exportedFunctionBody` dans
// `verify-brand-identity-contract.mjs`) ; on garde la meme convention.
function exportedFunctionBody(source, name) {
  const start = source.indexOf(`export function ${name}(`);
  assert.notEqual(start, -1, `Fonction \`${name}\` introuvable.`);
  const next = source.indexOf("\nexport function ", start + 1);
  return source.slice(start, next === -1 ? source.length : next);
}

const seo = await read("lib/seo.tsx");
const faqBody = exportedFunctionBody(seo, "faqPageJsonLd");

assert.match(faqBody, /"@type":\s*"FAQPage"/);
assert.match(faqBody, /"@type":\s*"Question"/);
assert.match(
  faqBody,
  /acceptedAnswer:\s*\{\s*\n?\s*"@type":\s*"Answer"/,
  "Chaque question doit porter une `acceptedAnswer` typee.",
);
assert.match(
  faqBody,
  /inLanguage:\s*"fr-FR"/,
  "La langue doit rester declaree pour les moteurs de reponse.",
);
assert.match(
  faqBody,
  /@id.*#faq/,
  "Le noeud FAQ doit etre identifie par page, sinon deux pages partagent un `@id`.",
);
// Le contenu est administrable : une entree incomplete ne doit pas produire de
// noeud vide, et une FAQ vide ne doit produire aucun balisage.
assert.match(
  faqBody,
  /\.filter\(\s*\(item\)\s*=>\s*item\.question\.length > 0 && item\.answer\.length > 0,?\s*\)/,
  "Les entrees incompletes doivent etre ecartees avant balisage.",
);
assert.match(
  faqBody,
  /if \(entries\.length === 0\) \{\s*\n\s*return null;/,
  "Une FAQ vide doit produire `null`, pas un `FAQPage` sans `mainEntity`.",
);
// Aucun prix ni disponibilite dans la FAQ : l'autorite commerciale reste le
// catalogue, et un balisage qui derive du prix affiche devient faux en silence.
assert.doesNotMatch(faqBody, /"@type":\s*"Offer"|\bprice\b/);

const jsonLdBody = exportedFunctionBody(seo, "JsonLd");
assert.match(
  jsonLdBody,
  /data === null \|\| data === undefined/,
  "`JsonLd` doit ignorer un balisage conditionnel absent plutot qu'emettre `null`.",
);

const faqRenderers = [
  "components/PublicStorefrontPage.tsx",
  "components/PublicMessagingCategoryPage.tsx",
  "components/PublicPriorityServicePage.tsx",
  "components/PublicServicesLandingPage.tsx",
  "components/PublicVpsServicePage.tsx",
];

for (const path of faqRenderers) {
  const source = await read(path);
  assert.match(
    source,
    /content\.faq\.map\(/,
    `${path} doit afficher la FAQ pour pouvoir la baliser.`,
  );
  assert.match(
    source,
    /faqPageJsonLd\(\s*PUBLIC_SITE_URL,[\s\S]{0,200}?content\.faq,/,
    `${path} doit baliser exactement la FAQ qu'il affiche.`,
  );
}

console.log("Contrat breadcrumb Storefront v1.4.0.4 vérifié.");
