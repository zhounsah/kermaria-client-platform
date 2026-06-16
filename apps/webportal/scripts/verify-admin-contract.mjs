import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const adminBff = await read("lib/admin-bff.ts");
const internalApi = await read("lib/internal-api.ts");
const appShell = await read("components/AppShell.tsx");

assert.match(adminBff, /getInternalSession/);
assert.match(adminBff, /session\.user\.role !== "internal_admin"/);
assert.match(adminBff, /getInternalAdminData/);
assert.match(adminBff, /ACCESS_DENIED/);
assert.doesNotMatch(
  adminBff,
  /localStorage|sessionStorage|NEXT_PUBLIC_INTERNAL_API_URL/i,
);

assert.match(internalApi, /import "server-only"/);
assert.match(internalApi, /\/internal\/admin\//);
assert.match(internalApi, /getAdminCustomer/);
assert.doesNotMatch(internalApi, /NEXT_PUBLIC_INTERNAL_API_URL/);
assert.match(appShell, /session\?\.user\.role === "internal_admin"/);

for (const route of [
  "overview",
  "customers",
  "customers/[customerReference]",
  "support-requests",
  "service-requests",
  "sessions",
  "audit-logs",
]) {
  const source = await read(`app/api/admin/${route}/route.ts`);
  assert.match(source, /handleAdminGet/);
}

const customerDetailRoute = await read(
  "app/api/admin/customers/[customerReference]/route.ts",
);
assert.match(customerDetailRoute, /isValidPortalIdentifier/);
assert.match(customerDetailRoute, /INVALID_REQUEST/);

for (const page of [
  "page.tsx",
  "customers/page.tsx",
  "customers/[customerReference]/page.tsx",
  "support-requests/page.tsx",
  "service-requests/page.tsx",
  "sessions/page.tsx",
  "audit-logs/page.tsx",
]) {
  const source = await read(`app/admin/${page}`);
  assert.match(
    source,
    /await requireAdminSession\(\)/,
    `La page admin ${page} doit exiger le rôle internal_admin.`,
  );
  assert.doesNotMatch(
    source,
    /sessionToken|passwordHash|SQL_PASSWORD|INTERNAL_API_URL/,
  );
}

const customerDetailPage = await read(
  "app/admin/customers/[customerReference]/page.tsx",
);
assert.match(customerDetailPage, /getAdminCustomer/);
assert.match(customerDetailPage, /Isolation métier/);
assert.match(customerDetailPage, /Documents commerciaux associés/);
assert.match(customerDetailPage, /Audits récents du client/);

console.log("Vérification du contrat d'administration BFF réussie.");
