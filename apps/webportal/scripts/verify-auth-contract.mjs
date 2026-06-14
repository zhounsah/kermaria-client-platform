import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const loginForm = await read("components/LoginForm.tsx");
const loginRoute = await read("app/api/auth/login/route.ts");
const logoutRoute = await read("app/api/auth/logout/route.ts");
const meRoute = await read("app/api/auth/me/route.ts");
const revokeOthersRoute = await read(
  "app/api/auth/revoke-other-sessions/route.ts",
);
const sessionConfig = await read("lib/session-config.ts");
const internalApi = await read("lib/internal-api.ts");
const runtimeConfig = await read("lib/runtime-config.ts");
const clientApi = await read("lib/client-api.ts");
const authHelper = await read("lib/auth.ts");

assert.match(loginForm, /event\.preventDefault\(\)/);
assert.match(loginForm, /requestBffJson<AuthMeResponse>/);
assert.match(loginForm, /"\/api\/auth\/login"/);
assert.match(loginForm, /method:\s*"POST"/);
assert.match(loginForm, /"Content-Type":\s*"application\/json"/);
assert.match(loginForm, /JSON\.stringify\(validation\.payload\)/);
assert.match(loginForm, /isSubmittingRef\.current/);
assert.match(loginForm, /aria-invalid/);
assert.doesNotMatch(
  loginForm,
  /FormData|URLSearchParams|method="get"|localStorage|sessionStorage/i,
);

assert.match(loginRoute, /export async function POST\(/);
assert.match(loginRoute, /getSessionCookieOptions\(\)/);
assert.doesNotMatch(loginRoute, /sessionToken\s*[:,]\s*session\.sessionToken/);

assert.match(logoutRoute, /export async function POST\(/);
assert.match(logoutRoute, /revokeInternalSession/);
assert.match(logoutRoute, /expires:\s*new Date\(0\)/);

assert.match(meRoute, /export async function GET\(/);
assert.match(meRoute, /authenticated:\s*false/);
assert.match(meRoute, /authenticated:\s*true/);
assert.match(revokeOthersRoute, /export async function POST\(/);
assert.match(revokeOthersRoute, /revokeOtherInternalSessions/);
assert.doesNotMatch(
  revokeOthersRoute,
  /URLSearchParams|localStorage|sessionStorage/i,
);

assert.match(sessionConfig, /process\.env\.SESSION_COOKIE_NAME/);
assert.match(sessionConfig, /process\.env\.SESSION_COOKIE_SECURE/);
assert.match(sessionConfig, /httpOnly:\s*true/);
assert.match(sessionConfig, /sameSite:\s*"lax"/);
assert.match(sessionConfig, /path:\s*"\/"/);
assert.doesNotMatch(sessionConfig, /NEXT_PUBLIC_|PUBLIC_INTERNAL_API_URL/);

assert.match(internalApi, /import "server-only"/);
assert.match(internalApi, /X-Portal-Session/);
assert.match(internalApi, /getInternalApiUrl/);
assert.doesNotMatch(internalApi, /NEXT_PUBLIC_INTERNAL_API_URL/);
assert.match(runtimeConfig, /process\.env\.INTERNAL_API_URL/);
assert.doesNotMatch(runtimeConfig, /NEXT_PUBLIC_INTERNAL_API_URL/);
assert.match(clientApi, /path:\s*`\/api\/\$\{string\}`/);
assert.match(clientApi, /AbortController/);
assert.doesNotMatch(
  clientApi,
  /INTERNAL_API_URL|SERVICE_AUTH_TOKEN|localStorage|sessionStorage/,
);

assert.match(authHelper, /redirect\("\/login"\)/);
assert.match(authHelper, /requireAdminSession/);
assert.match(authHelper, /requireClientSession/);

for (const page of [
  "dashboard",
  "services",
  "invoices",
  "support",
  "request-service",
  "profile",
  "password",
]) {
  const source = await read(`app/${page}/page.tsx`);
  assert.match(
    source,
    /await requireClientSession\(\)/,
    `La page privée /${page} doit exiger une session client.`,
  );
}

for (const route of [
  "app/api/support-requests/route.ts",
  "app/api/service-requests/route.ts",
]) {
  const source = await read(route);
  assert.match(source, /getSessionCookieName/);
  assert.match(source, /SESSION_REQUIRED/);
  assert.doesNotMatch(source, /URLSearchParams|FormData|method="get"/i);
}

console.log("Vérification du contrat d'authentification BFF réussie.");
