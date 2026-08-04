/**
 * Contrat d'indexabilite SEO du WEBPORTAL.
 *
 * Garde-fou principal : empecher le retour d'un `X-Robots-Tag: noindex`
 * global (regression V0.23 -> V1.1.10.1, qui rendait la vitrine publique
 * non indexable des que le reverse proxy ne strippait plus l'en-tete).
 *
 * L'en-tete HTTP prime sur `robots.txt` et sur les metadonnees `robots`
 * des pages : c'est donc lui qui est verifie en premier, puis la
 * coherence de `robots.txt` et du sitemap avec cette source de verite.
 */
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createRequire } from "node:module";

import nextConfig, { NOINDEX_ROUTE_PREFIXES } from "../next.config.ts";

// Le meme moteur de correspondance que celui utilise par Next pour
// resoudre les `source` de `headers()` : on teste le comportement reel,
// pas une reimplementation approchee.
const { pathToRegexp } = createRequire(import.meta.url)(
  "next/dist/compiled/path-to-regexp",
);

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

/** Routes publiques qui doivent rester indexables. */
const PUBLIC_PATHS = [
  "/",
  "/offres",
  "/offres/pack-essentiel",
  "/solutions",
  "/a-propos",
  "/contact",
  "/mentions-legales",
  "/politique-confidentialite",
  "/cgv",
  "/signup",
  "/robots.txt",
  "/sitemap.xml",
];

const matcherCache = new Map();

function sourceToRegExp(source) {
  let matcher = matcherCache.get(source);
  if (!matcher) {
    matcher = pathToRegexp(source);
    matcherCache.set(source, matcher);
  }
  return matcher;
}

/** Une source est attrape-tout si elle s'applique a la racine publique. */
function isCatchAllSource(source) {
  return sourceToRegExp(source).test("/");
}

const headerRules = await nextConfig.headers();

function headersFor(pathname) {
  return headerRules
    .filter((rule) => sourceToRegExp(rule.source).test(pathname))
    .flatMap((rule) => rule.headers);
}

function robotsTagFor(pathname) {
  return headersFor(pathname)
    .filter((header) => header.key.toLowerCase() === "x-robots-tag")
    .map((header) => header.value);
}

// 1. Aucune regle attrape-tout ne doit porter `X-Robots-Tag`.
//    C'est le garde-fou anti-noindex global.
for (const rule of headerRules) {
  if (!isCatchAllSource(rule.source)) {
    continue;
  }

  for (const header of rule.headers) {
    assert.notEqual(
      header.key.toLowerCase(),
      "x-robots-tag",
      `X-Robots-Tag applique globalement via "${rule.source}" : `
        + "la vitrine publique deviendrait non indexable.",
    );
  }
}

// 2. Les pages publiques ne portent aucun `X-Robots-Tag`.
for (const pathname of PUBLIC_PATHS) {
  assert.deepEqual(
    robotsTagFor(pathname),
    [],
    `${pathname} doit rester indexable (aucun X-Robots-Tag attendu).`,
  );
}

// 3. Les zones privees portent bien `noindex, nofollow`, racine et sous-arbre.
for (const prefix of NOINDEX_ROUTE_PREFIXES) {
  for (const pathname of [prefix, `${prefix}/quelque-chose`]) {
    assert.deepEqual(
      robotsTagFor(pathname),
      ["noindex, nofollow"],
      `${pathname} doit etre en noindex, nofollow.`,
    );
  }
}

// 4. Les en-tetes de securite restent appliques a toutes les routes.
const rootHeaderKeys = headersFor("/").map((header) => header.key);
for (const key of [
  "X-Content-Type-Options",
  "X-Frame-Options",
  "Content-Security-Policy",
  "Referrer-Policy",
  "Permissions-Policy",
  "Cross-Origin-Opener-Policy",
  "Cross-Origin-Resource-Policy",
]) {
  assert.ok(
    rootHeaderKeys.includes(key),
    `En-tete de securite ${key} perdu sur les routes publiques.`,
  );
}

// 5. `robots.txt` doit refuser exactement les memes prefixes.
const robotsSource = await read("app/robots.ts");
const disallowBlock = robotsSource.match(/disallow:\s*\[([^\]]*)\]/);
assert.ok(disallowBlock, "Liste `disallow` introuvable dans app/robots.ts.");

const disallowed = [...disallowBlock[1].matchAll(/"([^"]+)"/g)].map(
  (match) => match[1],
);
assert.deepEqual(
  [...disallowed].sort(),
  [...NOINDEX_ROUTE_PREFIXES].sort(),
  "robots.txt et NOINDEX_ROUTE_PREFIXES ont divergé.",
);

// 6. Aucune URL du sitemap ne doit pointer vers une route en noindex.
const sitemapSource = await read("app/sitemap.ts");
const sitemapPaths = [...sitemapSource.matchAll(/path:\s*"([^"]+)"/g)].map(
  (match) => match[1],
);
assert.ok(sitemapPaths.length > 0, "Aucune entree lue dans app/sitemap.ts.");

for (const pathname of sitemapPaths) {
  assert.deepEqual(
    robotsTagFor(pathname),
    [],
    `${pathname} est publie dans le sitemap mais servi en noindex.`,
  );
}

console.log("Vérification du contrat d'indexabilité SEO WEBPORTAL réussie.");
