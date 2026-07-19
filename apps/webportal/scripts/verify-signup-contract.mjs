import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

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
const adminSignupInitializePasswordRoute = await read(
  "app/api/admin/signups/[id]/initialize-password/route.ts",
);
const adminSignupResendPasswordEmailRoute = await read(
  "app/api/admin/signups/[id]/resend-password-email/route.ts",
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

const envExample = await read("../../.env.example");
const migration020 = await read(
  "../../apps/api-internal/Migrations/MariaDb/020_signup_pending.sql",
);
const migration034 = await read(
  "../../apps/api-internal/Migrations/MariaDb/034_v038_identity_alignment.sql",
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
const signupContracts = await read(
  "../../apps/api-internal/Contracts/SignupContracts.cs",
);
const emailTemplates = await read(
  "../../apps/api-internal/Services/Email/EmailTemplates.cs",
);
const programCs = await read("../../apps/api-internal/Program.cs");

const checks = [];
function check(name, fn) {
  checks.push([name, fn]);
}

check("migration cree la table signup_pending", () => {
  assert.match(migration020, /CREATE TABLE IF NOT EXISTS signup_pending/);
});
check("migration stocke uniquement des hash de jeton", () => {
  assert.match(migration020, /verification_token_hash CHAR\(64\)/);
  assert.match(migration020, /password_setup_token_hash CHAR\(64\)/);
  assert.doesNotMatch(migration020, /verification_token\s+VARCHAR/);
});
check("migration a une contrainte d'unicite email+statut", () => {
  assert.match(migration020, /UNIQUE KEY uk_signup_email_status \(email, status\)/);
});
check("migration v0.38 aligne signup et liens AD", () => {
  assert.match(migration034, /customer_type/);
  assert.match(migration034, /portal_user_id/);
  assert.match(migration034, /ad_provisioning_status/);
  assert.match(migration034, /koxo_export_status/);
});

check("SIGNUP_ENABLED defaut false", () => {
  assert.match(
    signupConfig,
    /ParseBool\(configuration\["SIGNUP_ENABLED"\], false\)/,
  );
});
check("SIGNUP_AUTO_APPROVE defaut false", () => {
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

check("jetons hashes en SHA-256", () => {
  assert.match(signupService, /SHA256\.HashData/);
});
check("token aleatoire 32 octets", () => {
  assert.match(signupService, /RandomNumberGenerator\.GetBytes\(32\)/);
});
check("non-leak : reponse identique via HasRecentSignupOrUserAsync", () => {
  assert.match(signupService, /HasRecentSignupOrUserAsync/);
  assert.match(signupService, /return Accepted\(\);/);
});
check("mot de passe : longueur minimale imposee", () => {
  assert.match(signupService, /MinPasswordLength\s*=\s*12/);
});
check("v0.38 normalise des donnees customer + primaryUser", () => {
  assert.match(signupService, /NormalizeSubmission/);
  assert.match(signupService, /SignupCustomerData/);
  assert.match(signupService, /SignupUserData/);
  assert.match(signupService, /BuildSamAccountNameBase/);
});
check("set-password branche la creation et la synchro AD", () => {
  assert.match(signupService, /ProvisionActiveDirectoryAsync/);
  assert.match(signupService, /SetUserPasswordAsync/);
  assert.match(signupService, /clients\.home\.bzh|_adConfiguration\.Domain/);
});
check("repository mock partage un store singleton", () => {
  assert.match(signupRepoMock, /class MockSignupStore/);
  assert.match(signupRepoInterface, /interface ISignupRepository/);
});
check("approbation cree customer + portal_user avant mot de passe", () => {
  assert.match(signupRepoMaria, /INSERT INTO customers/);
  assert.match(signupRepoMaria, /INSERT INTO portal_users/);
  assert.match(signupRepoMaria, /SET password_hash = @password_hash/);
});
check("contrat admin expose customer, primaryUser et etats AD", () => {
  assert.match(signupContracts, /SignupAdminAccountAccess/);
  assert.match(signupContracts, /AdProvisioningStatus/);
  assert.match(signupContracts, /KoxoExportStatus/);
  assert.match(signupContracts, /SignupCustomerData\? Customer/);
  assert.match(signupContracts, /SignupUserData\? PrimaryUser/);
});

check("endpoints publics signup presents", () => {
  assert.match(programCs, /"\/internal\/signup"/);
  assert.match(programCs, /"\/internal\/signup\/verify"/);
  assert.match(programCs, /"\/internal\/signup\/set-password"/);
});
check("endpoints admin signup presents", () => {
  assert.match(programCs, /"\/internal\/admin\/signups"/);
  assert.match(programCs, /"\/internal\/admin\/signups\/\{id\}\/approve"/);
  assert.match(programCs, /"\/internal\/admin\/signups\/\{id\}\/reject"/);
  assert.match(
    programCs,
    /"\/internal\/admin\/signups\/\{id\}\/initialize-password"/,
  );
  assert.match(
    programCs,
    /"\/internal\/admin\/signups\/\{id\}\/resend-password-email"/,
  );
});
check("audit trace a chaque etape", () => {
  assert.match(programCs, /"signup\.submit"/);
  assert.match(programCs, /"signup\.verify_success"/);
  assert.match(programCs, /"signup\.approved"/);
  assert.match(programCs, /"signup\.rejected"/);
  assert.match(programCs, /"signup\.password_initialized"/);
  assert.match(programCs, /"signup\.password_email_resent"/);
});
check("route profil change le mot de passe portail puis AD", () => {
  assert.match(programCs, /"\/internal\/profile\/password"/);
  assert.match(programCs, /FindUserLinkByPortalUserIdAsync/);
  assert.match(programCs, /UpdatePasswordHashAsync/);
  assert.match(programCs, /UpdateUserPasswordSyncStatusAsync/);
});

check("3 templates signup", () => {
  assert.match(emailTemplates, /signup_verification/);
  assert.match(emailTemplates, /account_approved/);
  assert.match(emailTemplates, /account_rejected/);
});

check("BFF signup verifie hCaptcha + honeypot + gate SIGNUP_ENABLED", () => {
  assert.match(signupRoute, /verifyHCaptcha/);
  assert.match(signupRoute, /isSignupEnabled\(\)/);
  assert.match(signupRoute, /website/);
  assert.match(signupRoute, /formRenderedAt/);
  assert.match(signupRoute, /checkRateLimit/);
});
check("BFF signup transporte la structure v0.38", () => {
  assert.match(signupRoute, /customerType/);
  assert.match(signupRoute, /addressLine1/);
  assert.match(signupRoute, /givenName/);
  assert.match(signupRoute, /customer:\s*\{/);
  assert.match(signupRoute, /primaryUser:\s*\{/);
});
check("BFF signup ignore les packs null ou vides", () => {
  assert.match(signupRoute, /hasProvidedPackValue\(body\.packKey\)/);
  assert.match(signupRoute, /hasProvidedPackValue\(body\.commitmentMonths\)/);
  assert.match(signupRoute, /hasProvidedPackValue\(body\.paymentMode\)/);
  assert.match(signupRoute, /value === null \|\| value === undefined/);
});
check("hCaptcha verifie cote serveur, fail-closed en production", () => {
  assert.match(signupServerLib, /hcaptcha\.com\/siteverify/);
  assert.match(signupServerLib, /CAPTCHA_MISCONFIGURED/);
  assert.match(signupServerLib, /HCAPTCHA_SECRET_KEY/);
});
check("BFF set-password relaie vers l'API interne", () => {
  assert.match(setPasswordRoute, /\/internal\/signup\/set-password/);
});
check("lien set-password valide au chargement (GET non destructif)", () => {
  assert.match(
    programCs,
    /app\.MapGet\(\s*"\/internal\/signup\/set-password\/validate"/,
  );
  assert.match(signupService, /ValidateSetPasswordTokenAsync/);
  assert.match(signupServerLib, /validateSetPasswordToken/);
  assert.match(signupServerLib, /set-password\/validate/);
  assert.match(setPasswordPage, /validateSetPasswordToken/);
  assert.match(setPasswordPage, /Definition impossible|Définition impossible/);
});
check("routes admin signup cablees", () => {
  assert.match(adminSignupsRoute, /handleAdminGet/);
  assert.match(adminSignupApproveRoute, /handleAdminMutation/);
  assert.match(adminSignupRejectRoute, /handleAdminMutation/);
  assert.match(adminSignupInitializePasswordRoute, /handleAdminMutation/);
  assert.match(adminSignupResendPasswordEmailRoute, /handleAdminMutation/);
  assert.match(internalApi, /getAdminSignups/);
});

check("routes signup publiques via PublicShell", () => {
  assert.match(publicRoutes, /"\/signup"/);
  assert.match(publicRoutes, /"\/set-password"/);
  assert.match(publicRoutes, /isSignupEnabled/);
});
check("formulaire signup garde honeypot + hCaptcha et champs structures", () => {
  assert.match(signupForm, /signup-honeypot/);
  assert.match(signupForm, /h-captcha/);
  assert.match(signupForm, /h-captcha-response/);
  assert.match(signupForm, /customerType/);
  assert.match(signupForm, /addressLine1/);
  assert.match(signupForm, /givenName/);
});
check("formulaire mot de passe impose la longueur + confirmation", () => {
  assert.match(setPasswordForm, /MIN_PASSWORD_LENGTH\s*=\s*12/);
  assert.match(setPasswordForm, /confirmPassword/);
});
check("actions admin approuver, refuser et relancer l'acces presentes", () => {
  assert.match(adminSignupActions, /approve/);
  assert.match(adminSignupActions, /reject/);
  assert.match(adminSignupActions, /initialize-password/);
  assert.match(adminSignupActions, /resend-password-email/);
});
check("lien admin 'Demandes d'inscription'", () => {
  assert.match(adminNavigation, /\/admin\/signups/);
});
check("pages signup\/verify\/set-password\/admin existent", () => {
  assert.ok(signupPage.length > 0);
  assert.ok(verifyPage.length > 0);
  assert.ok(setPasswordPage.length > 0);
  assert.ok(adminSignupsPage.length > 0);
  assert.ok(signupStatusLib.includes("localizeSignupStatus"));
});

check(".env.example documente les variables signup + hCaptcha", () => {
  assert.match(envExample, /SIGNUP_ENABLED=false/);
  assert.match(envExample, /SIGNUP_RATE_LIMIT_PER_IP_PER_HOUR=3/);
  assert.match(envExample, /SIGNUP_RATE_LIMIT_PER_EMAIL_PER_24H=1/);
  assert.match(envExample, /SIGNUP_AUTO_APPROVE=false/);
  assert.match(envExample, /HCAPTCHA_SITE_KEY=/);
  assert.match(envExample, /HCAPTCHA_SECRET_KEY=/);
});
check(".env.example cible clients.home.bzh", () => {
  assert.match(envExample, /AD_DOMAIN=clients\.home\.bzh/);
  assert.match(envExample, /AD_CLIENTS_OU_DN=OU=Clients,DC=clients,DC=home,DC=bzh/);
  assert.match(envExample, /AD_REQUIRED_OU_ROOT=DC=home,DC=bzh/);
  assert.match(envExample, /AD_ALLOWED_ROOTS=OU=Clients,DC=clients,DC=home,DC=bzh;OU=SecurityGroups,OU=Kermaria,DC=home,DC=bzh/);
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
  console.error(`\n${failures} verification(s) de contrat signup en echec.`);
  process.exit(1);
}

console.log(`\nContrat signup V0.38 valide (${checks.length} verifications).`);
