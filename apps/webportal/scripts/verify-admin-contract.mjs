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
assert.doesNotMatch(internalApi, /NEXT_PUBLIC_INTERNAL_API_URL/);
assert.match(appShell, /session\?\.user\.role === "internal_admin"/);

for (const route of [
  "overview",
  "customers",
  "support-requests",
  "service-requests",
  "sessions",
  "audit-logs",
]) {
  const source = await read(`app/api/admin/${route}/route.ts`);
  assert.match(source, /handleAdminGet/);
  assert.match(source, /export function GET\(/);
}

for (const page of [
  "page.tsx",
  "customers/page.tsx",
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

console.log("Vérification du contrat d'administration BFF réussie.");
