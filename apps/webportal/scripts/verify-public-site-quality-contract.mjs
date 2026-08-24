import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const [
  homePage,
  publicShell,
  offresPage,
  packSheetPage,
  diagnosticWizard,
  demoPage,
  demoClientSpace,
  ressourcesPage,
  contactPage,
  contactForm,
  notFoundPage,
  layout,
  appShell,
  publicMetadata,
  robots,
  sitemap,
  styles,
] = await Promise.all([
  read("app/page.tsx"),
  read("components/PublicShell.tsx"),
  read("app/offres/page.tsx"),
  read("app/offres/[slug]/page.tsx"),
  read("components/PublicDiagnosticWizard.tsx"),
  read("app/decouvrir-espace-client/[[...section]]/page.tsx"),
  read("components/DemoClientSpace.tsx"),
  read("app/ressources/page.tsx"),
  read("app/contact/page.tsx"),
  read("components/ContactForm.tsx"),
  read("app/not-found.tsx"),
  read("app/layout.tsx"),
  read("components/AppShell.tsx"),
  read("lib/public-metadata.ts"),
  read("app/robots.ts"),
  read("app/sitemap.ts"),
  read("app/globals.css"),
]);

for (const [label, source] of [
  ["accueil", homePage],
  ["offres", offresPage],
  ["ressources", ressourcesPage],
  ["contact", contactPage],
]) {
  assert.match(source, /buildPublicMetadata\(/, `${label} doit utiliser le helper SEO public.`);
  assert.match(source, /<h1\b/, `${label} doit exposer un H1 explicite.`);
}

assert.match(publicMetadata, /alternates:\s*\{\s*canonical:\s*path\s*\}/);
assert.match(publicMetadata, /openGraph:\s*\{/);
assert.match(publicMetadata, /twitter:\s*\{[\s\S]*summary_large_image/);
assert.doesNotMatch(layout, /headers\(|getCurrentPortalSession\(/);
assert.match(appShell, /requestBffJson<AuthMeResponse>/);
assert.match(appShell, /\/api\/auth\/me/);
assert.match(appShell, /displayVersion \?\? appPackage\.version/);

assert.match(homePage, /JsonLd data=\{localBusinessJsonLd/);
assert.match(homePage, /JsonLd data=\{webSiteJsonLd/);
assert.match(homePage, /href="\/offres"/);
assert.match(homePage, /href="\/contact"/);

assert.match(publicShell, /skip-link/);
assert.match(publicShell, /aria-controls="public-header-nav"/);
assert.match(publicShell, /aria-expanded=\{menuOpen\}/);
assert.match(publicShell, /aria-label="Navigation principale"/);
assert.match(publicShell, /aria-label="Liens l/);
assert.match(publicShell, /publicHref\("\/offres"\)/);
assert.match(publicShell, /publicHref\("\/diagnostic"\)/);
assert.match(publicShell, /publicHref\("\/ressources"\)/);
assert.match(publicShell, /publicHref\("\/services"\)/);

assert.match(offresPage, /PublicPackOverviewGrid/);
assert.match(offresPage, /PublicPackComparisonTable/);
assert.match(offresPage, /\/decouvrir-espace-client/);
assert.doesNotMatch(offresPage, /priceAmountCents|setupFeeAmountCents/);
assert.match(packSheetPage, /Détails opérationnels/);
assert.doesNotMatch(packSheetPage, /Contenu éditable|back-office|administrable en Markdown/);

assert.match(diagnosticWizard, /<option value="256">/);
assert.match(diagnosticWizard, /<option value="above_public_max">Plus de 256 Go<\/option>/);
assert.match(diagnosticWizard, /storage_requires_quote/);
assert.match(diagnosticWizard, /aria-live="polite"/);

assert.match(demoPage, /buildPublicMetadata\(/);
assert.match(demoClientSpace, /Mode DEMO/);
assert.match(demoClientSpace, /donnees fictives|donnÃ©es fictives|données fictives/);
assert.match(demoClientSpace, /lecture seule/);
assert.doesNotMatch(demoClientSpace, /requestBffJson|fetch\(/);
assert.match(demoClientSpace, /role="dialog"/);
assert.match(demoClientSpace, /aria-modal="true"/);
assert.match(demoClientSpace, /closeButtonRef/);
assert.match(demoClientSpace, /Escape/);

assert.match(ressourcesPage, /contentType === "seo_page"/);
assert.match(ressourcesPage, /!entry\.noIndex/);
assert.match(ressourcesPage, /ressources-list-title/);

assert.match(contactPage, /ContactForm/);
assert.match(contactForm, /noValidate/);
assert.match(contactForm, /aria-describedby=/);
assert.match(contactForm, /formuleCode/);
assert.doesNotMatch(contactForm, /localStorage|sessionStorage|SERVICE_AUTH_TOKEN|INTERNAL_API_URL/);

for (const [path, key] of [
  ["app/a-propos/page.tsx", "page:a-propos"],
  ["app/mentions-legales/page.tsx", "legal:mentions-legales"],
  ["app/politique-confidentialite/page.tsx", "legal:politique-confidentialite"],
  ["app/cgv/page.tsx", "legal:cgv"],
]) {
  const source = await read(path);
  assert.match(source, /buildPublicMetadata\(/, `${path} doit declarer ses metadata.`);
  assert.match(source, new RegExp(key.replaceAll(":", "\\:")), `${path} doit charger le contenu administrable attendu.`);
  assert.match(source, /PublicManagedContentArticle/, `${path} doit conserver le rendu legal/managed commun.`);
  assert.match(source, /export const dynamic = "force-dynamic"/, `${path} doit etre rendu en production, pas au build local.`);
}

for (const path of [
  "app/offres/page.tsx",
  "app/diagnostic/page.tsx",
  "app/services/page.tsx",
  "app/solutions/page.tsx",
]) {
  const source = await read(path);
  assert.match(source, /export const dynamic = "force-dynamic"/, `${path} doit interroger les donnees publiques a l'execution.`);
  assert.doesNotMatch(source, /export const revalidate = 300/, `${path} ne doit pas embarquer un fallback vide au build.`);
}

assert.match(notFoundPage, /robots:\s*\{\s*index:\s*false,\s*follow:\s*false\s*\}/);
assert.match(notFoundPage, /<h1 id="not-found-title">/);
assert.match(notFoundPage, /href="\/offres"/);
assert.match(notFoundPage, /href="\/contact"/);

assert.match(robots, /disallow:\s*\[/);
assert.match(robots, /sitemap:/);
assert.match(sitemap, /path:\s*"\/offres"/);
assert.match(sitemap, /path:\s*"\/ressources"/);
assert.match(sitemap, /path:\s*"\/services"/);

assert.match(styles, /@media \(max-width: 720px\)[\s\S]*\.public-header-nav/);
assert.match(styles, /\.public-header-inner\s*\{[\s\S]*grid-template-columns:\s*minmax\(230px,\s*1fr\) auto minmax\(230px,\s*1fr\)/);
assert.match(styles, /\.brand-public\s*\{[\s\S]*grid-column:\s*1/);
assert.match(styles, /\.public-header-nav\s*\{[\s\S]*display:\s*contents/);
assert.match(styles, /\.public-header-links\s*\{[\s\S]*grid-column:\s*2/);
assert.match(styles, /\.public-header-links\s*\{[\s\S]*justify-content:\s*center/);
assert.match(styles, /\.public-header-actions\s*\{[\s\S]*justify-self:\s*end/);
assert.match(styles, /\.public-header-nav a\s*\{[\s\S]*white-space:\s*nowrap/);
assert.match(styles, /\.public-main\s*\{[\s\S]*width:\s*min\(1440px,\s*calc\(100% - 24px\)\)/);
assert.match(styles, /@media \(max-width: 900px\)[\s\S]*\.public-footer-inner/);
assert.match(styles, /@media \(max-width: 700px\)[\s\S]*\.demo-client-card-grid/);
assert.match(styles, /prefers-reduced-motion/);

console.log("Verification qualite site public WEBPORTAL reussie.");
