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
  return import(`data:text/javascript;base64,${Buffer.from(transpiled.outputText).toString("base64")}`);
}

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

const publicRouteConfig = await read("lib/public-route-config.ts");
const publicRoutes = await read("lib/public-routes.ts");
const servicesMode = await read("lib/services-portal-mode.ts");
const servicesPage = await read("app/services/page.tsx");
const appShell = await read("components/AppShell.tsx");
const publicShell = await read("components/PublicShell.tsx");
const layout = await read("app/layout.tsx");

const routing = await importPureTypeScript(
  publicRouteConfig,
  "public-route-config.ts",
);
const modes = await importPureTypeScript(
  servicesMode,
  "services-portal-mode.ts",
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
const publicRoutesRuntime = await importPureTypeScript(
  executablePublicRoutes,
  "public-routes.ts",
);

assert.equal(modes.resolveServicesPortalMode("local", null), "public");
assert.equal(
  modes.resolveServicesPortalMode("local", "client_user"),
  "client",
);
assert.equal(
  modes.resolveServicesPortalMode("local", "internal_admin"),
  "admin",
);

assert.equal(
  routing.resolvePortalAreaUrl("http://localhost:3000", "public", "/tarifs"),
  "http://localhost:3000/tarifs",
);
assert.equal(
  routing.resolvePortalAreaUrl("http://localhost:3000", "client", "/services"),
  "http://localhost:3000/services",
);
assert.equal(
  routing.resolvePortalAreaUrl("http://localhost:3000", "admin", "/admin/vps"),
  "http://localhost:3000/admin/vps",
);

const previousPortalUrl = process.env.PUBLIC_PORTAL_URL;
process.env.PUBLIC_PORTAL_URL = "https://dashboard.zachary-it.fr";
try {
  for (const [host, expected] of [
    ["localhost:3000", "http://localhost:3000"],
    ["127.0.0.1:3100", "http://127.0.0.1:3100"],
    ["[::1]:3200", "http://[::1]:3200"],
  ]) {
    assert.equal(
      publicRoutesRuntime.getPortalPublicUrl({
        headers: createHeaders({ host }),
        nextUrl: { origin: "https://dashboard.zachary-it.fr" },
      }),
      expected,
      `${host} doit prévaloir sur PUBLIC_PORTAL_URL`,
    );
  }
} finally {
  if (previousPortalUrl === undefined) {
    delete process.env.PUBLIC_PORTAL_URL;
  } else {
    process.env.PUBLIC_PORTAL_URL = previousPortalUrl;
  }
}

assert.match(servicesPage, /await getCurrentPortalSession\(\)/);
assert.match(servicesPage, /resolveServicesPortalMode\(/);
assert.match(servicesPage, /if \(portalMode === "public"\)/);
assert.match(servicesPage, /if \(portalMode === "admin"\)[\s\S]*redirect\("\/admin"\)/);
assert.match(servicesPage, /await requireClientSession\(\)/);
assert.match(appShell, /CLIENT_VPS_DETAIL_PATH/);
assert.match(appShell, /isLocalClientServicesRoute/);
assert.match(appShell, /session\?\.user\.role === "client_user"/);
assert.match(appShell, /<PublicShell signupEnabled=\{signupEnabled\}/);
assert.match(publicShell, /const publicHref = \(pathname: string\) => pathname/);
assert.doesNotMatch(publicShell, /PUBLIC_SITE_URL/);
assert.match(publicShell, /href="\/login"/);
assert.match(layout, /<AppShell signupEnabled=\{signupEnabled\}>/);

console.log("Contrat de navigation locale vérifié.");
