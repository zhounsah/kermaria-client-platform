/**
 * Contrat d'identite de marque du site public.
 *
 * Objet : garantir que `Zachary IT` est compris comme le NOM DU SITE et le
 * nom commercial, que `Zachary HOUNSA-HOUNKPA EI` reste expose comme
 * identite juridique, et que la marque n'apparait jamais deux fois dans un
 * meme titre.
 *
 * Les valeurs elles-memes sont verifiees en important reellement
 * `lib/brand-identity.ts` (module sans dependance, donc chargeable tel quel
 * par Node). Le reste est verifie sur la source, comme les autres contrats
 * du depot : les modules concernes importent via l'alias `@/`, que Node ne
 * resout pas hors du build Next.
 */
import assert from "node:assert/strict";
import { readdir, readFile } from "node:fs/promises";

import { BRAND_NAME, LEGAL_NAME } from "../lib/brand-identity.ts";
import { STOREFRONT_SERVICE_SLUGS } from "../lib/storefront-content.ts";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

/**
 * Concatene les litteraux `"..."` d'un bloc. Les descriptions longues sont
 * ecrites en plusieurs morceaux concatenes par `+` pour tenir la largeur de
 * ligne : les comparer morceau par morceau ne prouverait rien.
 */
function joinStringLiterals(block) {
  return [...block.matchAll(/"((?:[^"\\]|\\.)*)"/g)]
    .map((match) => match[1])
    .join("");
}

/** Corps d'une fonction exportee, de sa signature a la suivante. */
function exportedFunctionBody(source, name) {
  const start = source.indexOf(`export function ${name}(`);
  assert.notEqual(start, -1, `Fonction \`${name}\` introuvable.`);
  const next = source.indexOf("\nexport function ", start + 1);
  return source.slice(start, next === -1 ? source.length : next);
}

function occurrences(haystack, needle) {
  return haystack.split(needle).length - 1;
}

const [
  brandIdentity,
  publicMetadata,
  layout,
  homePage,
  seo,
  publicShell,
  editorialSlugPage,
  sitemap,
  wikiArticlePage,
] = await Promise.all([
  read("lib/brand-identity.ts"),
  read("lib/public-metadata.ts"),
  read("app/layout.tsx"),
  read("app/page.tsx"),
  read("lib/seo.tsx"),
  read("components/PublicShell.tsx"),
  read("app/[slug]/page.tsx"),
  read("app/sitemap.ts"),
  read("app/wiki/article/[slug]/page.tsx"),
]);

// 1. Les deux identites, et leur repartition des roles.
assert.equal(BRAND_NAME, "Zachary IT", "Le nom commercial a change.");
assert.equal(
  LEGAL_NAME,
  "Zachary HOUNSA-HOUNKPA EI",
  "La denomination juridique a change.",
);
assert.doesNotMatch(
  brandIdentity,
  /^\s*import\s/m,
  "lib/brand-identity.ts doit rester sans import : ce contrat le charge "
    + "directement, sans la resolution d'alias `@/` de Next.",
);

// 2. Le nom du site et le suffixe des titres derivent de cette source unique.
assert.match(
  publicMetadata,
  /import \{ BRAND_NAME \} from "@\/lib\/brand-identity"/,
  "lib/public-metadata.ts doit lire le nom commercial depuis brand-identity.",
);
for (const name of ["PUBLIC_BRAND_NAME", "PUBLIC_SITE_NAME"]) {
  assert.match(
    publicMetadata,
    new RegExp(`export const ${name} = BRAND_NAME;`),
    `${name} doit valoir BRAND_NAME, pas une chaine recopiee.`,
  );
}
assert.match(
  publicMetadata,
  /siteName:\s*PUBLIC_SITE_NAME/,
  "`og:site_name` doit valoir le nom commercial sur chaque page publique.",
);
assert.match(
  layout,
  /siteName:\s*PUBLIC_SITE_NAME/,
  "Le layout racine doit declarer `og:site_name` = nom commercial.",
);
assert.match(
  layout,
  /template:\s*`%s \| \$\{PUBLIC_BRAND_NAME\}`/,
  "Le gabarit de titre doit suffixer le nom commercial une seule fois.",
);

// 3. Titre de l'accueil : marque en tete, une seule fois, avec la localite.
const homeMetadataBlock = homePage.slice(
  homePage.indexOf("buildPublicMetadata({"),
  homePage.indexOf("};", homePage.indexOf("buildPublicMetadata({")),
);
const homeTitle = joinStringLiterals(
  homeMetadataBlock.slice(
    homeMetadataBlock.indexOf("title:"),
    homeMetadataBlock.indexOf("description:"),
  ),
);
const homeDescription = joinStringLiterals(
  homeMetadataBlock.slice(
    homeMetadataBlock.indexOf("description:"),
    homeMetadataBlock.indexOf("path:"),
  ),
);

assert.ok(
  homeTitle.startsWith(BRAND_NAME),
  `Le titre de l'accueil doit s'ouvrir sur "${BRAND_NAME}" (lu : "${homeTitle}").`,
);
assert.equal(
  occurrences(homeTitle, BRAND_NAME),
  1,
  `La marque est dupliquee dans le titre de l'accueil : "${homeTitle}".`,
);
assert.ok(
  homeTitle.includes("Guichen"),
  "Le titre de l'accueil doit associer l'activite a Guichen.",
);
assert.ok(
  homeTitle.length <= 70,
  `Titre d'accueil trop long pour un resultat de recherche (${homeTitle.length} caracteres).`,
);
assert.equal(
  occurrences(homeDescription, BRAND_NAME),
  1,
  "La marque doit apparaitre une seule fois dans la meta description.",
);
assert.ok(
  homeDescription.includes("Guichen"),
  "La meta description de l'accueil doit citer Guichen.",
);
assert.match(
  homeMetadataBlock,
  /path:\s*"\/"/,
  "La canonical de l'accueil doit rester `/`.",
);

// 4. Garde-fou anti-doublon generalise : aucune page ne doit ecrire
//    elle-meme le suffixe de marque, que le gabarit du layout ajoute deja
//    pour tous les segments enfants.
const appFiles = (await readdir(new URL("../app", import.meta.url), {
  recursive: true,
}))
  .filter((entry) => /(^|[\\/])page\.tsx$/.test(entry))
  .map((entry) => `app/${entry.split("\\").join("/")}`);
assert.ok(appFiles.length > 10, "Aucune page lue sous app/.");

for (const file of appFiles) {
  const source = await read(file);
  for (const [, literal] of source.matchAll(/title:\s*"((?:[^"\\]|\\.)*)"/g)) {
    assert.ok(
      !literal.trimEnd().endsWith(`| ${BRAND_NAME}`),
      `${file} suffixe deja la marque dans un \`title\` : le gabarit du `
        + `layout racine l'ajoutera une seconde fois ("${literal}").`,
    );
    // L'accueil est la seule page dont le titre commence par la marque : il
    // s'agit d'un choix editorial assume, documente dans `app/page.tsx`.
    if (file === "app/page.tsx") continue;
    assert.ok(
      !literal.includes(BRAND_NAME),
      `${file} recopie la marque au milieu d'un \`title\` : le gabarit `
        + `l'ajoutera quand meme, et la deduplication de `
        + `\`parseStorefrontPageContent\` ne retire qu'un suffixe ("${literal}").`,
    );
  }
}

// 4bis. Meme garde-fou sur les titres administrables du seed CMS.
//
// `parseStorefrontPageContent` retire un « | Zachary IT » FINAL pour que le
// gabarit du layout l'ajoute une fois. Une marque placee au milieu du titre
// echappe a cette deduplication : le titre servi la porte alors deux fois, et
// depasse la longueur utile en resultat de recherche.
const storefrontSeed = await read("../api-internal/Services/StorefrontContentSeed.cs");
for (const [, seoTitle] of storefrontSeed.matchAll(/Seo\(\s*"((?:[^"\\]|\\.)*)"/g)) {
  const withoutSuffix = seoTitle.replace(/\s*\|\s*Zachary IT$/i, "");
  assert.ok(
    !withoutSuffix.includes(BRAND_NAME),
    "Un titre CMS ne doit porter la marque qu'en suffixe, seule position que "
      + `la deduplication sait retirer ("${seoTitle}").`,
  );
  assert.ok(
    withoutSuffix.length + ` | ${BRAND_NAME}`.length <= 75,
    `Titre CMS trop long une fois la marque ajoutee (${withoutSuffix.length + 13} `
      + `caracteres) : "${seoTitle}".`,
  );
}

// 5. Balisage `WebSite` : nom du site = marque, identite juridique en
//    `alternateName`, rattachement a l'entreprise via `publisher`.
const webSiteBody = exportedFunctionBody(seo, "webSiteJsonLd");
assert.match(webSiteBody, /"@type":\s*"WebSite"/);
assert.match(
  webSiteBody,
  /"@id":\s*`\$\{base\}\/#website`/,
  "Le noeud WebSite doit rester identifie par `#website`.",
);
assert.match(
  webSiteBody,
  /\burl:\s*`\$\{base\}\/`/,
  "Le noeud WebSite doit declarer l'URL du site.",
);
assert.match(
  webSiteBody,
  /\bname:\s*BRAND_NAME\b/,
  "`WebSite.name` doit valoir le nom commercial.",
);
assert.match(
  webSiteBody,
  /alternateName:\s*\[\s*LEGAL_NAME\s*\]/,
  "`WebSite.alternateName` doit conserver la denomination juridique.",
);
assert.match(
  webSiteBody,
  /publisher:\s*\{\s*"@id":\s*`\$\{base\}\/#business`\s*\}/,
  "Le WebSite doit designer l'entreprise comme editeur.",
);
assert.doesNotMatch(
  webSiteBody,
  /\bname:\s*LEGAL_NAME\b/,
  "`WebSite.name` ne doit plus porter la denomination juridique.",
);

// 6. Balisage de l'entreprise : marque en `name`, raison sociale en
//    `legalName`, ancrage local sur Guichen.
const businessBody = exportedFunctionBody(seo, "localBusinessJsonLd");
assert.match(businessBody, /"@type":\s*"LocalBusiness"/);
assert.match(
  businessBody,
  /"@id":\s*`\$\{base\}\/#business`/,
  "Le noeud entreprise doit rester identifie par `#business`.",
);
assert.match(
  businessBody,
  /\bname:\s*BRAND_NAME\b/,
  "`LocalBusiness.name` doit valoir le nom commercial.",
);
assert.match(
  businessBody,
  /\blegalName:\s*LEGAL_NAME\b/,
  "`LocalBusiness.legalName` doit porter la denomination juridique.",
);
assert.doesNotMatch(
  businessBody,
  /alternateName:\s*BRAND_NAME/,
  "La marque ne doit plus etre releguee en `alternateName`.",
);
assert.match(
  businessBody,
  /addressLocality:\s*"Guichen"/,
  "L'adresse de l'entreprise doit rester a Guichen.",
);
assert.match(
  businessBody,
  /\{\s*"@type":\s*"City",\s*name:\s*"Guichen"\s*\}/,
  "Guichen doit rester declaree en zone desservie.",
);

// La description de l'entreprise est la reponse lisible par machine a « que
// fait Zachary IT ? ». Elle doit couvrir les univers reellement publies, pas
// la seule sauvegarde.
const businessDescription = seo.match(
  /const BUSINESS_DESCRIPTION =\s*([\s\S]*?);/,
)?.[1] ?? "";
assert.ok(
  businessDescription.length > 0,
  "`BUSINESS_DESCRIPTION` introuvable dans lib/seo.tsx.",
);
for (const universe of ["sauvegarde", "hébergement", "messagerie", "réseau", "support"]) {
  assert.ok(
    businessDescription.toLowerCase().includes(universe),
    `La description de l'entreprise doit citer l'univers « ${universe} » : `
      + "sinon un moteur de reponse ne peut pas savoir qu'il fait partie de l'offre.",
  );
}

// `knowsAbout` est une declaration de competence : chaque sujet doit
// correspondre a une page de service reellement servie. Le nombre d'entrees
// suit donc le nombre de slugs publies.
assert.match(
  businessBody,
  /knowsAbout:\s*BUSINESS_TOPICS/,
  "Le balisage entreprise doit declarer les sujets couverts.",
);
const topicsBlock = seo.match(/const BUSINESS_TOPICS = \[([\s\S]*?)\];/)?.[1] ?? "";
const topics = [...topicsBlock.matchAll(/"([^"]+)"/g)].map((match) => match[1]);
assert.equal(
  topics.length,
  STOREFRONT_SERVICE_SLUGS.length,
  "`knowsAbout` doit couvrir exactement les pages de service publiees "
    + `(${topics.length} sujets pour ${STOREFRONT_SERVICE_SLUGS.length} pages).`,
);
assert.equal(
  new Set(topics).size,
  topics.length,
  "Un sujet repete dans `knowsAbout` est du remplissage, pas une competence.",
);

// 7. Aucun des deux noms n'est recopie en dur dans le balisage : une seule
//    source, sinon les deux divergent silencieusement.
for (const literal of [BRAND_NAME, LEGAL_NAME]) {
  assert.ok(
    !seo.includes(`"${literal}"`),
    `lib/seo.tsx recopie "${literal}" : utiliser lib/brand-identity.ts.`,
  );
}

// 8. L'accueil emet chaque entite une fois et une seule.
for (const builder of ["localBusinessJsonLd", "webSiteJsonLd"]) {
  assert.equal(
    occurrences(homePage, `<JsonLd data={${builder}(`),
    1,
    `${builder} doit etre rendu exactement une fois sur l'accueil.`,
  );
}

// 9. Pages SEO dynamiques : titre, description, canonical et `index, follow`
//    restent ceux de la page. Le branding global ne doit rien y ajouter.
assert.match(
  editorialSlugPage,
  /title:\s*page\.seoTitle \?\? page\.title/,
  "La page editoriale doit garder son propre titre.",
);
assert.match(
  editorialSlugPage,
  /description:\s*page\.seoDescription \?\? page\.summary/,
  "La page editoriale doit garder sa propre description.",
);
assert.match(
  editorialSlugPage,
  /path:\s*page\.canonicalUrl \?\? `\/\$\{page\.slug\}`/,
  "La page editoriale doit garder sa canonical specifique.",
);
assert.match(
  editorialSlugPage,
  /robots:\s*\{\s*index:\s*!page\.noIndex,\s*follow:\s*true\s*\}/,
  "La page editoriale doit rester en `index, follow` sauf noIndex explicite.",
);
assert.match(
  editorialSlugPage,
  /buildPublicMetadata\(/,
  "La page editoriale doit continuer a passer par le helper commun "
    + "(canonical, Open Graph, Twitter Card).",
);
assert.ok(
  !editorialSlugPage.includes(BRAND_NAME),
  "La page editoriale ne doit pas coller la marque a son titre : le gabarit "
    + "du layout racine s'en charge, une fois.",
);

// 10. Wiki : canonical sur le domaine dedie, titre propre a l'article.
assert.match(
  wikiArticlePage,
  /alternates:\s*\{\s*canonical:\s*wikiCanonical\(`\/article\/\$\{result\.data\.slug\}`\)\s*\}/,
  "L'article wiki doit rester canonicalise sur wiki.zacharyhounsa.ovh.",
);
assert.ok(
  !wikiArticlePage.includes(BRAND_NAME),
  "L'article wiki ne doit pas coller la marque a son titre.",
);

// 11. Sitemap : les pages SEO editoriales publiees restent emises sur `www`,
//     les articles wiki sur le domaine wiki.
assert.match(
  sitemap,
  /entry\.contentType !== "wiki_article" && entry\.publicPath/,
  "Le sitemap `www` doit continuer a publier les pages editoriales.",
);
assert.match(
  sitemap,
  /entry\.contentType === "wiki_article" && entry\.publicPath/,
  "Le sitemap du wiki doit continuer a publier les articles.",
);
assert.match(
  sitemap,
  /new URL\(entry\.publicPath!, `https:\/\/\$\{WIKI_PUBLIC_HOST\}`\)/,
  "Les articles wiki doivent rester emis sur le domaine wiki.",
);
assert.match(
  sitemap,
  /path:\s*"\/a-propos"/,
  "La page a-propos doit rester au sitemap : elle porte la relation "
    + "marque / entite juridique.",
);

// 12. Coquille publique : marque dans l'en-tete, les deux noms au pied de page.
const headerBrand = publicShell.slice(
  publicShell.indexOf('className="brand brand-public"'),
  publicShell.indexOf("</a>", publicShell.indexOf('className="brand brand-public"')),
);
assert.ok(
  headerBrand.includes('className="brand-logo brand-logo-public"'),
  "L'en-tete public doit afficher le logo horizontal officiel.",
);
assert.ok(
  !headerBrand.includes(LEGAL_NAME),
  "L'en-tete public ne doit pas revenir a la denomination juridique.",
);

const footerBrand = publicShell.slice(
  publicShell.indexOf('className="public-footer-brand"'),
  publicShell.indexOf("</div>", publicShell.indexOf('className="public-footer-brand"')),
);
assert.ok(
  footerBrand.includes('className="brand-logo brand-logo-footer"'),
  "Le pied de page public doit afficher le logo officiel.",
);
assert.ok(
  footerBrand.includes(LEGAL_NAME),
  "Le pied de page public doit conserver la denomination juridique.",
);

console.log("Vérification du contrat d'identité de marque WEBPORTAL réussie.");
