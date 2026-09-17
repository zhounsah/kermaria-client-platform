import assert from "node:assert/strict";
import { readdir, readFile } from "node:fs/promises";
import { extname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const projectRoot = fileURLToPath(new URL("../../..", import.meta.url));
const sourceRoots = [
  "apps/webportal/app",
  "apps/webportal/components",
  "apps/webportal/lib",
  "apps/api-internal/Services",
  "apps/api-internal/Contracts",
  "packages/shared/src",
];
const sourceExtensions = new Set([".ts", ".tsx", ".cs"]);
const intentionalCompatibilityLiterals = [
  "lorsquÔÇÖelles sÔÇÖappliquent",
];

// Sequences de mojibake relevees dans les surfaces executees. `Âge` reste
// volontairement autorise : l'accent circonflexe sur le A est un francais
// correct, contrairement a `Â«` ou a `Ã©`.
const mojibakePattern = /(?:Ã.|Â[«»\s]|â€™|â€œ|â€|â€“|â€”|â€¦|ÔÇ|\uFFFD)/u;

async function collectRuntimeSources(root) {
  const directory = join(projectRoot, root);
  const entries = await readdir(directory, { recursive: true });
  return entries
    .map((entry) => join(directory, entry))
    .filter((path) => sourceExtensions.has(extname(path)));
}

const sourcePaths = (await Promise.all(sourceRoots.map(collectRuntimeSources))).flat();
assert.ok(sourcePaths.length > 250, "Le balayage des surfaces runtime est anormalement étroit.");

for (const path of sourcePaths) {
  const rawSource = await readFile(path, "utf8");
  // Ces clés ne sont présentes que dans les deux normaliseurs de compatibilité
  // CMS/seed. Elles sont remplacées avant tout rendu ; toute autre séquence
  // dégradée dans les sources runtime reste bloquante.
  const source = intentionalCompatibilityLiterals.reduce(
    (normalized, literal) => normalized.replaceAll(literal, ""),
    rawSource,
  );
  assert.doesNotMatch(
    source,
    mojibakePattern,
    `Mojibake détecté dans ${relative(projectRoot, path)}.`,
  );
}

const readWebportal = (path) => readFile(new URL(`../${path}`, import.meta.url), "utf8");
const readApi = (path) => readFile(new URL(`../../api-internal/${path}`, import.meta.url), "utf8");

const offersPage = await readWebportal("app/offres/page.tsx");
assert.match(offersPage, /Quatre offres conçues/);
assert.match(offersPage, /Ces offres sont pensées/);
assert.doesNotMatch(offersPage, /Quatre offres conçus|Ces offres sont pensés/);

const storefrontContent = await readWebportal("lib/storefront-content.ts");
for (const category of [
  "Hébergement & services en ligne",
  "Domaines & messagerie",
  "Réseau & sécurité",
  "Assistance & maintenance",
]) {
  assert.match(storefrontContent, new RegExp(category));
}
const defaultCategoryLinks = storefrontContent.slice(
  storefrontContent.indexOf("export const DEFAULT_STOREFRONT_SERVICES_CATEGORY_LINKS"),
  storefrontContent.indexOf("export const DEFAULT_STOREFRONT_SERVICES_LEAD"),
);
assert.doesNotMatch(defaultCategoryLinks, /Cloud & Hébergement|Support & IT/);
assert.match(storefrontContent, /function normalizeLegacyPublicText/);
assert.match(storefrontContent, /lorsquÔÇÖelles sÔÇÖappliquent[\s\S]*lorsqu’elles s’appliquent/);
assert.match(storefrontContent, /GENERIC_AUDIT_LABEL_PATTERN[\s\S]*"Faire le diagnostic"/);

const storefrontSeed = await readApi("Services/StorefrontContentSeed.cs");
assert.match(storefrontSeed, /NormalizePublicCopy/);
assert.match(storefrontSeed, /lorsquÔÇÖelles sÔÇÖappliquent[\s\S]*lorsqu’elles s’appliquent/);
assert.match(storefrontSeed, /NormalizeCtaLabel[\s\S]*href == "\/diagnostic" \? "Faire le diagnostic"/);

console.log("Contrat de qualité textuelle runtime vérifié.");
