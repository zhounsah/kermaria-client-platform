import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const sharedTypes = await read("../../packages/shared/src/index.ts");
const internalApi = await read("lib/internal-api.ts");
const payloads = await read("lib/bff-payloads.ts");
const publicRouteConfig = await read("lib/public-route-config.ts");
const publicShell = await read("components/PublicShell.tsx");
const adminNav = await read("components/AdminNavigation.tsx");
const sitemap = await read("app/sitemap.ts");
const solutionsPage = await read("app/solutions/page.tsx");
const adminSolutionsPage = await read("app/admin/solutions/page.tsx");
const adminSolutionNewPage = await read("app/admin/solutions/new/page.tsx");
const adminSolutionDetailPage = await read("app/admin/solutions/[id]/page.tsx");
const adminSolutionForm = await read("components/AdminClientSolutionForm.tsx");
const adminSettingsForm = await read(
  "components/AdminClientSolutionPortalSettingsForm.tsx",
);
const publicLogoRoute = await read("app/api/solutions/[id]/logo/route.ts");
const adminSolutionsRoute = await read("app/api/admin/client-solutions/route.ts");
const adminSolutionDetailRoute = await read(
  "app/api/admin/client-solutions/[id]/route.ts",
);
const adminLogoRoute = await read(
  "app/api/admin/client-solutions/[id]/logo/route.ts",
);
const adminSettingsRoute = await read(
  "app/api/admin/client-solutions/settings/route.ts",
);
const styles = await read("app/globals.css");
const contracts = await read(
  "../api-internal/Contracts/ClientSolutionContracts.cs",
);
const service = await read("../api-internal/Services/ClientSolutionService.cs");
const mariaDbRepository = await read(
  "../api-internal/Data/Repositories/MariaDbClientSolutionRepository.cs",
);
const schemaEnsurer = await read(
  "../api-internal/Services/ClientSolutionSchemaEnsurer.cs",
);
const program = await read("../api-internal/Program.cs");
const migration = await read(
  "../api-internal/Migrations/MariaDb/041_client_solutions_portal.sql",
);

// Contrat de types partages
assert.match(sharedTypes, /type ClientSolutionStatus = "published" \| "draft";/);
assert.match(sharedTypes, /interface PublicClientSolution\b/);
assert.match(sharedTypes, /interface PublicClientSolutionPortal\b/);
assert.match(sharedTypes, /interface AdminClientSolutionPortal\b/);
assert.match(sharedTypes, /interface ClientSolutionPayload\b/);
assert.match(sharedTypes, /interface ClientSolutionPortalSettingsPayload\b/);
assert.match(sharedTypes, /CLIENT_SOLUTION_STATUSES/);
assert.match(sharedTypes, /CLIENT_SOLUTION_LOGO_CONTENT_TYPES/);
assert.match(sharedTypes, /CLIENT_SOLUTION_LOGO_MAX_SIZE_BYTES = 512 \* 1024/);
assert.match(sharedTypes, /createDefaultClientSolutionPortal\(/);

// Acces API interne
assert.match(internalApi, /getPublicClientSolutionPortal/);
assert.match(internalApi, /getAdminClientSolutionPortal/);
assert.match(internalApi, /getAdminClientSolution\(/);
assert.match(internalApi, /\/internal\/portal\/client-solutions/);
assert.match(internalApi, /\/internal\/admin\/client-solutions/);
// La page publique ne doit jamais exiger de session cliente.
assert.match(
  internalApi,
  /getPublicClientSolutionPortal\(\)\s*\{\s*return getPublicData</,
);

// Validation BFF
assert.match(payloads, /parseClientSolutionPayload/);
assert.match(payloads, /parseClientSolutionPortalSettingsPayload/);
assert.match(payloads, /CLIENT_SOLUTION_STATUSES\.includes\(payload\.status\)/);
assert.match(payloads, /isAbsoluteWebUrl\(payload\.targetUrl\)/);

// Exposition publique
assert.match(publicRouteConfig, /"\/solutions"/);
assert.match(publicShell, /href="\/solutions"/);
assert.match(sitemap, /path: "\/solutions"/);
assert.match(adminNav, /\/admin\/solutions/);

// Page vitrine
assert.match(solutionsPage, /getPublicClientSolutionPortal/);
assert.doesNotMatch(solutionsPage, /requireClientSession|requireAdminSession/);
assert.match(solutionsPage, /settings\.title/);
assert.match(solutionsPage, /solution\.targetUrl/);
assert.match(solutionsPage, /rel: "noopener noreferrer"/);
assert.match(solutionsPage, /\/api\/solutions\/\$\{encodeURIComponent\(solution\.id\)\}\/logo/);
assert.match(solutionsPage, /buildMonogram/);

// Ecrans d'administration
for (const page of [
  adminSolutionsPage,
  adminSolutionNewPage,
  adminSolutionDetailPage,
]) {
  assert.match(page, /await requireAdminSession\(\)/);
}
assert.match(adminSolutionsPage, /getAdminClientSolutionPortal/);
assert.match(adminSolutionsPage, /AdminClientSolutionPortalSettingsForm/);
assert.match(adminSolutionForm, /\/api\/admin\/client-solutions/);
assert.match(adminSolutionForm, /method: "DELETE"/);
assert.match(adminSolutionForm, /body\.set\("logo", selectedLogo\)/);
assert.match(adminSettingsForm, /\/api\/admin\/client-solutions\/settings/);

// Routes BFF
assert.match(adminSolutionsRoute, /handleAdminGet<AdminClientSolutionPortal>/);
assert.match(adminSolutionsRoute, /parseClientSolutionPayload/);
assert.match(adminSolutionDetailRoute, /handleAdminGet<ClientSolution>/);
assert.match(adminSolutionDetailRoute, /"PATCH"/);
assert.match(adminSolutionDetailRoute, /"DELETE"/);
assert.match(adminSettingsRoute, /parseClientSolutionPortalSettingsPayload/);
assert.match(adminLogoRoute, /hasValidCsrfToken/);
assert.match(adminLogoRoute, /getInternalSession/);
assert.match(adminLogoRoute, /CLIENT_SOLUTION_LOGO_MAX_SIZE_BYTES/);
assert.match(adminLogoRoute, /CLIENT_SOLUTION_LOGO_CONTENT_TYPES/);

// Route publique du logo : sans session, mais sans execution de script
assert.doesNotMatch(publicLogoRoute, /getSessionCookieName|getInternalSession/);
assert.match(publicLogoRoute, /"X-Content-Type-Options": "nosniff"/);
assert.match(publicLogoRoute, /"Content-Security-Policy"/);
assert.match(publicLogoRoute, /default-src 'none'/);

// Styles
assert.match(styles, /\.solutions-grid/);
assert.match(styles, /\.solution-tile\b/);
assert.match(styles, /\.solution-tile-monogram/);
assert.match(styles, /\.admin-solution-layout/);

// Contrats et service API interne
assert.match(contracts, /class ClientSolutionStatuses/);
assert.match(contracts, /Published = "published"/);
assert.match(contracts, /Draft = "draft"/);
assert.match(contracts, /MaxSizeBytes = 512 \* 1024/);
assert.match(contracts, /record PublicClientSolutionPortal/);
assert.match(service, /interface IClientSolutionService/);
assert.match(service, /GetPublicPortalAsync/);
assert.match(service, /GetPublicLogoAsync/);
// Seules les solutions publiees sortent cote vitrine.
assert.match(
  service,
  /solution\.Status == ClientSolutionStatuses\.Published/,
);
assert.match(service, /solution\.Status != ClientSolutionStatuses\.Published/);
assert.match(service, /IsAbsoluteWebUrl/);
assert.match(service, /CLIENT_SOLUTION_LOGO_TOO_LARGE|LOGO_TOO_LARGE/);

// Persistance : horodatages UTC et parametres SQL nommes
assert.match(mariaDbRepository, /DateTime\.SpecifyKind\(value, DateTimeKind\.Utc\)/);
assert.match(mariaDbRepository, /DateTime\.UtcNow/);
assert.doesNotMatch(mariaDbRepository, /\bNOW\(\)/);

// Endpoints exposes
assert.match(program, /"\/internal\/portal\/client-solutions"/);
assert.match(program, /"\/internal\/portal\/client-solutions\/\{id\}\/logo"/);
assert.match(program, /"\/internal\/admin\/client-solutions"/);
assert.match(program, /"\/internal\/admin\/client-solutions\/settings"/);
assert.match(program, /"\/internal\/admin\/client-solutions\/\{id\}"/);
assert.match(program, /"\/internal\/admin\/client-solutions\/\{id\}\/logo"/);
assert.match(program, /admin\.client_solutions\.read/);
assert.match(program, /admin\.client_solutions\.write/);
assert.match(program, /ClientSolutionConflictException conflict =>/);
assert.match(program, /client_solution\.create/);
assert.match(program, /client_solution\.delete/);
assert.match(program, /client_solution\.logo\.upload/);

// Aucun DDL au fil des requetes : le compte applicatif MariaDB n'a pas les
// droits de schema. La precondition se verifie en lecture seule et remonte une
// erreur explicite (meme regle que le centre de telechargements, V1.1.9.2).
assert.doesNotMatch(schemaEnsurer, /MariaDbMigrationRunner/);
assert.doesNotMatch(schemaEnsurer, /\b(CREATE|ALTER|DROP)\s+(TABLE|INDEX)\b/i);
assert.match(schemaEnsurer, /information_schema\.tables/);
assert.match(schemaEnsurer, /ClientSolutionSchemaUnavailableException/);
assert.match(program, /ClientSolutionSchemaUnavailableException => \(/);
assert.match(program, /"CLIENT_SOLUTIONS_SCHEMA_UNAVAILABLE"/);

// Migration MariaDB
assert.match(migration, /CREATE TABLE IF NOT EXISTS client_solutions/);
assert.match(
  migration,
  /CREATE TABLE IF NOT EXISTS client_solution_portal_settings/,
);
assert.match(migration, /UNIQUE KEY uq_client_solutions_slug/);
assert.match(migration, /UTC_TIMESTAMP\(6\)/);
assert.doesNotMatch(migration, /\bNOW\(\)/);

console.log("Vérification du contrat portail solutions réussie.");
