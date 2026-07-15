import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const sharedTypes = await read("../../packages/shared/src/index.ts");
const internalApi = await read("lib/internal-api.ts");
const payloads = await read("lib/bff-payloads.ts");
const portalNav = await read("components/PortalNavigation.tsx");
const adminNav = await read("components/AdminNavigation.tsx");
const downloadsPage = await read("app/downloads/page.tsx");
const adminDownloadsPage = await read("app/admin/downloads/page.tsx");
const adminDownloadNewPage = await read("app/admin/downloads/new/page.tsx");
const adminDownloadDetailPage = await read("app/admin/downloads/[id]/page.tsx");
const adminDownloadCategoriesPage = await read(
  "app/admin/downloads/categories/page.tsx",
);
const clientDownloadsRoute = await read("app/api/downloads/route.ts");
const clientDownloadFileRoute = await read("app/api/downloads/[id]/file/route.ts");
const adminDownloadsRoute = await read("app/api/admin/downloads/route.ts");
const adminDownloadDetailRoute = await read(
  "app/api/admin/downloads/[id]/route.ts",
);
const adminDownloadFileRoute = await read(
  "app/api/admin/downloads/[id]/file/route.ts",
);
const adminCategoriesRoute = await read(
  "app/api/admin/download-categories/route.ts",
);
const adminCategoryDetailRoute = await read(
  "app/api/admin/download-categories/[id]/route.ts",
);
const styles = await read("app/globals.css");

assert.match(sharedTypes, /type DownloadResourceType =/);
assert.match(sharedTypes, /type DownloadSourceKind = "internal_file" \| "external_url";/);
assert.match(sharedTypes, /type DownloadVisibilityMode = "all_clients" \| "targeted";/);
assert.match(sharedTypes, /interface DownloadCategory/);
assert.match(sharedTypes, /interface DownloadResource/);
assert.match(sharedTypes, /interface PortalDownloadCategory/);
assert.match(sharedTypes, /DOWNLOAD_RESOURCE_TYPES/);

assert.match(internalApi, /getClientDownloads/);
assert.match(internalApi, /getAdminDownloadCategories/);
assert.match(internalApi, /getAdminDownloads/);
assert.match(internalApi, /getAdminDownload\(/);
assert.match(internalApi, /\/internal\/portal\/downloads/);
assert.match(internalApi, /\/internal\/admin\/download-categories/);
assert.match(internalApi, /\/internal\/admin\/downloads/);

assert.match(payloads, /parseDownloadCategoryPayload/);
assert.match(payloads, /parseDownloadResourcePayload/);
assert.match(payloads, /DOWNLOAD_VISIBILITY_TARGET_TYPES/);

assert.match(portalNav, /\/downloads/);
assert.match(adminNav, /\/admin\/downloads/);

assert.match(downloadsPage, /await requireClientSession\(\)/);
assert.match(downloadsPage, /getClientDownloads/);
assert.match(downloadsPage, /<details/);
assert.match(downloadsPage, /Télécharger/);

for (const page of [
  adminDownloadsPage,
  adminDownloadNewPage,
  adminDownloadDetailPage,
  adminDownloadCategoriesPage,
]) {
  assert.match(page, /await requireAdminSession\(\)/);
}

assert.match(clientDownloadsRoute, /handlePortalGet<PortalDownloadCategory\[]>/);
assert.match(clientDownloadFileRoute, /\/internal\/portal\/downloads\/\$\{encodeURIComponent\(id\)\}\/file/);
assert.match(clientDownloadFileRoute, /redirect: "manual"/);
assert.doesNotMatch(clientDownloadFileRoute, /public\//i);

assert.match(adminDownloadsRoute, /handleAdminGet<DownloadResource\[]>/);
assert.match(adminDownloadsRoute, /handleAdminMutation/);
assert.match(adminDownloadDetailRoute, /handleAdminGet<DownloadResource>/);
assert.match(adminDownloadDetailRoute, /handleAdminMutation/);
assert.match(adminDownloadFileRoute, /hasValidCsrfToken/);
assert.match(adminDownloadFileRoute, /getInternalSession/);
assert.match(adminCategoriesRoute, /handleAdminGet<DownloadCategory\[]>/);
assert.match(adminCategoriesRoute, /handleAdminMutation/);
assert.match(adminCategoryDetailRoute, /handleAdminMutation/);

assert.match(styles, /\.downloads-accordion/);
assert.match(styles, /\.download-card/);
assert.match(styles, /\.admin-download-layout/);
assert.match(styles, /\.admin-checkbox-group/);

console.log("Vérification du contrat téléchargements V0.37 réussie.");
