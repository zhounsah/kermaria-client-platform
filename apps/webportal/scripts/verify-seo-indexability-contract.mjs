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
import { resolveCanonicalPublicUrl } from "../lib/public-route-config.ts";

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
  "/diagnostic",
  "/a-propos",
  "/contact",
  "/mentions-legales",
  "/politique-confidentialite",
  "/cgv",
  "/robots.txt",
  "/sitemap.xml",
];

/**
 * Routes retirees de l'index par les metadonnees `robots` de leur page, et
 * non par un en-tete ni par `robots.txt`.
 *
 * La distinction est le coeur du correctif : une URL en `Disallow` n'est
 * jamais exploree, donc la directive de la page n'est jamais lue. Les deux
 * mecanismes se contredisent, et c'est le blocage qui l'emporte.
 */
const METADATA_NOINDEX_PAGES = [
  ["/solutions", "app/solutions/page.tsx"],
  ["/signup", "app/signup/page.tsx"],
  ["/configurer", "app/configurer/page.tsx"],
];

/**
 * Pages publiques a canonical statique, et fichier qui doit la declarer.
 * `/offres/[slug]` est traitee a part : sa canonical est dynamique.
 */
const CANONICAL_PAGES = [
  ["/", "app/page.tsx"],
  ["/offres", "app/offres/page.tsx"],
  ["/diagnostic", "app/diagnostic/page.tsx"],
  ["/solutions", "app/solutions/page.tsx"],
  ["/configurer", "app/configurer/page.tsx"],
  ["/a-propos", "app/a-propos/page.tsx"],
  ["/contact", "app/contact/page.tsx"],
  ["/mentions-legales", "app/mentions-legales/page.tsx"],
  ["/politique-confidentialite", "app/politique-confidentialite/page.tsx"],
  ["/cgv", "app/cgv/page.tsx"],
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

// 7. `robots.txt` ne publie que `Sitemap`. La directive `Host` n'est pas
//    standard : elle est ignoree par les robots et remontee en erreur par
//    les validateurs.
assert.doesNotMatch(
  robotsSource,
  /^\s*host\s*:/mi,
  "robots.txt ne doit plus emettre la directive non standard `Host`.",
);
assert.match(
  robotsSource,
  /sitemap:\s*resolveSitemapUrl\(baseUrl\)/,
  "robots.txt doit publier l'URL du sitemap sur l'hote canonique.",
);

// 8. La redirection canonique 301 des alias publics (apex sans `www`)
//    conserve chemin et query string.
for (const [host, pathname, search, expected] of [
  ["zacharyhounsa.ovh", "/", "", "https://www.zacharyhounsa.ovh/"],
  [
    "zacharyhounsa.ovh",
    "/offres/dossier-securise",
    "?utm_source=google&utm_medium=cpc",
    "https://www.zacharyhounsa.ovh/offres/dossier-securise"
      + "?utm_source=google&utm_medium=cpc",
  ],
  [
    "zacharyhounsa.ovh:8443",
    "/sitemap.xml",
    "",
    "https://www.zacharyhounsa.ovh/sitemap.xml",
  ],
  [
    "ZACHARYHOUNSA.OVH",
    "/robots.txt",
    "",
    "https://www.zacharyhounsa.ovh/robots.txt",
  ],
  ["home.bzh", "/cgv", "?print=1", "https://www.home.bzh/cgv?print=1"],
]) {
  assert.equal(
    resolveCanonicalPublicUrl(host, pathname, search),
    expected,
    `${host}${pathname}${search} doit etre redirige en 301.`,
  );
}

// 9. Aucun 301 sur l'hote canonique lui-meme (boucle de redirection), sur
//    les portails, en local, sur un hote inconnu ou hostile.
for (const host of [
  "www.zacharyhounsa.ovh",
  "www.home.bzh",
  "portail.home.bzh",
  "dashboard.zacharyhounsa.ovh",
  "administration.zacharyhounsa.ovh",
  "localhost:3000",
  "127.0.0.1:3000",
  "[::1]:3000",
  "unknown.example",
  "zacharyhounsa.ovh.evil.example",
  "zacharyhounsa.ovh/evil.example",
  "user@zacharyhounsa.ovh",
  "",
  null,
  undefined,
]) {
  assert.equal(
    resolveCanonicalPublicUrl(host, "/", ""),
    null,
    `${String(host)} ne doit declencher aucune redirection.`,
  );
}

// 10. La redirection ne detourne ni les validations ACME ni un chemin
//     ouvrant une redirection vers un domaine tiers.
for (const pathname of [
  "/.well-known/acme-challenge/jeton",
  "//evil.example",
  "/\\evil.example",
  "/%2fevil.example",
  "/%5cevil.example",
  "/%0alogin",
  "login",
]) {
  assert.equal(
    resolveCanonicalPublicUrl("zacharyhounsa.ovh", pathname, ""),
    null,
    `${pathname} ne doit pas etre redirige.`,
  );
}

// 11. Le 301 canonique est pose par le proxy, avant tout rendu, et laisse
//     `robots.txt` / `sitemap.xml` dans son matcher.
const proxySource = await read("proxy.ts");
const redirectIndex = proxySource.indexOf("NextResponse.redirect(canonicalUrl");
const passthroughIndex = proxySource.indexOf("NextResponse.next(");
assert.match(
  proxySource,
  /NextResponse\.redirect\(canonicalUrl,\s*301\)/,
  "Le proxy doit rediriger les alias publics en 301 permanent.",
);
assert.match(proxySource, /resolveCanonicalPublicUrl\(/);
assert.match(proxySource, /request\.nextUrl\.search/);
assert.notEqual(passthroughIndex, -1, "Le passe-plat du proxy a disparu.");
assert.ok(
  redirectIndex !== -1 && redirectIndex < passthroughIndex,
  "Le 301 canonique doit precedeer le rendu de la page.",
);
const proxyMatcher = proxySource.match(/matcher:\s*\["([^"]+)"\]/);
assert.ok(proxyMatcher, "Matcher du proxy introuvable.");
for (const pathname of ["/", "/robots.txt", "/sitemap.xml", "/offres"]) {
  assert.match(
    pathname,
    new RegExp(`^${proxyMatcher[1]}$`),
    `${pathname} doit rester couvert par le matcher du proxy.`,
  );
}

// 12. `sitemap.xml` ne fabrique plus de `lastmod` a l'heure de la requete :
//     soit une date reelle de contenu administrable, soit rien.
assert.doesNotMatch(
  sitemapSource,
  /const now = new Date\(\)|lastModified:\s*now/,
  "Le sitemap ne doit pas horodater les pages a l'heure de la requete.",
);
assert.match(
  sitemapSource,
  /result\.data\?\.updatedAt/,
  "Le sitemap doit lire `updatedAt` du contenu administrable.",
);
assert.match(
  sitemapSource,
  /\.\.\.\(lastModified \? \{ lastModified \} : \{\}\)/,
  "`lastmod` doit etre omis quand aucune date fiable n'existe.",
);
assert.match(sitemapSource, /Number\.isNaN\(lastModified\.getTime\(\)\)/);

// 13. `robots.txt` et `sitemap.xml` restent publics : aucune session
//     requise, aucun `noindex` pose sur ces deux routes.
for (const [label, source] of [
  ["app/robots.ts", robotsSource],
  ["app/sitemap.ts", sitemapSource],
]) {
  assert.doesNotMatch(
    source,
    /requireClientSession|requireAdminSession|requireSession|getSessionCookieName/,
    `${label} doit rester accessible sans authentification.`,
  );
  assert.doesNotMatch(
    source,
    /["'`]noindex|X-Robots-Tag/i,
    `${label} ne doit poser aucun noindex.`,
  );
}

// 14. Chaque page publique declare sa propre canonical.
for (const [pathname, file] of CANONICAL_PAGES) {
  const source = await read(file);
  assert.match(
    source,
    new RegExp(
      `alternates:\\s*\\{\\s*canonical:\\s*"${pathname.replace("/", "\\/")}"`,
    ),
    `${file} doit declarer \`alternates: { canonical: "${pathname}" }\`.`,
  );
}

// 15. La fiche de pack canonicalise depuis `pack.slug`, pas depuis le `slug`
//     de l'URL : la canonical reste unique si un alias est un jour accepte.
const packSheetSource = await read("app/offres/[slug]/page.tsx");
assert.match(
  packSheetSource,
  /alternates:\s*\{\s*canonical:\s*`\/offres\/\$\{pack\.slug\}`\s*\}/,
  "La fiche de pack doit canonicaliser depuis `pack.slug`.",
);

// 16. Aucune canonical dans le layout racine. Les metadonnees Next.js sont
//     heritees : une canonical posee la servirait de repli, et toute page
//     qui oublierait la sienne heriterait silencieusement de `/`.
const layoutSource = await read("app/layout.tsx");
assert.doesNotMatch(
  layoutSource,
  /alternates\s*:/,
  "Le layout racine ne doit declarer aucune canonical de repli.",
);

// 17. Les routes retirees de l'index le sont par leurs metadonnees, sans
//     `Disallow` qui empecherait la directive d'etre lue.
for (const [pathname, file] of METADATA_NOINDEX_PAGES) {
  const source = await read(file);
  assert.match(
    source,
    /robots:\s*\{\s*index:\s*false,\s*follow:\s*true\s*\}/,
    `${file} doit poser \`robots: { index: false, follow: true }\`.`,
  );

  assert.ok(
    !disallowed.some(
      (prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`),
    ),
    `${pathname} est en Disallow : sa directive de page ne sera jamais lue.`,
  );

  assert.ok(
    !sitemapPaths.includes(pathname),
    `${pathname} est retiree de l'index mais publiee dans le sitemap.`,
  );
}

// 18. Le sitemap normalise ses URL : l'accueil sortait sans slash final la
//     ou sa canonical en porte un, soit deux chaines pour une meme page.
assert.match(
  sitemapSource,
  /url:\s*new URL\(path, baseUrl\)\.toString\(\)/,
  "Le sitemap doit batir ses URL avec `new URL(path, baseUrl)`.",
);
assert.doesNotMatch(
  sitemapSource,
  /path === "\/" \? "" : path/,
  "L'accueil ne doit plus etre concatene sans slash final.",
);

// 19. Une image Open Graph par defaut existe a la racine, et la carte
//     Twitter l'exploite : `summary` sans image n'a aucun interet.
const ogImageSource = await read("app/opengraph-image.tsx");
for (const field of ["alt", "size", "contentType"]) {
  assert.match(
    ogImageSource,
    new RegExp(`export const ${field}\\b`),
    `app/opengraph-image.tsx doit exporter \`${field}\`.`,
  );
}
assert.match(
  layoutSource,
  /twitter:\s*\{\s*card:\s*"summary_large_image"\s*\}/,
  "Le layout racine doit declarer une carte Twitter avec image.",
);

// 20. Le markdown administrable ne peut plus emettre de `h1` concurrent de
//     celui de la page. Seul `h1` est rabaisse : un corps editorial qui
//     commence proprement en `##` doit conserver une hierarchie h2/h3.
const managedMarkdownSource = await read("components/ManagedMarkdown.tsx");
assert.match(
  managedMarkdownSource,
  /h1:\s*\(.*?<h2\b/s,
  "ManagedMarkdown doit rendre `h1` en `h2`.",
);
assert.match(
  managedMarkdownSource,
  /h2:\s*\(.*?<h2\b/s,
  "ManagedMarkdown doit conserver `h2` en `h2`.",
);
assert.match(
  managedMarkdownSource,
  /h3:\s*\(.*?<h3\b/s,
  "ManagedMarkdown doit conserver `h3` en `h3`.",
);
assert.doesNotMatch(
  managedMarkdownSource,
  /const seenHeadings|new Map<string,\s*number>\(\)/,
  "ManagedMarkdown ne doit pas dedupliquer les headings avec un compteur mutable pendant le rendu.",
);

console.log("Vérification du contrat d'indexabilité SEO WEBPORTAL réussie.");
