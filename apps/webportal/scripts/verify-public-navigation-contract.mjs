import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const [
  publicShell,
  publicServices,
  servicesLanding,
  homePage,
  offresPage,
] = await Promise.all([
  read("components/PublicShell.tsx"),
  read("lib/public-services.ts"),
  read("components/PublicServicesLandingPage.tsx"),
  read("app/page.tsx"),
  read("app/offres/page.tsx"),
]);
const { resolveStorefrontPublicCta } = await import(
  new URL("../lib/storefront-content.ts", import.meta.url),
);

assert.match(publicShell, /label: "Offres"[\s\S]*label: "Tarifs"[\s\S]*label: "Diagnostic"[\s\S]*label: "À propos"/);
assert.match(publicShell, /Services <ChevronDown/);
assert.match(publicShell, /Nous contacter/);
assert.match(publicShell, /href=\{publicHref\("\/contact"\)\}/);
assert.match(publicShell, /href="\/login"/);
assert.doesNotMatch(publicShell, /Demander un audit/);
assert.doesNotMatch(publicShell, /services\/support-it#infogerance|Cloud & Hébergement/);
assert.match(publicShell, /VPN \/ accès sécurisé/);
assert.match(publicShell, /Hébergement web/);
assert.match(publicShell, /onNavigate=\{closeMobileMenu\}/);
assert.match(publicShell, /function closeMobileMenuOnEscape/);
assert.match(publicShell, /menuToggleRef\.current\?\.focus\(\)/);
assert.match(publicShell, /triggerRef\.current\?\.focus\(\)/);
assert.match(publicShell, /hidden=\{!open\}/);

const footerSource = publicShell.slice(publicShell.indexOf("<footer"));
assert.match(footerSource, /className="public-footer-grid"/);
assert.match(footerSource, /id="public-footer-services-title">Services/);
assert.match(footerSource, /id="public-footer-discover-title">Découvrir/);
assert.match(footerSource, /id="public-footer-help-title">Aide & espace client/);
assert.match(footerSource, /Informations légales/);
assert.match(footerSource, /FooterLinkList links=\{footerServiceLinks\}/);
assert.match(footerSource, /FooterLinkList links=\{footerDiscoverLinks\}/);
assert.match(footerSource, /FooterLinkList links=\{footerLegalLinks\}/);
for (const [label, pathname] of [
  ["Diagnostic", "/diagnostic"],
  ["Nous contacter", "/contact"],
  ["Espace client", "/login"],
  ["Mentions légales", "/mentions-legales"],
  ["Politique de confidentialité", "/politique-confidentialite"],
  ["CGV", "/cgv"],
]) {
  assert.match(publicShell, new RegExp(`label: "${label}"|>${label}<|href=\{publicHref\("${pathname.replaceAll("/", "\\/")}"\)\}`));
}
assert.match(publicShell, /new Date\(\)\.getFullYear\(\)/);
assert.doesNotMatch(footerSource, /Version v|APP_VERSION_LABEL|\bpack\b|\bpreset\b|\bformules?\b/i);

assert.match(publicServices, /shortTitle: "Assistance & maintenance"/);
assert.match(publicServices, /shortTitle: "Hébergement & services en ligne"/);
assert.match(publicServices, /href: "\/tarifs", label: "Voir les tarifs"/);
assert.doesNotMatch(publicServices, /Demander un audit/);

assert.match(servicesLanding, /Faire le diagnostic/);
assert.match(servicesLanding, /href="\/diagnostic"/);
assert.match(servicesLanding, /Nous contacter/);
assert.match(homePage, /Découvrir les services/);
assert.match(homePage, /href="\/diagnostic"/);
assert.match(offresPage, /Configurer une offre/);
assert.doesNotMatch(offresPage, /Voir les offres[\s\S]{0,120}href="\/formules"/);

assert.deepEqual(
  resolveStorefrontPublicCta(
    { ctaLabel: "Demander un audit", ctaHref: "/contact" },
    null,
  ),
  { label: "Nous contacter", href: "/contact" },
  "Un audit générique ne doit jamais mener au contact.",
);
assert.deepEqual(
  resolveStorefrontPublicCta(
    { ctaLabel: "Demander un audit", ctaHref: "/diagnostic" },
    null,
  ),
  { label: "Faire le diagnostic", href: "/diagnostic" },
  "Le questionnaire public doit rester nommé Diagnostic.",
);

console.log("Contrat navigation publique et vocabulaire CTA vérifié.");
