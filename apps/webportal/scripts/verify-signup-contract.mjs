import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

// Webportal artefacts
const publicRoutes = await read("lib/public-routes.ts");
const signupServerLib = await read("lib/signup-server.ts");
const signupStatusLib = await read("lib/signup-status.ts");
const signupRoute = await read("app/api/signup/route.ts");
const setPasswordRoute = await read("app/api/set-password/route.ts");
const adminSignupsRoute = await read("app/api/admin/signups/route.ts");
const adminSignupApproveRoute = await read(
  "app/api/admin/signups/[id]/approve/route.ts",
);
const adminSignupRejectRoute = await read(
  "app/api/admin/signups/[id]/reject/route.ts",
);
const signupForm = await read("components/SignupForm.tsx");
const setPasswordForm = await read("components/SetPasswordForm.tsx");
const adminSignupActions = await read("components/AdminSignupActions.tsx");
const adminNavigation = await read("components/AdminNavigation.tsx");
const signupPage = await read("app/signup/page.tsx");
const verifyPage = await read("app/signup/verify/page.tsx");
const setPasswordPage = await read("app/set-password/page.tsx");
const adminSignupsPage = await read("app/admin/signups/page.tsx");
const internalApi = await read("lib/internal-api.ts");

// Repo-level artefacts
const envExample = await read("../../.env.example");
const migration = await read(
  "../../apps/api-internal/Migrations/MariaDb/020_signup_pending.sql",
);
const signupConfig = await read(
  "../../apps/api-internal/Data/Configuration/SignupRuntimeConfiguration.cs",
);
const signupService = await read(
  "../../apps/api-internal/Services/SignupService.cs",
);
const signupRepoInterface = await read(
  "../../apps/api-internal/Data/Repositories/ISignupRepository.cs",
);
const signupRepoMaria = await read(
  "../../apps/api-internal/Data/Repositories/MariaDbSignupRepository.cs",
);
const signupRepoMock = await read(
  "../../apps/api-internal/Data/Repositories/MockSignupRepository.cs",
);
const emailTemplates = await read(
  "../../apps/api-internal/Services/Email/EmailTemplates.cs",
);
const programCs = await read("../../apps/api-internal/Program.cs");

const checks = [];
function check(name, fn) {
  checks.push([name, fn]);
}

// --- Migration ---
check("migration crée la table signup_pending", () => {
  assert.match(migration, /CREATE TABLE IF NOT EXISTS signup_pending/);
});
check("migration stocke uniquement des hash de jeton", () => {
  assert.match(migration, /verification_token_hash CHAR\(64\)/);
  assert.match(migration, /password_setup_token_hash CHAR\(64\)/);
  assert.doesNotMatch(migration, /verification_token\s+VARCHAR/);
});
check("migration a une contrainte d'unicité email+statut", () => {
  assert.match(migration, /UNIQUE KEY uk_signup_email_status \(email, status\)/);
});

// --- Configuration API ---
check("SIGNUP_ENABLED défaut false", () => {
  assert.match(
    signupConfig,
    /ParseBool\(configuration\["SIGNUP_ENABLED"\], false\)/,
  );
});
check("SIGNUP_AUTO_APPROVE défaut false", () => {
  assert.match(
    signupConfig,
    /ParseBool\(configuration\["SIGNUP_AUTO_APPROVE"\], false\)/,
  );
});
check("rate limits + TTL configurables", () => {
  assert.match(signupConfig, /SIGNUP_RATE_LIMIT_PER_IP_PER_HOUR/);
  assert.match(signupConfig, /SIGNUP_RATE_LIMIT_PER_EMAIL_PER_24H/);
  assert.match(signupConfig, /SIGNUP_VERIFICATION_TOKEN_TTL_HOURS/);
  assert.match(signupConfig, /SIGNUP_PASSWORD_SETUP_TOKEN_TTL_HOURS/);
});

// --- Service ---
check("jetons hashés en SHA-256", () => {
  assert.match(signupService, /SHA256\.HashData/);
});
check("token aléatoire 32 octets", () => {
  assert.match(signupService, /RandomNumberGenerator\.GetBytes\(32\)/);
});
check("non-leak : réponse identique via HasRecentSignupOrUserAsync", () => {
  assert.match(signupService, /HasRecentSignupOrUserAsync/);
  assert.match(signupService, /return Accepted\(\);/);
});
check("mot de passe : longueur minimale imposée", () => {
  assert.match(signupService, /MinPasswordLength\s*=\s*12/);
});
check("approbation crée un customer + portal_user sans mot de passe", () => {
  assert.match(signupRepoMaria, /INSERT INTO customers/);
  assert.match(signupRepoMaria, /INSERT INTO portal_users/);
  assert.match(signupRepoMaria, /password_hash,\s*display_name/);
  // portal_user créé avec NULL comme password_hash (activation via lien)
  assert.match(signupRepoMaria, /NULL, @displayName/);
});
check("repository mock partage un store singleton", () => {
  assert.match(signupRepoMock, /class MockSignupStore/);
  assert.match(signupRepoInterface, /interface ISignupRepository/);
});

// --- Endpoints API ---
check("endpoints publics signup présents", () => {
  assert.match(programCs, /"\/internal\/signup"/);
  assert.match(programCs, /"\/internal\/signup\/verify"/);
  assert.match(programCs, /"\/internal\/signup\/set-password"/);
});
check("endpoints admin signup présents", () => {
  assert.match(programCs, /"\/internal\/admin\/signups"/);
  assert.match(programCs, /"\/internal\/admin\/signups\/\{id\}\/approve"/);
  assert.match(programCs, /"\/internal\/admin\/signups\/\{id\}\/reject"/);
});
check("audit tracé à chaque étape", () => {
  assert.match(programCs, /"signup\.submit"/);
  assert.match(programCs, /"signup\.verify_success"/);
  assert.match(programCs, /"signup\.approved"/);
  assert.match(programCs, /"signup\.rejected"/);
});

// --- Emails ---
check("3 templates signup", () => {
  assert.match(emailTemplates, /signup_verification/);
  assert.match(emailTemplates, /account_approved/);
  assert.match(emailTemplates, /account_rejected/);
});

// --- BFF ---
check("BFF signup vérifie hCaptcha + honeypot + gate SIGNUP_ENABLED", () => {
  assert.match(signupRoute, /verifyHCaptcha/);
  assert.match(signupRoute, /isSignupEnabled\(\)/);
  assert.match(signupRoute, /website/);
  assert.match(signupRoute, /formRenderedAt/);
  assert.match(signupRoute, /checkRateLimit/);
});
check("hCaptcha vérifié côté serveur, fail-closed en production", () => {
  assert.match(signupServerLib, /hcaptcha\.com\/siteverify/);
  assert.match(signupServerLib, /CAPTCHA_MISCONFIGURED/);
  assert.match(signupServerLib, /HCAPTCHA_SECRET_KEY/);
});
check("BFF set-password relaie vers l'API interne", () => {
  assert.match(setPasswordRoute, /\/internal\/signup\/set-password/);
});
check("lien set-password validé au chargement (GET non destructif)", () => {
  // API : endpoint GET dédié, distinct du POST qui consomme le jeton
  assert.match(
    programCs,
    /app\.MapGet\(\s*"\/internal\/signup\/set-password\/validate"/,
  );
  assert.match(signupService, /ValidateSetPasswordTokenAsync/);
  // BFF : relais GET vers l'endpoint de validation
  assert.match(signupServerLib, /validateSetPasswordToken/);
  assert.match(signupServerLib, /set-password\/validate/);
  // Page : décide côté serveur d'afficher le formulaire ou l'erreur
  assert.match(setPasswordPage, /validateSetPasswordToken/);
  assert.match(setPasswordPage, /Définition impossible/);
});
check("routes admin signup câblées", () => {
  assert.match(adminSignupsRoute, /handleAdminGet/);
  assert.match(adminSignupApproveRoute, /handleAdminMutation/);
  assert.match(adminSignupRejectRoute, /handleAdminMutation/);
  assert.match(internalApi, /getAdminSignups/);
});

// --- UI ---
check("routes signup publiques via PublicShell", () => {
  assert.match(publicRoutes, /"\/signup"/);
  assert.match(publicRoutes, /"\/set-password"/);
  assert.match(publicRoutes, /isSignupEnabled/);
});
check("formulaire signup a honeypot + hCaptcha", () => {
  assert.match(signupForm, /signup-honeypot/);
  assert.match(signupForm, /h-captcha/);
  assert.match(signupForm, /h-captcha-response/);
});
check("formulaire mot de passe impose la longueur + confirmation", () => {
  assert.match(setPasswordForm, /MIN_PASSWORD_LENGTH\s*=\s*12/);
  assert.match(setPasswordForm, /confirmPassword/);
});
check("actions admin approuver/refuser présentes", () => {
  assert.match(adminSignupActions, /approve/);
  assert.match(adminSignupActions, /reject/);
});
check("lien admin 'Demandes d'inscription'", () => {
  assert.match(adminNavigation, /\/admin\/signups/);
});
check("pages signup/verify/set-password/admin existent", () => {
  assert.ok(signupPage.length > 0);
  assert.ok(verifyPage.length > 0);
  assert.ok(setPasswordPage.length > 0);
  assert.ok(adminSignupsPage.length > 0);
  assert.ok(signupStatusLib.includes("localizeSignupStatus"));
});

// --- .env.example ---
check(".env.example documente les variables signup + hCaptcha", () => {
  assert.match(envExample, /SIGNUP_ENABLED=false/);
  assert.match(envExample, /SIGNUP_RATE_LIMIT_PER_IP_PER_HOUR=3/);
  assert.match(envExample, /SIGNUP_RATE_LIMIT_PER_EMAIL_PER_24H=1/);
  assert.match(envExample, /SIGNUP_AUTO_APPROVE=false/);
  assert.match(envExample, /HCAPTCHA_SITE_KEY=/);
  assert.match(envExample, /HCAPTCHA_SECRET_KEY=/);
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
  console.error(`\n${failures} vérification(s) de contrat signup en échec.`);
  process.exit(1);
}

console.log(`\nContrat signup V0.26 validé (${checks.length} vérifications).`);
