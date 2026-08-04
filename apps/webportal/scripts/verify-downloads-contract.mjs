import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

function scanCSharpCharacterEnd(source, start) {
  let index = start + 1;
  while (index < source.length) {
    if (source[index] === "\\") {
      index += 2;
    } else if (source[index] === "'") {
      return index + 1;
    } else {
      index += 1;
    }
  }
  return source.length;
}

function scanCSharpStringEnd(source, start) {
  let quoteCount = 1;
  while (source[start + quoteCount] === '"') {
    quoteCount += 1;
  }
  if (quoteCount >= 3) {
    const delimiter = '"'.repeat(quoteCount);
    const end = source.indexOf(delimiter, start + quoteCount);
    return end === -1 ? source.length : end + quoteCount;
  }

  const verbatim =
    source[start - 1] === "@" ||
    (source[start - 1] === "$" && source[start - 2] === "@");
  const interpolated =
    source[start - 1] === "$" ||
    (source[start - 1] === "@" && source[start - 2] === "$" && source[start - 3] !== "$" );
  let interpolationDepth = 0;
  let index = start + 1;

  while (index < source.length) {
    if (interpolationDepth === 0) {
      if (!verbatim && source[index] === "\\") {
        index += 2;
      } else if (verbatim && source.startsWith('""', index)) {
        index += 2;
      } else if (source[index] === '"') {
        return index + 1;
      } else if (interpolated && source[index] === "{" && source[index + 1] !== "{") {
        interpolationDepth = 1;
        index += 1;
      } else if (
        interpolated &&
        (source.startsWith("{{", index) || source.startsWith("}}", index))
      ) {
        index += 2;
      } else {
        index += 1;
      }
      continue;
    }

    if (source.startsWith("//", index)) {
      const end = source.indexOf("\n", index + 2);
      index = end === -1 ? source.length : end;
    } else if (source.startsWith("/*", index)) {
      const end = source.indexOf("*/", index + 2);
      index = end === -1 ? source.length : end + 2;
    } else if (source[index] === '"') {
      index = scanCSharpStringEnd(source, index);
    } else if (source[index] === "'") {
      index = scanCSharpCharacterEnd(source, index);
    } else if (source[index] === "{") {
      interpolationDepth += 1;
      index += 1;
    } else if (source[index] === "}") {
      interpolationDepth -= 1;
      index += 1;
    } else {
      index += 1;
    }
  }
  return source.length;
}

function neutralizeCSharpTriviaAndLiterals(source) {
  const cleaned = source.split("");
  const blank = (start, end) => {
    for (let index = start; index < end; index += 1) {
      if (cleaned[index] !== "\r" && cleaned[index] !== "\n") {
        cleaned[index] = " ";
      }
    }
  };

  let index = 0;
  while (index < source.length) {
    let end = index + 1;
    if (source.startsWith("//", index)) {
      const newline = source.indexOf("\n", index + 2);
      end = newline === -1 ? source.length : newline;
    } else if (source.startsWith("/*", index)) {
      const commentEnd = source.indexOf("*/", index + 2);
      end = commentEnd === -1 ? source.length : commentEnd + 2;
    } else if (source[index] === '"') {
      end = scanCSharpStringEnd(source, index);
    } else if (source[index] === "'") {
      end = scanCSharpCharacterEnd(source, index);
    } else {
      index += 1;
      continue;
    }
    blank(index, end);
    index = end;
  }

  return cleaned.join("");
}

function extractCSharpMethodBody(source, signature) {
  const cleaned = neutralizeCSharpTriviaAndLiterals(source);
  const match = cleaned.match(signature);
  assert.ok(match, `Signature C# publique introuvable: ${signature}`);

  let openingBrace = match.index + match[0].length;
  while (/\s/.test(cleaned[openingBrace] ?? "")) {
    openingBrace += 1;
  }
  assert.equal(
    cleaned[openingBrace],
    "{",
    `Corps C# avec accolade attendu immediatement apres: ${signature}`,
  );

  let depth = 0;
  for (let index = openingBrace; index < cleaned.length; index += 1) {
    if (cleaned[index] === "{") {
      depth += 1;
    } else if (cleaned[index] === "}") {
      depth -= 1;
      if (depth === 0) {
        return cleaned.slice(openingBrace + 1, index);
      }
    }
  }

  assert.fail(`Accolades C# non equilibrees: ${signature}`);
}

const getActiveServiceTypesSignature =
  /\bpublic\s+async\s+Task<IReadOnlySet<string>>\s+GetActiveServiceTypesAsync\(\s*PortalSessionContext\s+session,\s*CancellationToken\s+cancellationToken\s*\)/;
const getServicesSignature =
  /\bpublic\s+async\s+Task<IReadOnlyList<ServiceSummary>>\s+GetServicesAsync\(\s*PortalSessionContext\s+session,\s*CancellationToken\s+cancellationToken\s*\)/;
const buildAccessScopeSignature =
  /\bprivate\s+async\s+Task<DownloadAccessScope>\s+BuildAccessScopeAsync\(\s*PortalSessionContext\s+session,\s*CancellationToken\s+cancellationToken\s*\)/;
const getServicesCall =
  /\bGetServicesAsync\(\s*session,\s*cancellationToken\s*\)/;
const getSubscriptionsByCustomerCall =
  /\b_subscriptions\.GetByCustomerAsync\(\s*session\.CustomerId,\s*cancellationToken\s*\)/;
const getActiveServiceTypesCall =
  /\b_serviceCatalogService\.GetActiveServiceTypesAsync\(\s*session,\s*cancellationToken\s*\)/;

const sharedTypes = await read("../../packages/shared/src/index.ts");
const internalApi = await read("lib/internal-api.ts");
const downloadService = await read(
  "../api-internal/Services/DownloadService.cs",
);
const clientServiceCatalogService = await read(
  "../api-internal/Services/ClientServiceCatalogService.cs",
);
const downloadSchemaEnsurer = await read(
  "../api-internal/Services/DownloadSchemaEnsurer.cs",
);
const apiProgram = await read("../api-internal/Program.cs");
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

assert.match(
  extractCSharpMethodBody(downloadService, buildAccessScopeSignature),
  getActiveServiceTypesCall,
);
assert.match(
  extractCSharpMethodBody(
    clientServiceCatalogService,
    getActiveServiceTypesSignature,
  ),
  getServicesCall,
);
assert.match(
  extractCSharpMethodBody(clientServiceCatalogService, getServicesSignature),
  getSubscriptionsByCustomerCall,
);

const commentAndLiteralDecoys = `
public async Task<IReadOnlySet<string>> GetActiveServiceTypesAsync(
    PortalSessionContext session,
    CancellationToken cancellationToken)
{
    // GetServicesAsync(session, cancellationToken); }
    var decoy = "GetServicesAsync(session, cancellationToken); }";
    return new HashSet<string>();
}
`;
assert.doesNotMatch(
  extractCSharpMethodBody(
    commentAndLiteralDecoys,
    getActiveServiceTypesSignature,
  ),
  getServicesCall,
);

const expressionBodyFollowedByDecoy = `
public async Task<IReadOnlySet<string>> GetActiveServiceTypesAsync(
    PortalSessionContext session,
    CancellationToken cancellationToken)
    => new HashSet<string>();

private void Decoy()
{
    GetServicesAsync(session, cancellationToken);
}
`;
assert.throws(
  () =>
    extractCSharpMethodBody(
      expressionBodyFollowedByDecoy,
      getActiveServiceTypesSignature,
    ),
  /Corps C# avec accolade attendu immediatement apres/,
);

const buildAccessScopeDecoys = `
private async Task<DownloadAccessScope> BuildAccessScopeAsync(
    PortalSessionContext session,
    CancellationToken cancellationToken)
{
    // _serviceCatalogService.GetActiveServiceTypesAsync(session, cancellationToken); }
    var decoy = "_serviceCatalogService.GetActiveServiceTypesAsync(session, cancellationToken); }";
    return new DownloadAccessScope();
}
`;
assert.doesNotMatch(
  extractCSharpMethodBody(buildAccessScopeDecoys, buildAccessScopeSignature),
  getActiveServiceTypesCall,
);

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

// Garde-fou V1.1.9.2 : le centre de téléchargements ne doit exécuter aucun DDL
// au fil des requêtes. Le compte applicatif MariaDB n'a pas les droits de
// schéma ; appeler le runner de migrations depuis une requête rendait toute la
// rubrique indisponible en `SQL_UNAVAILABLE`.
assert.doesNotMatch(downloadSchemaEnsurer, /MariaDbMigrationRunner/);
assert.doesNotMatch(
  downloadSchemaEnsurer,
  /\b(CREATE|ALTER|DROP)\s+(TABLE|INDEX)\b/i,
);
assert.match(downloadSchemaEnsurer, /information_schema\.tables/);
assert.match(downloadSchemaEnsurer, /DownloadSchemaUnavailableException/);
assert.match(apiProgram, /DownloadSchemaUnavailableException => \(/);
assert.match(apiProgram, /"DOWNLOADS_SCHEMA_UNAVAILABLE"/);

assert.match(styles, /\.downloads-accordion/);
assert.match(styles, /\.download-card/);
assert.match(styles, /\.admin-download-layout/);
assert.match(styles, /\.admin-checkbox-group/);

console.log("Vérification du contrat téléchargements V0.37 réussie.");
