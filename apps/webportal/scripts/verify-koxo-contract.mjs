import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const koxoRoute = await read("app/api/internal/koxo/users/route.ts");
const adminKoxoRoute = await read("app/api/admin/koxo/route.ts");
const adminKoxoValidateRoute = await read("app/api/admin/koxo/validate/route.ts");
const adminKoxoPage = await read("app/admin/koxo/page.tsx");
const adminNavigation = await read("components/AdminNavigation.tsx");
const adminValidationButton = await read(
  "components/AdminKoxoValidationButton.tsx",
);
const internalApi = await read("lib/internal-api.ts");
const runtimeConfig = await read("lib/runtime-config.ts");
const envExample = await read("../../.env.example");
const apiProgram = await read("../../apps/api-internal/Program.cs");
const koxoContracts = await read("../../apps/api-internal/Contracts/KoxoContracts.cs");
const koxoService = await read("../../apps/api-internal/Services/KoxoExportService.cs");
const koxoRepository = await read(
  "../../apps/api-internal/Data/Repositories/MariaDbKoxoRepository.cs",
);

const checks = [];
function check(name, fn) {
  checks.push([name, fn]);
}

check("route BFF KoXo privee existe et utilise un bearer token", () => {
  assert.match(koxoRoute, /Authorization: Bearer|authorization/);
  assert.match(koxoRoute, /readBearerToken/);
  assert.match(koxoRoute, /getKoxoExportApiToken/);
  assert.match(koxoRoute, /timingSafeEqual/);
  assert.match(koxoRoute, /KOXO_EXPORT_AUTH_REQUIRED/);
});

check("route BFF KoXo exige HTTPS et peut filtrer les IPs", () => {
  assert.match(koxoRoute, /shouldRequireKoxoExportHttps/);
  assert.match(koxoRoute, /getKoxoExportAllowedIps/);
  assert.match(koxoRoute, /x-forwarded-proto/);
  assert.match(koxoRoute, /x-forwarded-for/);
  assert.match(koxoRoute, /x-real-ip/);
  assert.match(koxoRoute, /KOXO_EXPORT_IP_FORBIDDEN/);
  assert.match(koxoRoute, /HTTPS_REQUIRED/);
});

check("route BFF KoXo relaie vers l'API interne sans session ni secret journalise", () => {
  assert.match(koxoRoute, /getInternalServiceHeaders/);
  assert.match(koxoRoute, /\/internal\/koxo\/users/);
  assert.match(koxoRoute, /X-Koxo-Source-Address/);
  assert.match(koxoRoute, /logBffFailure/);
  assert.doesNotMatch(koxoRoute, /requireAdminSession|cookies\(|csrf/i);
  assert.doesNotMatch(koxoRoute, /detail:\s*providedToken|detail:\s*expectedToken/);
});

check("configuration runtime expose les variables KoXo", () => {
  assert.match(runtimeConfig, /getKoxoExportApiToken/);
  assert.match(runtimeConfig, /getKoxoExportAllowedIps/);
  assert.match(runtimeConfig, /shouldRequireKoxoExportHttps/);
  assert.match(envExample, /KOXO_EXPORT_API_TOKEN=/);
  assert.match(envExample, /KOXO_EXPORT_ALLOWED_IPS=/);
  assert.match(envExample, /KOXO_EXPORT_REQUIRE_HTTPS=/);
});

check("api-internal expose les endpoints KoXo prives et admin", () => {
  assert.match(apiProgram, /"\/internal\/koxo\/users"/);
  assert.match(apiProgram, /"\/internal\/admin\/koxo"/);
  assert.match(apiProgram, /"\/internal\/admin\/koxo\/validate"/);
  assert.match(apiProgram, /KOXO_EXPORT_VALIDATION_FAILED/);
});

check("service KoXo impose un schema ferme et une validation bloquante", () => {
  assert.match(koxoContracts, /record KoxoExportUser/);
  assert.match(koxoContracts, /string Civilite/);
  assert.match(koxoContracts, /string Nom/);
  assert.match(koxoContracts, /string Prenom/);
  assert.match(koxoContracts, /string DateNaissance/);
  assert.match(koxoContracts, /string IdentifiantUnique/);
  assert.match(koxoContracts, /string GroupeSecondaire/);
  // Aiguillage vers le bon profil KoXo, donc vers le bon CSV : sans lui, un
  // fichier unique melangerait payants et demonstrations sous un seul modele.
  assert.match(koxoContracts, /string GroupePrimaire/);
  assert.match(koxoContracts, /string Email/);
  assert.match(koxoService, /SchemaVersion = 2/);
  assert.match(koxoService, /IdentifierPattern/);
  assert.match(koxoService, /KoxoValidationException/);
  assert.match(koxoService, /validation_failed/);
});

check("repository KoXo joint portal_users, customers et customer_ad_links", () => {
  assert.match(koxoRepository, /FROM portal_users/);
  assert.match(koxoRepository, /JOIN customers/);
  assert.match(koxoRepository, /JOIN customer_ad_links/);
  assert.match(koxoRepository, /koxo_export_runs/);
});

check("admin webportal expose un tableau de bord KoXo et une validation manuelle", () => {
  assert.match(internalApi, /getAdminKoxoDashboard/);
  assert.match(adminKoxoRoute, /\/internal\/admin\/koxo/);
  assert.match(adminKoxoValidateRoute, /\/internal\/admin\/koxo\/validate/);
  assert.match(adminKoxoPage, /Exportables/);
  assert.match(adminKoxoPage, /Invalides/);
  assert.match(adminKoxoPage, /Aperçu JSON|AperÃ§u JSON/);
  assert.match(adminKoxoPage, /Erreurs de validation/);
  assert.match(adminValidationButton, /Tester la validation/);
  assert.match(adminNavigation, /\/admin\/koxo/);
});

let failures = 0;
for (const [name, fn] of checks) {
  try {
    fn();
    console.log(`  ok   ${name}`);
  } catch (error) {
    failures += 1;
    console.error(`  FAIL ${name}`);
    console.error(`       ${error.message.split("\n")[0]}`);
  }
}

if (failures > 0) {
  console.error(`\n${failures} verification(s) de contrat KoXo en echec.`);
  process.exit(1);
}

console.log(`\nContrat KoXo V0.40 valide (${checks.length} verifications).`);
