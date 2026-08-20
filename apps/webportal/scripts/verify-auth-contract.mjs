import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import ts from "typescript";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

async function importPureTypeScript(source, label) {
  const transpiled = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.ES2022,
      target: ts.ScriptTarget.ES2022,
    },
    fileName: label,
    reportDiagnostics: true,
  });
  const errors = (transpiled.diagnostics ?? []).filter(
    (diagnostic) => diagnostic.category === ts.DiagnosticCategory.Error,
  );
  assert.deepEqual(errors, [], `${label} doit être transpile sans erreur.`);

  const encoded = Buffer.from(transpiled.outputText).toString("base64");
  return import(`data:text/javascript;base64,${encoded}`);
}

const publicRouteConfig = await read("lib/public-route-config.ts");
const publicRoutes = await read("lib/public-routes.ts");
const homePage = await read("app/page.tsx");
const loginPage = await read("app/login/page.tsx");
const loginForm = await read("components/LoginForm.tsx");
const loginRoute = await read("app/api/auth/login/route.ts");
const logoutRoute = await read("app/api/auth/logout/route.ts");
const meRoute = await read("app/api/auth/me/route.ts");
const revokeOthersRoute = await read(
  "app/api/auth/revoke-other-sessions/route.ts",
);
const csrfHelper = await read("lib/csrf.ts");
const csrfServerHelper = await read("lib/csrf-server.ts");
const sessionConfig = await read("lib/session-config.ts");
const internalApi = await read("lib/internal-api.ts");
const runtimeConfig = await read("lib/runtime-config.ts");
const clientApi = await read("lib/client-api.ts");
const authHelper = await read("lib/auth.ts");

const routing = await importPureTypeScript(
  publicRouteConfig,
  "public-route-config.ts",
);
const executablePublicRoutes = publicRoutes
  .replace('import "server-only";', "")
  .replace(
    /import \{[\s\S]*?\} from "\.\/public-route-config";/,
    [
      "const isPublicRoute = () => false;",
      "const getPortalArea = () => null;",
      'const PORTFOLIO_URL = "";',
      'const PUBLIC_SITE_URL = "";',
      "const PUBLIC_ROUTES = [];",
    ].join("\n"),
  );
assert.notEqual(
  executablePublicRoutes,
  publicRoutes,
  "Le module public-routes doit être préparé pour son exécution isolée.",
);
const publicRoutesRuntime = await importPureTypeScript(
  executablePublicRoutes,
  "public-routes.ts",
);
const {
  getPortalArea,
  isPortalRoleAllowed,
  resolveClientCheckoutContinuationPath,
  resolvePortalAreaUrl,
  resolvePortalRoleUrl,
} = routing;

function createHeaders(values) {
  const entries = new Map(
    Object.entries(values).map(([name, value]) => [name.toLowerCase(), value]),
  );
  return {
    get(name) {
      return entries.get(name.toLowerCase()) ?? null;
    },
  };
}

for (const [headers, expectedOrigin] of [
  [{ host: "localhost:3000" }, "http://localhost:3000"],
  [{ host: "127.0.0.1:3100" }, "http://127.0.0.1:3100"],
  [{ host: "[::1]:3200" }, "http://[::1]:3200"],
  [{ host: "zachary-it.fr" }, "https://zachary-it.fr"],
  [
    { host: "dashboard.zacharyhounsa.ovh" },
    "https://dashboard.zacharyhounsa.ovh",
  ],
  [
    { host: "localhost:3443", "x-forwarded-proto": "https" },
    "https://localhost:3443",
  ],
  [
    {
      host: "dashboard.zacharyhounsa.ovh",
      "x-forwarded-proto": "http",
    },
    "http://dashboard.zacharyhounsa.ovh",
  ],
]) {
  assert.equal(
    publicRoutesRuntime.getPortalRequestOriginFromHeaders(
      createHeaders(headers),
    ),
    expectedOrigin,
    JSON.stringify(headers),
  );
}

for (const [origin, expectedArea] of [
  ["https://zachary-it.fr", "public"],
  ["https://www.zachary-it.fr", "public"],
  ["https://zacharyhounsa.ovh", "public"],
  ["https://www.zacharyhounsa.ovh", "public"],
  ["https://dashboard.zacharyhounsa.ovh", "client"],
  ["https://administration.zacharyhounsa.ovh", "admin"],
  ["https://home.bzh", "public"],
  ["https://www.home.bzh", "public"],
  ["https://portail.home.bzh", "public"],
  ["https://dashboard.home.bzh", "client"],
  ["https://administration.home.bzh", "admin"],
  ["https://DASHBOARD.ZACHARYHOUNSA.OVH/path?mode=test#section", "client"],
  ["http://localhost:3000", "local"],
  ["https://127.0.0.1:3443/path", "local"],
  ["http://[::1]:3000/login", "local"],
]) {
  assert.equal(getPortalArea(origin), expectedArea, origin);
}

for (const origin of [
  null,
  "",
  "not-an-url",
  "//dashboard.zacharyhounsa.ovh",
  "ftp://dashboard.zacharyhounsa.ovh",
  "https://user@dashboard.zacharyhounsa.ovh",
  "https://user:secret@dashboard.zacharyhounsa.ovh",
  "https://dashboard.zacharyhounsa.ovh.evil.example",
  "https://unknown.example",
]) {
  assert.equal(getPortalArea(origin), null, String(origin));
}

assert.equal(
  resolvePortalAreaUrl(
    "https://zachary-it.fr",
    "client",
    "/login?error=PORTAL_ROLE_MISMATCH#form",
  ),
  "https://dashboard.zacharyhounsa.ovh/login?error=PORTAL_ROLE_MISMATCH#form",
);
assert.equal(
  resolvePortalAreaUrl(
    "http://www.zacharyhounsa.ovh:8080",
    "client",
    "/login?error=PORTAL_ROLE_MISMATCH#form",
  ),
  "https://dashboard.zacharyhounsa.ovh/login?error=PORTAL_ROLE_MISMATCH#form",
);
assert.equal(
  resolvePortalAreaUrl(
    "https://PORTAIL.HOME.BZH:9443",
    "admin",
    "/admin/audit?case=Mixed#latest",
  ),
  "https://administration.home.bzh/admin/audit?case=Mixed#latest",
);
assert.equal(
  resolvePortalAreaUrl("https://home.bzh", "public", "/offres"),
  "https://zachary-it.fr/offres",
);
assert.equal(
  resolvePortalAreaUrl("https://zachary-it.fr", "public", "/diagnostic"),
  "https://zachary-it.fr/diagnostic",
);
assert.equal(
  resolvePortalAreaUrl("https://www.zacharyhounsa.ovh", "public", "/contact"),
  "https://zachary-it.fr/contact",
);
assert.equal(
  resolvePortalAreaUrl("https://dashboard.zacharyhounsa.ovh", "public", "/offres"),
  "https://zachary-it.fr/offres",
);
assert.equal(
  resolvePortalAreaUrl("https://www.home.bzh", "public", "/offres"),
  "https://zachary-it.fr/offres",
);
assert.equal(
  resolvePortalAreaUrl("https://unknown.example", "public", "/offres"),
  null,
);
assert.equal(
  resolvePortalAreaUrl(
    "https://dashboard.zacharyhounsa.ovh.evil.example",
    "public",
    "/offres",
  ),
  null,
);
assert.equal(
  resolvePortalAreaUrl("http://localhost:3000", "admin", "/admin?tab=users"),
  "http://localhost:3000/admin?tab=users",
);
assert.equal(
  resolvePortalAreaUrl("https://127.0.0.1:3443", "client", "/dashboard"),
  "https://127.0.0.1:3443/dashboard",
);
assert.equal(
  resolvePortalAreaUrl("http://[::1]:3000", "public", "/offres#packs"),
  "http://[::1]:3000/offres#packs",
);

for (const hostilePath of [
  "",
  "login",
  "https://evil.example/login",
  "//evil.example/login",
  "/\\evil.example/login",
  "/folder\\login",
  "/%2fevil.example",
  "/%2Fevil.example",
  "/%5cevil.example",
  "/%5Cevil.example",
  "/%00login",
  "/%0alogin",
  "/%1flogin",
  "/%7flogin",
  "/login\nheader:value",
]) {
  assert.equal(
    resolvePortalAreaUrl(
      "https://dashboard.zacharyhounsa.ovh",
      "client",
      hostilePath,
    ),
    null,
    hostilePath,
  );
}

assert.equal(
  resolvePortalRoleUrl("https://zachary-it.fr", "client_user"),
  "https://dashboard.zacharyhounsa.ovh/dashboard",
);
assert.equal(
  resolvePortalRoleUrl("https://www.zacharyhounsa.ovh", "client_user"),
  "https://dashboard.zacharyhounsa.ovh/dashboard",
);
assert.equal(
  resolvePortalRoleUrl("https://www.zacharyhounsa.ovh", "internal_admin"),
  "https://administration.zacharyhounsa.ovh/admin",
);
assert.equal(
  resolvePortalRoleUrl(
    "https://www.zacharyhounsa.ovh",
    "internal_admin",
    "/login",
  ),
  "https://administration.zacharyhounsa.ovh/login",
);
assert.equal(
  resolvePortalRoleUrl("http://localhost:3000", "internal_admin"),
  "http://localhost:3000/admin",
);
assert.equal(resolvePortalRoleUrl("https://www.home.bzh", "unknown_role"), null);
assert.equal(isPortalRoleAllowed("client", "client_user"), true);
assert.equal(isPortalRoleAllowed("client", "internal_admin"), false);
assert.equal(isPortalRoleAllowed("admin", "internal_admin"), true);
assert.equal(isPortalRoleAllowed("admin", "client_user"), false);
assert.equal(isPortalRoleAllowed("public", "client_user"), false);
assert.equal(isPortalRoleAllowed("local", "client_user"), true);
assert.equal(isPortalRoleAllowed("local", "internal_admin"), true);
assert.equal(isPortalRoleAllowed("local", "unknown_role"), false);
assert.equal(isPortalRoleAllowed(null, "client_user"), false);

assert.equal(
  resolveClientCheckoutContinuationPath("/formules/pack-pro-association"),
  "/formules/pack-pro-association",
);
assert.equal(resolveClientCheckoutContinuationPath("/formules"), "/formules");
for (const unsafeContinuation of [
  "/dashboard",
  "/formules/pack-pro-association/details",
  "/formules/%2e%2e/admin",
  "//evil.example",
  ["/formules/pack-pro-association"],
]) {
  assert.equal(
    resolveClientCheckoutContinuationPath(unsafeContinuation),
    null,
    `Continuation invalide acceptee: ${String(unsafeContinuation)}`,
  );
}

assert.doesNotMatch(publicRouteConfig, /process\.env|server-only/);
assert.match(publicRoutes, /export function getPortalRequestOriginFromHeaders/);
assert.match(publicRoutes, /hostname\.startsWith\("\["\)/);
assert.match(homePage, /getPortalArea\(origin\)/);
assert.match(homePage, /notFound\(\)/);
assert.match(homePage, /resolvePortalRoleUrl/);
assert.match(homePage, /isPortalRoleAllowed/);
assert.doesNotMatch(homePage, /redirect\("\/(?:login|dashboard|admin)"\)/);
for (const presentationCode of [
  "INVALID_CREDENTIALS",
  "LOGIN_REQUEST_TOO_LARGE",
  "LOGIN_UNAVAILABLE",
  "PORTAL_ROLE_MISMATCH",
]) {
  assert.match(loginPage, new RegExp(presentationCode));
}
assert.match(loginPage, /Object\.hasOwn\(LOGIN_ERROR_MESSAGES, errorCode\)/);
assert.doesNotMatch(loginPage, /query\.email|initialEmail/);
assert.match(loginPage, /resolveClientCheckoutContinuationPath\(query\.next\)/);
assert.match(loginPage, /continuationPath=\{continuationPath\}/);
assert.match(loginPage, /initialError=\{initialError\}/);
assert.match(loginPage, /portalArea=\{area\}/);
assert.match(loginPage, /notFound\(\)/);

assert.match(loginForm, /event\.preventDefault\(\)/);
assert.match(loginForm, /requestBffJson<AuthMeResponse>/);
assert.match(loginForm, /"\/api\/auth\/login"/);
assert.match(loginForm, /method:\s*"POST"/);
assert.match(loginForm, /"Content-Type":\s*"application\/json"/);
assert.match(loginForm, /JSON\.stringify\(validation\.payload\)/);
assert.match(loginForm, /isSubmittingRef\.current/);
assert.match(loginForm, /aria-invalid/);
assert.match(loginForm, /acceptCharset="UTF-8"/);
assert.match(loginForm, /encType="application\/x-www-form-urlencoded"/);
assert.match(loginForm, /continuationPath/);
assert.match(loginForm, /resolvePortalAreaUrl\(origin, "client", continuationPath\)/);
assert.match(loginForm, /resolvePortalRoleUrl\(origin, result\.user\.role\)/);
assert.match(loginForm, /"\/login\?error=PORTAL_ROLE_MISMATCH"/);
assert.match(loginForm, /window\.location\.assign\(target\)/);
assert.doesNotMatch(
  loginForm,
  /FormData|URLSearchParams|useRouter|router\.|localStorage|sessionStorage/i,
);
assert.doesNotMatch(loginForm, /[?&](?:email|username)=/i);

assert.match(loginRoute, /export async function POST\(/);
assert.match(loginRoute, /getPortalRequestOriginFromHeaders/);
assert.match(loginRoute, /status:\s*403/);
assert.match(loginRoute, /code:\s*"PORTAL_LOGIN_FORBIDDEN"/);
assert.match(loginRoute, /MAX_LOGIN_BODY_BYTES\s*=\s*16\s*\*\s*1024/);
assert.match(loginRoute, /application\/json/);
assert.match(loginRoute, /application\/x-www-form-urlencoded/);
assert.match(loginRoute, /status:\s*413/);
assert.match(loginRoute, /status:\s*415/);
assert.match(loginRoute, /new TextDecoder\("utf-8", \{ fatal: true \}\)/);
assert.match(loginRoute, /request\.body\.getReader\(\)/);
assert.match(loginRoute, /new URLSearchParams\(body\)/);
assert.match(loginRoute, /form\.getAll\("email"\)/);
assert.match(loginRoute, /form\.getAll\("password"\)/);
assert.match(loginRoute, /isSameOriginFormPost\(request, origin\)/);
assert.match(loginRoute, /url\.origin === origin/);
assert.match(loginRoute, /NextResponse\.redirect\(target, \{ status: 303 \}\)/);
assert.match(loginRoute, /`\/login\?error=\$\{code\}`/);
assert.match(loginRoute, /getSessionCookieOptions\(\)/);
assert.match(loginRoute, /ensureCsrfCookie/);
assert.match(loginRoute, /authenticated:\s*false/);
assert.doesNotMatch(loginRoute, /sessionToken\s*[:,]\s*session\.sessionToken/);
assert.doesNotMatch(loginRoute, /request\.formData\(|multipart\/form-data/);
assert.doesNotMatch(
  loginRoute,
  /searchParams\.set\(|[?&](?:email|password|token|correlation_id)=/i,
);

const classifyIndex = loginRoute.indexOf("const area = getPortalArea(origin)");
const portalGuardIndex = loginRoute.indexOf(
  'if (!origin || !area || area === "public")',
);
const formatIndex = loginRoute.indexOf("const format = getLoginRequestFormat");
const formOriginIndex = loginRoute.indexOf(
  'format === "form" && !isSameOriginFormPost',
);
const readPayloadIndex = loginRoute.indexOf("await readBoundedLoginBody");
const createSessionIndex = loginRoute.indexOf("await createInternalSession");
const allowRoleIndex = loginRoute.indexOf("isPortalRoleAllowed(area");
const revokeSessionIndex = loginRoute.indexOf("await revokeInternalSession");
const setCookieIndex = loginRoute.indexOf("response.cookies.set");
const setCsrfIndex = loginRoute.indexOf("ensureCsrfCookie(request, response)");
for (const [label, index] of [
  ["classification de zone", classifyIndex],
  ["refus de portail", portalGuardIndex],
  ["classification du format", formatIndex],
  ["contrôle Origin du formulaire", formOriginIndex],
  ["lecture bornée du corps", readPayloadIndex],
  ["création de session", createSessionIndex],
  ["contrôle du rôle", allowRoleIndex],
  ["révocation", revokeSessionIndex],
  ["pose du cookie", setCookieIndex],
  ["pose du CSRF", setCsrfIndex],
]) {
  assert.notEqual(index, -1, `${label} introuvable dans la route de login.`);
}
assert.ok(classifyIndex < portalGuardIndex, "La zone doit précéder son refus.");
assert.ok(portalGuardIndex < formatIndex, "Le portail doit être refusé avant le format.");
assert.ok(formatIndex < formOriginIndex, "Le format doit précéder le contrôle Origin.");
assert.ok(formOriginIndex < readPayloadIndex, "Origin doit précéder la lecture du corps.");
assert.ok(readPayloadIndex < createSessionIndex, "Le corps doit précéder l'authentification.");
assert.ok(createSessionIndex < allowRoleIndex, "Le rôle est contrôlé après authentification.");
assert.ok(allowRoleIndex < revokeSessionIndex, "Le refus doit déclencher la révocation.");
assert.ok(revokeSessionIndex < setCookieIndex, "La révocation doit précéder tout cookie.");
assert.ok(allowRoleIndex < setCookieIndex, "Le rôle doit être validé avant le cookie.");
assert.ok(allowRoleIndex < setCsrfIndex, "Le rôle doit être validé avant le CSRF.");

assert.match(logoutRoute, /export async function POST\(/);
assert.match(logoutRoute, /revokeInternalSession/);
assert.match(logoutRoute, /clearCsrfCookie/);
assert.match(logoutRoute, /expires:\s*new Date\(0\)/);

assert.match(meRoute, /export async function GET\(/);
assert.match(meRoute, /authenticated:\s*false/);
assert.match(meRoute, /authenticated:\s*true/);
assert.match(meRoute, /ensureCsrfCookie/);
assert.match(revokeOthersRoute, /export async function POST\(/);
assert.match(revokeOthersRoute, /revokeOtherInternalSessions/);
assert.doesNotMatch(
  revokeOthersRoute,
  /URLSearchParams|localStorage|sessionStorage/i,
);

assert.match(csrfHelper, /CSRF_COOKIE_NAME/);
assert.match(csrfHelper, /CSRF_HEADER_NAME/);
assert.match(csrfHelper, /X-CSRF-Token/);
assert.match(csrfServerHelper, /timingSafeEqual/);
assert.match(csrfServerHelper, /httpOnly:\s*false/);
assert.doesNotMatch(csrfServerHelper, /localStorage|sessionStorage/);

assert.match(sessionConfig, /process\.env\.SESSION_COOKIE_NAME/);
assert.match(sessionConfig, /process\.env\.SESSION_COOKIE_SECURE/);
assert.match(sessionConfig, /process\.env\.SESSION_COOKIE_SAME_SITE/);
assert.match(sessionConfig, /httpOnly:\s*true/);
assert.match(sessionConfig, /sameSite:\s*getSessionCookieSameSite\(\)/);
assert.match(sessionConfig, /return "lax" as const/);
assert.match(sessionConfig, /path:\s*"\/"/);
assert.doesNotMatch(sessionConfig, /\bdomain\s*:/i);
assert.doesNotMatch(sessionConfig, /NEXT_PUBLIC_|PUBLIC_INTERNAL_API_URL/);

assert.match(internalApi, /import "server-only"/);
assert.match(internalApi, /X-Portal-Session/);
assert.match(internalApi, /getInternalApiUrl/);
assert.doesNotMatch(internalApi, /NEXT_PUBLIC_INTERNAL_API_URL/);
assert.match(runtimeConfig, /process\.env\.INTERNAL_API_URL/);
assert.doesNotMatch(runtimeConfig, /NEXT_PUBLIC_INTERNAL_API_URL/);
assert.match(clientApi, /path:\s*`\/api\/\$\{string\}`/);
assert.match(clientApi, /AbortController/);
assert.match(clientApi, /CSRF_HEADER_NAME/);
assert.match(clientApi, /readCsrfTokenFromDocumentCookie/);
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
