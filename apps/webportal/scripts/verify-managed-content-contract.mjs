import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const sharedTypes = await read("../../packages/shared/src/index.ts");
const internalApi = await read("lib/internal-api.ts");
const payloads = await read("lib/bff-payloads.ts");
const publicRouteConfig = await read("lib/public-route-config.ts");
const managedMarkdown = await read("components/ManagedMarkdown.tsx");
const publicPackCard = await read("components/PublicPackCard.tsx");
const comparisonTable = await read("components/PublicPackComparisonTable.tsx");
const adminNavigation = await read("components/AdminNavigation.tsx");
const adminContentPage = await read("app/admin/content/page.tsx");
const adminContentDetailPage = await read("app/admin/content/[key]/page.tsx");
const adminPackCatalogPage = await read("app/admin/public-pack-catalog/page.tsx");
const cgvPage = await read("app/cgv/page.tsx");
const mentionsPage = await read("app/mentions-legales/page.tsx");
const aProposPage = await read("app/a-propos/page.tsx");
const packSheetPage = await read("app/offres/[slug]/page.tsx");
const adminContentRoute = await read("app/api/admin/content/route.ts");
const adminContentDetailRoute = await read("app/api/admin/content/[key]/route.ts");

assert.match(sharedTypes, /type ManagedContentKey =/);
assert.match(sharedTypes, /type ManagedContentType =/);
assert.match(sharedTypes, /interface ManagedContentSummary/);
assert.match(sharedTypes, /interface ManagedContentDetail/);
assert.match(sharedTypes, /interface ManagedContentPayload/);
assert.match(sharedTypes, /buildPackSheetContentKey/);
assert.match(sharedTypes, /getManagedContentRegistry/);

assert.match(internalApi, /getPublicManagedContent/);
assert.match(internalApi, /getAdminManagedContentList/);
assert.match(internalApi, /getAdminManagedContent\(/);
assert.match(internalApi, /\/internal\/portal\/content\//);
assert.match(internalApi, /\/internal\/admin\/content/);

assert.match(payloads, /parseManagedContentPayload/);
assert.match(publicRouteConfig, /PUBLIC_ROUTES/);
assert.match(publicRouteConfig, /PORTFOLIO_URL/);
assert.match(publicRouteConfig, /isPublicRoute/);

assert.match(adminContentRoute, /handleAdminGet<ManagedContentSummary\[]>/);
assert.match(adminContentDetailRoute, /handleAdminGet<ManagedContentDetail>/);
assert.match(adminContentDetailRoute, /handleAdminMutation/);
assert.match(adminContentDetailRoute, /isManagedContentKey/);
assert.match(adminContentDetailRoute, /decodeURIComponent/);

assert.match(adminContentPage, /await requireAdminSession\(\)/);
assert.match(adminContentPage, /target="_blank"/);
assert.match(adminContentDetailPage, /await requireAdminSession\(\)/);
assert.match(adminContentDetailPage, /decodeURIComponent/);
assert.match(adminContentDetailPage, /target="_blank"/);
assert.match(adminNavigation, /\/admin\/content/);
assert.match(
  adminPackCatalogPage,
  /Modifier la fiche technique/,
  "La page admin de vitrine packs doit proposer un lien rapide vers les fiches techniques.",
);

assert.match(cgvPage, /getPublicManagedContent\("legal:cgv"\)/);
assert.match(mentionsPage, /getPublicManagedContent\("legal:mentions-legales"\)/);
assert.match(aProposPage, /getPublicManagedContent\("page:a-propos"\)/);
assert.doesNotMatch(cgvPage, /placeholder/i);
assert.doesNotMatch(mentionsPage, /placeholder/i);
assert.doesNotMatch(aProposPage, /placeholder/i);

assert.match(packSheetPage, /buildPackSheetContentKey/);
assert.match(packSheetPage, /getPublicManagedContent/);
assert.match(packSheetPage, /ManagedMarkdown/);
assert.match(packSheetPage, /Composants techniques liés/);

assert.match(publicPackCard, /Voir la fiche technique/);
assert.match(comparisonTable, /Voir la fiche technique/);

assert.match(managedMarkdown, /ReactMarkdown/);
assert.doesNotMatch(managedMarkdown, /dangerouslySetInnerHTML/);
assert.doesNotMatch(managedMarkdown, /rehypeRaw|rehype-raw/);

console.log("Vérification du contrat managed content V0.33 réussie.");
