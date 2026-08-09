import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

async function readRoot(path) {
  return readFile(new URL(`../../../${path}`, import.meta.url), "utf8");
}

const sharedTypes = await readRoot("packages/shared/src/index.ts");
const migration = await readRoot(
  "apps/api-internal/Migrations/MariaDb/045_editorial_platform.sql",
);
const contracts = await readRoot("apps/api-internal/Contracts/EditorialContracts.cs");
const service = await readRoot("apps/api-internal/Services/EditorialService.cs");
const program = await readRoot("apps/api-internal/Program.cs");
const markdown = await read("components/ManagedMarkdown.tsx");
const proxy = await read("proxy.ts");
const routeConfig = await read("lib/public-route-config.ts");
const sitemap = await read("app/sitemap.ts");
const robots = await read("app/robots.ts");
const adminNavigation = await read("components/AdminNavigation.tsx");
const adminForm = await read("components/AdminEditorialForm.tsx");
const adminList = await read("app/admin/editorial/[contentType]/page.tsx");
const adminDashboard = await read("app/admin/editorial/page.tsx");
const adminCategoryForm = await read("components/AdminEditorialCategoryForm.tsx");
const wikiHome = await read("app/wiki/page.tsx");
const wikiArticle = await read("app/wiki/article/[slug]/page.tsx");
const seoPage = await read("app/[slug]/page.tsx");
const faqBlock = await read("components/PublicFaqBlock.tsx");
const docs = await readRoot("docs/V1.3_EDITORIAL_PLATFORM.md");

for (const name of [
  "editorial_contents",
  "editorial_categories",
  "editorial_content_revisions",
  "editorial_redirects",
  "editorial_faq_scopes",
  "editorial_faq_scope_links",
  "admin_permission_grants",
]) {
  assert.match(migration, new RegExp(`CREATE TABLE IF NOT EXISTS ${name}`));
}

for (const type of ["wiki_article", "seo_page", "faq"]) {
  assert.match(sharedTypes, new RegExp(`["']${type}["']`));
  assert.match(contracts, new RegExp(type));
}

for (const status of ["draft", "published", "archived", "scheduled"]) {
  assert.match(sharedTypes, new RegExp(`["']${status}["']`));
  assert.match(contracts, new RegExp(status));
}

assert.match(service, /ReservedSeoSlugs/);
for (const reserved of ["admin", "api", "offres", "signup", "diagnostic", "configurer"]) {
  assert.match(service, new RegExp(`"${reserved}"`));
}
assert.match(service, /AddRevisionAsync/);
assert.match(service, /AddRedirectAsync/);
assert.match(program, /content\.wiki\.read/);
assert.match(program, /content\.seo\.write/);
assert.match(program, /content\.faq\.write/);
assert.match(program, /content\.publish/);

for (const endpoint of [
  "/internal/public/editorial/wiki/home",
  "/internal/public/editorial/wiki/articles/{slug}",
  "/internal/public/editorial/seo-pages/{slug}",
  "/internal/public/editorial/faq/{scope}",
  "/internal/public/editorial/sitemap",
  "/internal/admin/editorial",
  "/internal/admin/editorial/{id}/publish",
  "/internal/admin/editorial/revisions/{revisionId}/restore",
]) {
  assert.ok(program.includes(endpoint), `${endpoint} doit être exposé.`);
}

assert.match(markdown, /remarkGfm/);
assert.match(markdown, /rehypeSanitize/);
assert.match(markdown, /skipHtml/);
assert.match(markdown, /safeMarkdownUrl/);
assert.doesNotMatch(markdown, /dangerouslySetInnerHTML|rehypeRaw|rehype-raw/);
assert.match(markdown, /managed-markdown-table-scroll/);

assert.match(routeConfig, /WIKI_PUBLIC_HOST = "wiki\.zacharyhounsa\.ovh"/);
assert.match(routeConfig, /WIKI_INTERNAL_HOST = "wiki\.home\.bzh"/);
assert.match(
  routeConfig,
  /pathname === "\/wiki" \|\| pathname\.startsWith\("\/wiki\/"\)/,
  "Un chemin deja prefixe /wiki sur le hostname Wiki ne doit pas devenir /wiki/wiki.",
);
assert.match(proxy, /resolveWikiRewritePath/);
assert.match(proxy, /x-wiki-host-kind/);
assert.match(robots, /getWikiHostKind/);
assert.match(sitemap, /getPublicEditorialSitemap/);
assert.match(sitemap, /WIKI_PUBLIC_HOST/);

assert.match(wikiHome, /Aucun article publié/);
assert.match(wikiArticle, /wikiCanonical/);
assert.match(wikiArticle, /ManagedMarkdown/);
assert.match(seoPage, /getEditorialRedirect/);
assert.match(seoPage, /getPublicSeoPage/);
assert.match(faqBlock, /<details/);
assert.match(faqBlock, /return null/);

assert.match(adminNavigation, /\/admin\/editorial/);
assert.match(adminForm, /Importer un fichier Markdown/);
assert.match(adminForm, /Exporter en Markdown/);
assert.match(adminForm, /parseMarkdownFile/);
assert.match(adminForm, /"publish"/);
assert.match(adminForm, /"archive"/);
assert.match(adminList, /Question/);
assert.match(adminList, /Catégorie \/ scope/);
assert.match(adminList, /Indexation/);
assert.match(adminList, /Aucun article pour le moment/);
assert.match(adminList, /Importer un Markdown/);
assert.match(adminCategoryForm, /aria-expanded/);
assert.match(adminCategoryForm, /\+ Ajouter une catégorie/);
assert.match(adminCategoryForm, /Catégories existantes/);
assert.match(adminCategoryForm, /category\.slug/);
assert.match(adminDashboard, /dashboardSummary/);
assert.match(adminDashboard, /indexables/);
assert.match(adminDashboard, /scopes/);

assert.match(docs, /Pas de faux articles/);
assert.doesNotMatch(migration, /Restaurer un fichier supprimé|sauvegarde-informatique-guichen/);

console.log("Vérification du contrat editorial V1.3 réussie.");
