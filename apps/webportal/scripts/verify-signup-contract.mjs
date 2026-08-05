import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const publicRoutes = await read("lib/public-routes.ts");
const publicRouteConfig = await read("lib/public-route-config.ts");
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
const packSelectionSummary = await read(
  "components/PublicPackSelectionSummary.tsx",
);
const setPasswordForm = await read("components/SetPasswordForm.tsx");
const adminSignupActions = await read("components/AdminSignupActions.tsx");
const adminNavigation = await read("components/AdminNavigation.tsx");
const signupPage = await read("app/signup/page.tsx");
const verifyPage = await read("app/signup/verify/page.tsx");
const setPasswordPage = await read("app/set-password/page.tsx");
const passwordPage = await read("app/password/page.tsx");
const adminSignupsPage = await read("app/admin/signups/page.tsx");
const adminSignupDetailPage = await read("app/admin/signups/[id]/page.tsx");
const internalApi = await read("lib/internal-api.ts");

const envExample = await read("../../.env.example");
const migration020 = await read(
  "../../apps/api-internal/Migrations/MariaDb/020_signup_pending.sql",
);
const migration034 = await read(
  "../../apps/api-internal/Migrations/MariaDb/034_v038_identity_alignment.sql",
);
const migration035 = await read(
  "../../apps/api-internal/Migrations/MariaDb/035_v040_koxo_sync.sql",
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
check("migration v0.40 ajoute birth_date et l'identifiant KoXo immuable", () => {
  assert.match(migration035, /signup_pending[\s\S]*birth_date DATE NULL/);
  assert.match(migration035, /portal_users[\s\S]*birth_date DATE NULL/);
  assert.match(migration035, /koxo_unique_identifier VARCHAR\(32\) NULL/);
  assert.match(
    migration035,
    /CREATE UNIQUE INDEX IF NOT EXISTS uk_portal_users_koxo_unique_identifier/,
  );
  assert.match(migration035, /CREATE TABLE IF NOT EXISTS koxo_identifier_counters/);
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
check("v0.40 impose civilite exportable et date de naissance", () => {
  assert.match(signupService, /AllowedPersonalTitles/);
  assert.match(signupService, /NormalizePersonalTitle/);
  assert.match(signupService, /NormalizeBirthDate/);
  assert.match(signupService, /DateOnly\.TryParseExact/);
  assert.match(signupContracts, /string\?\s+BirthDate/);
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
  assert.match(signupRoute, /birthDate/);
  assert.match(signupRoute, /customer:\s*\{/);
  assert.match(signupRoute, /primaryUser:\s*\{/);
});
check("BFF signup restreint la civilite et exige la date de naissance", () => {
  assert.match(signupRoute, /allowedPersonalTitles/);
  assert.match(signupRoute, /birthDatePattern/);
  assert.match(signupRoute, /errors\.personalTitle = "La civilite est requise\."/);
  assert.match(signupRoute, /Selectionnez une civilite exportable\./);
  assert.match(signupRoute, /errors\.birthDate = "La date de naissance est requise\."/);
  assert.match(signupRoute, /La date de naissance est invalide\./);
});
check("BFF signup ignore les packs null ou vides", () => {
  assert.match(signupRoute, /hasProvidedPackValue\(body\.packKey\)/);
  assert.match(signupRoute, /hasProvidedPackValue\(body\.commitmentMonths\)/);
  assert.match(signupRoute, /hasProvidedPackValue\(body\.paymentMode\)/);
  assert.match(signupRoute, /value === null \|\| value === undefined/);
});
check("BFF signup valide puis fige la selection publique", () => {
  assert.match(signupRoute, /resolvePackSelectionInput\(\{/);
  assert.match(signupRoute, /packKey:\s*body\.packKey/);
  assert.match(signupRoute, /commitmentMonths:\s*body\.commitmentMonths/);
  assert.match(signupRoute, /paymentMode:\s*body\.paymentMode/);
  assert.match(signupRoute, /getPublicCommercialCatalog\(\)/);
  assert.match(signupRoute, /getPublicPackCatalogContent\(\)/);
  assert.match(signupRoute, /buildSignupPackSnapshot\(/);
  assert.match(signupRoute, /code:\s*"INVALID_PACK_SELECTION"/);
  assert.match(signupRoute, /code:\s*"PACK_SELECTION_UNAVAILABLE"/);
  assert.match(signupRoute, /packSelection,/);

  const resolveIndex = signupRoute.indexOf("resolvePackSelectionInput({");
  const catalogIndex = signupRoute.indexOf("getPublicCommercialCatalog()");
  const snapshotIndex = signupRoute.indexOf("buildSignupPackSnapshot(");
  const upstreamIndex = signupRoute.indexOf("const result = await callInternalSignup");
  for (const [label, index] of [
    ["validation de selection", resolveIndex],
    ["chargement du catalogue", catalogIndex],
    ["creation du snapshot", snapshotIndex],
    ["appel signup interne", upstreamIndex],
  ]) {
    assert.notEqual(index, -1, `${label} introuvable dans la route signup.`);
  }
  assert.ok(resolveIndex < catalogIndex);
  assert.ok(catalogIndex < snapshotIndex);
  assert.ok(snapshotIndex < upstreamIndex);
});
check("hCaptcha verifie cote serveur, fail-closed en production", () => {
  assert.match(signupServerLib, /hcaptcha\.com\/siteverify/);
  assert.match(signupServerLib, /CAPTCHA_MISCONFIGURED/);
  assert.match(signupServerLib, /HCAPTCHA_SECRET_KEY/);
});
check("BFF set-password relaie vers l'API interne", () => {
  assert.match(setPasswordRoute, /\/internal\/signup\/set-password/);
  assert.match(setPasswordRoute, /NextResponse\.json/);
});
check("BFF set-password conserve JSON et accepte le formulaire natif borne", () => {
  assert.match(setPasswordRoute, /application\/json/);
  assert.match(setPasswordRoute, /application\/x-www-form-urlencoded/);
  assert.match(setPasswordRoute, /JSON\.parse\(body\)/);
  assert.match(setPasswordRoute, /new URLSearchParams\(body\)/);
  assert.match(setPasswordRoute, /form\.getAll\("token"\)/);
  assert.match(setPasswordRoute, /form\.getAll\("password"\)/);
  assert.match(setPasswordRoute, /form\.getAll\("confirmPassword"\)/);
  assert.match(setPasswordRoute, /MAX_SET_PASSWORD_BODY_BYTES\s*=\s*16\s*\*\s*1024/);
  assert.match(setPasswordRoute, /request\.body\.getReader\(\)/);
  assert.match(
    setPasswordRoute,
    /new TextDecoder\("utf-8", \{ fatal: true \}\)/,
  );
  assert.match(setPasswordRoute, /\b413\b/);
  assert.match(setPasswordRoute, /\b415\b/);
  assert.match(
    setPasswordRoute,
    /result\.ok \? 200 : result\.status >= 500 \? 502 : result\.status/,
  );
  assert.match(setPasswordRoute, /\{ token, password \}/);
});
check("BFF set-password protege le POST natif et ses redirections", () => {
  assert.match(setPasswordRoute, /getPortalRequestOriginFromHeaders/);
  assert.match(setPasswordRoute, /getPortalArea\(origin\)/);
  assert.match(
    setPasswordRoute,
    /area !== "public"\s*&&\s*area !== "client"\s*&&\s*area !== "local"/,
  );
  for (const canonicalClientHost of [
    "dashboard.zacharyhounsa.ovh",
    "dashboard.home.bzh",
  ]) {
    assert.match(
      publicRouteConfig,
      new RegExp(`client:\\s*"${canonicalClientHost.replaceAll(".", "\\.")}"`),
    );
  }
  assert.match(setPasswordRoute, /request\.headers\.get\("origin"\)/);
  assert.match(setPasswordRoute, /url\.origin === origin/);
  assert.match(setPasswordRoute, /status:\s*303/);
  assert.match(
    setPasswordRoute,
    /Location:\s*`\/set-password\?result=\$\{code\}`/,
  );
  assert.match(setPasswordRoute, /"Cache-Control", "no-store"/);
  assert.doesNotMatch(setPasswordRoute, /request\.formData\(|multipart\/form-data/i);
  assert.doesNotMatch(
    setPasswordRoute,
    /[?&](?:token|password|confirmPassword|correlation_id)=/i,
  );
  assert.doesNotMatch(
    setPasswordRoute,
    /\.cookies\.set\(|["']Set-Cookie["']/i,
  );
  assert.match(
    setPasswordRoute,
    /if\s*\(ok\)\s*\{\s*return code === "PASSWORD_SET"\s*\?\s*"PASSWORD_SET"\s*:\s*"SET_PASSWORD_UNAVAILABLE";\s*\}/s,
  );
  assert.doesNotMatch(
    setPasswordRoute,
    /if\s*\(ok\)\s*\{\s*return "PASSWORD_SET"/s,
  );

  for (const resultCode of [
    "PASSWORD_SET",
    "TOKEN_INVALID",
    "TOKEN_EXPIRED",
    "INVALID_PASSWORD",
    "INVALID_REQUEST",
    "RATE_LIMITED",
    "SET_PASSWORD_REQUEST_TOO_LARGE",
    "SET_PASSWORD_UNAVAILABLE",
  ]) {
    assert.match(setPasswordRoute, new RegExp(resultCode));
  }

  const formatIndex = setPasswordRoute.indexOf(
    "const format = getSetPasswordRequestFormat",
  );
  const originIndex = setPasswordRoute.indexOf(
    'format === "form" && !isAllowedFormPost',
  );
  const rateLimitIndex = setPasswordRoute.indexOf(
    "const rateDecision = checkRateLimit",
  );
  const bodyIndex = setPasswordRoute.indexOf(
    "await readBoundedSetPasswordBody(request)",
  );
  const upstreamIndex = setPasswordRoute.indexOf(
    "const result = await callInternalSignup",
  );
  for (const [label, index] of [
    ["classification du format", formatIndex],
    ["controle Origin", originIndex],
    ["rate-limit", rateLimitIndex],
    ["lecture bornee", bodyIndex],
    ["appel upstream", upstreamIndex],
  ]) {
    assert.notEqual(index, -1, `${label} introuvable dans la route set-password.`);
  }
  assert.ok(formatIndex < originIndex);
  assert.ok(originIndex < rateLimitIndex);
  assert.ok(rateLimitIndex < bodyIndex);
  assert.ok(bodyIndex < upstreamIndex);
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
  assert.match(publicRouteConfig, /"\/signup"/);
  assert.match(publicRouteConfig, /"\/set-password"/);
  assert.match(publicRoutes, /PUBLIC_ROUTES/);
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
check("formulaire signup V0.40 impose une civilite exportable et birthDate", () => {
  assert.match(signupForm, /name="personalTitle"/);
  assert.match(signupForm, /value="madame"/);
  assert.match(signupForm, /value="monsieur"/);
  assert.doesNotMatch(signupForm, /value="autre"/);
  assert.match(signupForm, /name="birthDate"/);
  assert.match(signupForm, /type="date"/);
  assert.match(signupForm, /required/);
});
check("les vues admin signup affichent la date de naissance", () => {
  assert.match(adminSignupsPage, /Demandes d'inscription|Demandes d’inscription/);
  assert.match(internalApi, /birthDate:\s*string \| null/);
  assert.match(adminSignupDetailPage, /Date de naissance/);
});
check("page signup reprend uniquement un snapshot catalogue valide", () => {
  assert.match(signupPage, /selectionFromSearchParams\(await searchParams\)/);
  assert.match(signupPage, /getPublicCommercialCatalog\(\)/);
  assert.match(signupPage, /getPublicPackCatalogContent\(\)/);
  assert.match(signupPage, /buildSignupPackSnapshot\(/);
  assert.match(signupPage, /<PublicPackSelectionSummary/);
  assert.match(signupPage, /initialPackSelection=\{packSelection/);
  for (const field of [
    "packKey",
    "packLabel",
    "commitmentMonths",
    "paymentMode",
    "monthlyPriceAmountCents",
    "setupFeeAmountCents",
    "firstChargeAmountCents",
  ]) {
    assert.match(
      signupPage,
      new RegExp(`${field}:\\s*packSelection\\.${field}`),
      `Le snapshot signup doit transmettre ${field}.`,
    );
  }
  assert.doesNotMatch(
    signupPage,
    /method:\s*["'](?:POST|PUT|PATCH|DELETE)["']|requestBffJson|callInternalSignup/,
    "Le rendu GET de signup ne doit effectuer aucune mutation.",
  );
});
check("formulaire signup transporte et affiche le snapshot sans le recalculer", () => {
  for (const field of ["packKey", "commitmentMonths", "paymentMode"]) {
    assert.match(
      signupForm,
      new RegExp(`${field}:\\s*initialPackSelection\\?\\.${field} \\?\\? null`),
      `Le POST signup doit transporter ${field}.`,
    );
    assert.match(
      signupForm,
      new RegExp(`name=["']${field}["']`),
      `Le fallback natif doit conserver ${field}.`,
    );
  }
  assert.match(signupForm, /<PublicPackSelectionSummary/);
  assert.match(signupForm, /initialPackSelection\.packLabel/);
  assert.doesNotMatch(
    signupForm,
    /normalizeCommitmentMonths|normalizePaymentMode|resolvePackSelectionInput/,
    "Le formulaire ne doit pas reinterpretter un snapshot valide cote serveur.",
  );
  assert.match(packSelectionSummary, /aria-label=\{`[^`]*\$\{packLabel\}`\}/);
  assert.match(packSelectionSummary, /<dt>Engagement<\/dt>/);
  assert.match(packSelectionSummary, /<dt>Paiement<\/dt>/);
  assert.match(packSelectionSummary, /<dt>Tarif affich/u);
});
check("formulaire mot de passe impose la longueur + confirmation", () => {
  assert.match(setPasswordForm, /MIN_PASSWORD_LENGTH\s*=\s*12/);
  assert.match(setPasswordForm, /MAX_PASSWORD_LENGTH\s*=\s*200/);
  assert.match(setPasswordForm, /confirmPassword/);
  assert.match(setPasswordForm, /name="token"/);
  assert.match(setPasswordForm, /type="hidden"/);
  assert.match(setPasswordForm, /acceptCharset="UTF-8"/);
  assert.match(
    setPasswordForm,
    /encType="application\/x-www-form-urlencoded"/,
  );
  assert.match(setPasswordForm, /action="\/api\/set-password"/);
  assert.match(setPasswordForm, /method="post"/);
  assert.match(setPasswordForm, /event\.preventDefault\(\)/);
  assert.match(setPasswordForm, /requestBffJson<SetPasswordResponse>/);
  assert.match(setPasswordForm, /"Content-Type": "application\/json"/);
  assert.match(setPasswordForm, /JSON\.stringify\(\{ token, password \}\)/);
  assert.doesNotMatch(setPasswordForm, /FormData|URLSearchParams/i);
});
check("page set-password presente uniquement des resultats natifs finis", () => {
  for (const resultCode of [
    "PASSWORD_SET",
    "TOKEN_INVALID",
    "TOKEN_EXPIRED",
    "INVALID_PASSWORD",
    "INVALID_REQUEST",
    "RATE_LIMITED",
    "SET_PASSWORD_REQUEST_TOO_LARGE",
    "SET_PASSWORD_UNAVAILABLE",
  ]) {
    assert.match(setPasswordPage, new RegExp(resultCode));
  }
  assert.match(
    setPasswordPage,
    /Object\.hasOwn\(SET_PASSWORD_RESULTS, resultCode\)/,
  );
  assert.match(setPasswordPage, /<Link href="\/login">/);
  assert.doesNotMatch(setPasswordPage, /dangerouslySetInnerHTML/);

  const presentationIndex = setPasswordPage.indexOf("if (presentation)");
  const tokenValidationIndex = setPasswordPage.indexOf(
    "validateSetPasswordToken(trimmedToken",
  );
  assert.notEqual(presentationIndex, -1);
  assert.notEqual(tokenValidationIndex, -1);
  assert.ok(
    presentationIndex < tokenValidationIndex,
    "Un resultat POST reconnu doit etre presente avant toute validation du token.",
  );
});
check("page password reste separee du fallback set-password", () => {
  assert.match(passwordPage, /await requireClientSession\(\)/);
  // Le drapeau AD_PASSWORD_CHANGE_ENABLED est lu via runtime-config, partage
  // avec la page profil qui annonce ou non le parcours.
  assert.match(passwordPage, /isPasswordChangeEnabled\(\)/);
  assert.match(passwordPage, /<PasswordChangeForm \/>/);
  assert.doesNotMatch(passwordPage, /action="\/api\/set-password"/);
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
check(".env.example borne explicitement le perimetre AD", () => {
  function readEnvValue(name) {
    const match = envExample.match(new RegExp(`^${name}=([^\\r\\n]+)$`, "m"));
    assert.ok(match, `${name} doit etre renseignee.`);
    return match[1].trim();
  }

  function assertOuUnderRoot(value, requiredRoot, label) {
    const suffix = `,${requiredRoot}`;
    assert.ok(
      value.toLowerCase().endsWith(suffix.toLowerCase()),
      `${label} doit etre sous AD_REQUIRED_OU_ROOT.`,
    );
    const ouPrefix = value.slice(0, -suffix.length);
    assert.match(
      ouPrefix,
      /^OU=[^,=]+(?:,OU=[^,=]+)*$/i,
      `${label} doit etre une suite de composantes OU non vides.`,
    );
  }

  const domain = readEnvValue("AD_DOMAIN");
  const requiredRoot = readEnvValue("AD_REQUIRED_OU_ROOT");
  const clientsOu = readEnvValue("AD_CLIENTS_OU_DN");
  const allowedRoots = readEnvValue("AD_ALLOWED_ROOTS")
    .split(";")
    .map((value) => value.trim());

  assert.ok(domain.length <= 253, "AD_DOMAIN depasse la longueur DNS maximale.");
  const domainLabels = domain.split(".");
  assert.ok(domainLabels.length >= 2, "AD_DOMAIN doit etre un domaine DNS qualifie.");
  for (const label of domainLabels) {
    assert.match(
      label,
      /^(?=.{1,63}$)[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$/i,
      `Label DNS invalide dans AD_DOMAIN: ${label}`,
    );
  }
  const domainDn = domainLabels.map((label) => `DC=${label}`).join(",");

  const rootComponents = requiredRoot.split(",");
  assert.ok(rootComponents.length >= 2, "AD_REQUIRED_OU_ROOT doit etre un DN DC qualifie.");
  const rootLabels = rootComponents.map((component) => {
    const match = component.match(/^DC=([a-z0-9](?:[a-z0-9-]*[a-z0-9])?)$/i);
    assert.ok(match, `Composante DC invalide: ${component}`);
    return match[1];
  });
  const rootDomain = rootLabels.join(".");
  assert.ok(
    domain.toLowerCase() === rootDomain.toLowerCase()
      || domain.toLowerCase().endsWith(`.${rootDomain.toLowerCase()}`),
    `${domainDn} doit etre egal ou enfant de ${requiredRoot}.`,
  );

  assertOuUnderRoot(clientsOu, requiredRoot, "AD_CLIENTS_OU_DN");
  assert.ok(
    allowedRoots.length >= 2,
    "AD_ALLOWED_ROOTS doit contenir au moins deux OUs.",
  );
  assert.equal(
    new Set(allowedRoots.map((value) => value.toLowerCase())).size,
    allowedRoots.length,
    "AD_ALLOWED_ROOTS ne doit pas contenir de doublon.",
  );
  for (const [index, allowedRoot] of allowedRoots.entries()) {
    assertOuUnderRoot(
      allowedRoot,
      requiredRoot,
      `AD_ALLOWED_ROOTS[${index}]`,
    );
  }
  assert.equal(
    allowedRoots.filter(
      (allowedRoot) => allowedRoot.toLowerCase() === clientsOu.toLowerCase(),
    ).length,
    1,
    "AD_ALLOWED_ROOTS doit contenir exactement AD_CLIENTS_OU_DN.",
  );
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

console.log(`\nContrat signup V0.40 valide (${checks.length} verifications).`);
