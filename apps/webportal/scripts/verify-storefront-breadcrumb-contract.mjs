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

console.log("Contrat breadcrumb Storefront v1.4.0.4 vérifié.");
