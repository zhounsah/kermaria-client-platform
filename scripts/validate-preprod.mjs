import { spawnSync } from "node:child_process";
import { readFile } from "node:fs/promises";
import path from "node:path";

const failures = [];
const warnings = [];

const requiredVariables = [
  "NODE_ENV",
  "ASPNETCORE_ENVIRONMENT",
  "DOTNET_ENVIRONMENT",
  "INTERNAL_API_URL",
  "ALLOW_LOCAL_INTERNAL_API_URL",
  "SERVICE_AUTH_TOKEN",
  "SESSION_COOKIE_NAME",
  "SESSION_COOKIE_SECURE",
  "SQL_PROVIDER",
  "SQL_HOST",
  "SQL_PORT",
  "SQL_DATABASE",
  "SQL_USERNAME",
  "SQL_PASSWORD",
  "AD_INTEGRATION_MODE",
  "LOG_LEVEL",
  "SESSION_DURATION_MINUTES",
  "LOGIN_MAX_FAILURES",
  "LOGIN_LOCKOUT_MINUTES",
];

const demoVariables = [
  "DEMO_PORTAL_EMAIL",
  "DEMO_PORTAL_PASSWORD",
  "DEMO_INTERNAL_ADMIN_EMAIL",
  "DEMO_INTERNAL_ADMIN_PASSWORD",
];

for (const variableName of requiredVariables) {
  if (!process.env[variableName]?.trim()) {
    failures.push(`Variable d'environnement manquante: ${variableName}.`);
  }
}

if (!equalsIgnoreCase(process.env.NODE_ENV, "production")) {
  failures.push("NODE_ENV doit valoir production.");
}

if (!equalsIgnoreCase(process.env.ASPNETCORE_ENVIRONMENT, "Production")) {
  failures.push("ASPNETCORE_ENVIRONMENT doit valoir Production.");
}

if (!equalsIgnoreCase(process.env.DOTNET_ENVIRONMENT, "Production")) {
  failures.push("DOTNET_ENVIRONMENT doit valoir Production.");
}

if (
  process.env.ASPNETCORE_ENVIRONMENT?.trim()
  && process.env.DOTNET_ENVIRONMENT?.trim()
  && !equalsIgnoreCase(
    process.env.ASPNETCORE_ENVIRONMENT,
    process.env.DOTNET_ENVIRONMENT,
  )
) {
  failures.push(
    "ASPNETCORE_ENVIRONMENT et DOTNET_ENVIRONMENT doivent rester coherents.",
  );
}

if (!equalsIgnoreCase(process.env.SQL_PROVIDER, "mariadb")) {
  failures.push("SQL_PROVIDER doit valoir mariadb en preproduction V0.16.");
}

if (!equalsIgnoreCase(process.env.AD_INTEGRATION_MODE, "disabled")) {
  failures.push("AD_INTEGRATION_MODE doit rester disabled.");
}

if (!equalsIgnoreCase(process.env.SESSION_COOKIE_SECURE, "true")) {
  failures.push("SESSION_COOKIE_SECURE doit valoir true en preproduction.");
}

for (const variableName of ["SERVICE_AUTH_TOKEN", "SQL_PASSWORD"]) {
  if (isPlaceholderSecret(process.env[variableName])) {
    failures.push(`Secret invalide ou placeholder detecte: ${variableName}.`);
  }
}

for (const variableName of demoVariables) {
  if (process.env[variableName]?.trim()) {
    failures.push(
      `La variable ${variableName} doit rester absente en preproduction.`,
    );
  }
}

validateInternalApiUrl();
await validateTrackedFiles();
await validateSourceContracts();
runSecretCheck();

if (warnings.length > 0) {
  process.stdout.write("Avertissements validate:preprod:\n");
  for (const warning of warnings) {
    process.stdout.write(`- ${warning}\n`);
  }
}

if (failures.length > 0) {
  process.stderr.write("Validation preproduction V0.16 en echec:\n");
  for (const failure of failures) {
    process.stderr.write(`- ${failure}\n`);
  }
  process.exit(1);
}

process.stdout.write(
  "Validation preproduction V0.16 reussie: variables, garde-fous BFF/API et scan de secrets sont coherents.\n",
);

function validateInternalApiUrl() {
  const configuredUrl = process.env.INTERNAL_API_URL?.trim();
  if (!configuredUrl) {
    return;
  }

  let parsedUrl;
  try {
    parsedUrl = new URL(configuredUrl);
  } catch {
    failures.push("INTERNAL_API_URL doit etre une URL http(s) valide.");
    return;
  }

  if (!["http:", "https:"].includes(parsedUrl.protocol)) {
    failures.push("INTERNAL_API_URL doit utiliser http ou https.");
  }

  const isLocalHost = ["localhost", "127.0.0.1", "::1"].includes(
    parsedUrl.hostname,
  );
  const allowLocal =
    process.env.ALLOW_LOCAL_INTERNAL_API_URL?.trim().toLowerCase() === "true";

  if (isLocalHost && !allowLocal) {
    failures.push(
      "INTERNAL_API_URL locale exige ALLOW_LOCAL_INTERNAL_API_URL=true.",
    );
  }

  if (!isLocalHost && allowLocal) {
    warnings.push(
      "ALLOW_LOCAL_INTERNAL_API_URL=true est defini alors que INTERNAL_API_URL n'est pas locale.",
    );
  }
}

async function validateTrackedFiles() {
  const trackedFiles = getTrackedFiles();
  const browserSourceFiles = trackedFiles.filter(
    (filePath) =>
      /^apps\/webportal\/(?:app|components|lib)\//.test(filePath)
      && /\.(?:ts|tsx)$/i.test(filePath),
  );

  for (const filePath of browserSourceFiles) {
    const source = await readFile(filePath, "utf8");

    if (
      /NEXT_PUBLIC_INTERNAL_API_URL|PUBLIC_INTERNAL_API_URL|NEXT_PUBLIC_SERVICE_AUTH_TOKEN|PUBLIC_SERVICE_AUTH_TOKEN/.test(
        source,
      )
    ) {
      failures.push(
        `Variable interne exposee au navigateur detectee dans ${filePath}.`,
      );
    }

    if (/localStorage|sessionStorage/.test(source)) {
      failures.push(
        `Stockage navigateur interdit detecte dans ${filePath}.`,
      );
    }
  }
}

async function validateSourceContracts() {
  const runtimeConfig = await readFile(
    path.join("apps", "webportal", "lib", "runtime-config.ts"),
    "utf8",
  );
  const internalApi = await readFile(
    path.join("apps", "webportal", "lib", "internal-api.ts"),
    "utf8",
  );
  const readyRoute = await readFile(
    path.join("apps", "webportal", "app", "api", "health", "ready", "route.ts"),
    "utf8",
  );
  const apiProgram = await readFile(
    path.join("apps", "api-internal", "Program.cs"),
    "utf8",
  );

  if (!runtimeConfig.includes('import "server-only"')) {
    failures.push("apps/webportal/lib/runtime-config.ts doit rester server-only.");
  }

  if (!internalApi.includes('import "server-only"')) {
    failures.push("apps/webportal/lib/internal-api.ts doit rester server-only.");
  }

  if (!readyRoute.includes("validateServerRuntimeConfiguration")) {
    failures.push(
      "La readiness WEBPORTAL doit continuer a valider la configuration serveur.",
    );
  }

  if (!readyRoute.includes("checkInternalApiReadiness")) {
    failures.push(
      "La readiness WEBPORTAL doit continuer a verifier API-INTERNAL.",
    );
  }

  if (!/app\.MapGet\(\s*"\/health"/.test(apiProgram)) {
    failures.push("API-INTERNAL doit exposer /health.");
  }

  if (!/app\.MapGet\(\s*"\/ready"/.test(apiProgram)) {
    failures.push("API-INTERNAL doit exposer /ready.");
  }

  if (!/app\.MapGet\(\s*"\/health\/ready"/.test(apiProgram)) {
    failures.push("API-INTERNAL doit conserver /health/ready.");
  }
}

function runSecretCheck() {
  const result = spawnSync(process.execPath, ["scripts/check-secrets.mjs"], {
    encoding: "utf8",
    stdio: "pipe",
  });

  if (result.status === 0) {
    return;
  }

  if (result.stdout?.trim()) {
    warnings.push(result.stdout.trim());
  }

  failures.push(
    result.stderr?.trim()
      || "Le garde-fou secrets a signale un probleme.",
  );
}

function getTrackedFiles() {
  const result = spawnSync("git", ["ls-files"], {
    encoding: "utf8",
    stdio: "pipe",
  });

  if (result.status !== 0) {
    failures.push("Impossible de lister les fichiers suivis avec git ls-files.");
    return [];
  }

  return result.stdout
    .split(/\r?\n/u)
    .map((filePath) => filePath.trim())
    .filter(Boolean);
}

function equalsIgnoreCase(left, right) {
  return left?.trim().toLowerCase() === right.toLowerCase();
}

function isPlaceholderSecret(value) {
  if (!value?.trim()) {
    return true;
  }

  const normalized = value.trim().toLowerCase();
  return (
    normalized === "password"
    || normalized === "changeme"
    || normalized === "change-me"
    || normalized === "test"
    || normalized === "dev-local-token"
    || normalized.startsWith("test")
    || normalized.includes("replace_with")
    || normalized.includes("replace-with")
    || normalized.includes("example")
    || normalized.includes("placeholder")
  );
}
