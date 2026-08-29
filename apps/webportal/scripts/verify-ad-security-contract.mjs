import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

async function readApi(path) {
  return readFile(new URL(`../../../${path}`, import.meta.url), "utf8");
}

const adminBff = await read("lib/admin-bff.ts");
const csrfServer = await read("lib/csrf-server.ts");
const csrfClient = await read("lib/csrf.ts");
const runtimeConfig = await read("lib/runtime-config.ts");

// CSRF côté serveur — validation du jeton dans le BFF admin
assert.match(adminBff, /hasValidCsrfToken/, "Le BFF admin doit valider le jeton CSRF.");
assert.match(adminBff, /CSRF_FORBIDDEN/, "Le BFF admin doit rejeter les requêtes sans CSRF valide.");

// CSRF côté client — le module client doit générer et attacher le jeton
assert.match(csrfClient, /csrf/, "Le module CSRF client doit exister.");
assert.match(csrfServer, /timingSafeEqual|crypto/, "Le CSRF serveur doit utiliser une comparaison à temps constant.");

// X-Service-Auth exigé hors Development — validé dans la configuration runtime
assert.match(
  runtimeConfig,
  /SERVICE_AUTH_TOKEN/,
  "Le token d'authentification interservice doit être vérifié dans la configuration.",
);

// Aucune route admin ne doit exposer de secret en clair
assert.doesNotMatch(adminBff, /password\s*=\s*["'][^"']+["']/i, "Le BFF admin ne doit pas contenir de mot de passe en clair.");

// V0.25 brique 2a — lecture groupes effectifs d'un utilisateur AD
const adUserGroupsRoute = await read(
  "app/api/admin/customers/[customerReference]/ad/users/[samAccountName]/groups/route.ts",
);
assert.match(
  adUserGroupsRoute,
  /handleAdminGet/,
  "La route lecture des groupes effectifs doit passer par handleAdminGet (session + CSRF + admin).",
);
assert.match(
  adUserGroupsRoute,
  /\/internal\/admin\/customers\/.+\/ad\/users\/.+\/groups/,
  "La route doit forwarder vers l'endpoint API-INTERNAL dedie.",
);

// V0.25 brique 2b — renommage d'un utilisateur AD
const adUserRenameRoute = await read(
  "app/api/admin/customers/[customerReference]/ad/users/[samAccountName]/rename/route.ts",
);
assert.match(
  adUserRenameRoute,
  /handleAdminMutation/,
  "La route renommage AD doit passer par handleAdminMutation (session + CSRF + admin).",
);
assert.match(
  adUserRenameRoute,
  /parseAdUserRenamePayload/,
  "La route renommage doit valider le payload via parseAdUserRenamePayload.",
);
assert.match(
  adUserRenameRoute,
  /\/internal\/admin\/customers\/.+\/ad\/users\/.+\/rename/,
  "La route renommage doit forwarder vers l'endpoint API-INTERNAL dedie.",
);

// V0.25 brique 2c — deplacement (Users<->Disabled + cross-client)
const adUserMoveRoute = await read(
  "app/api/admin/customers/[customerReference]/ad/users/[samAccountName]/move/route.ts",
);
assert.match(
  adUserMoveRoute,
  /handleAdminMutation/,
  "La route deplacement AD doit passer par handleAdminMutation (session + CSRF + admin).",
);
assert.match(
  adUserMoveRoute,
  /parseAdUserMovePayload/,
  "La route deplacement doit valider le payload via parseAdUserMovePayload.",
);
assert.match(
  adUserMoveRoute,
  /\/internal\/admin\/customers\/.+\/ad\/users\/.+\/move/,
  "La route deplacement doit forwarder vers l'endpoint API-INTERNAL dedie.",
);

// V0.25 brique 1 — changement de mot de passe AD client
const passwordRoute = await read("app/api/profile/password/route.ts");
assert.match(
  passwordRoute,
  /handlePortalPayloadMutation/,
  "La route de changement de mot de passe doit passer par handlePortalPayloadMutation (session client).",
);
assert.match(
  passwordRoute,
  /\/internal\/profile\/password/,
  "La route doit forwarder vers l'endpoint API-INTERNAL /internal/profile/password.",
);
assert.doesNotMatch(
  passwordRoute,
  /console\.(log|info|warn|error)\([^)]*password/i,
  "La route ne doit jamais journaliser le mot de passe.",
);

const passwordForm = await read("components/PasswordChangeForm.tsx");
assert.match(
  passwordForm,
  /type="password"/,
  "Le formulaire doit utiliser type=password pour masquer la saisie.",
);
assert.match(
  passwordForm,
  /autoComplete="new-password"/,
  "Le formulaire doit declarer autoComplete pour les gestionnaires de mot de passe.",
);
assert.doesNotMatch(
  passwordForm,
  /localStorage|sessionStorage/,
  "Le formulaire ne doit pas stocker le mot de passe en local/sessionStorage.",
);

// Le drapeau est lu dans runtime-config (partage avec la page profil, qui
// annonce ou non le parcours) ; la page doit passer par ce meme helper.
assert.match(
  runtimeConfig,
  /AD_PASSWORD_CHANGE_ENABLED/,
  "isPasswordChangeEnabled doit lire le flag AD_PASSWORD_CHANGE_ENABLED.",
);

const passwordPage = await read("app/password/page.tsx");
assert.match(
  passwordPage,
  /isPasswordChangeEnabled/,
  "La page doit verifier le flag AD_PASSWORD_CHANGE_ENABLED avant de rendre le formulaire.",
);

// Aucun nom d'hote ou chemin interne ne doit fuiter dans les pages client.
const profilePage = await read("app/profile/page.tsx");
const profileEditPage = await read("app/profile/edit/page.tsx");
const profileEditForm = await read("components/ProfileEditForm.tsx");
for (const [label, source] of [
  ["la page mot de passe", passwordPage],
  ["le formulaire de mot de passe", passwordForm],
  ["la page profil", profilePage],
  ["la page de modification du profil", profileEditPage],
  ["le formulaire de profil", profileEditForm],
]) {
  assert.doesNotMatch(
    source,
    /clients\.home\.bzh|\/internal\//,
    `Aucun chemin ou domaine interne ne doit apparaitre dans ${label}.`,
  );
}

// Autorité KoXo : aucune écriture LDAP de cycle de vie quand KoXo fait autorité.
// Le contrôle est structurel — une nouvelle route héritera du garde sans qu'on
// ait à y penser, ce qui est précisément la raison de le poser dans le service.
const ldapService = await readApi(
  "apps/api-internal/Services/ActiveDirectory/LdapActiveDirectoryService.cs",
);
const lifecycleWrites = [
  "CreateUserAsync",
  "DisableUserAsync",
  "MoveUserToDisabledAsync",
  "RenameUserAsync",
  "MoveUserAsync",
  "ChangeUserPasswordAsync",
  "SetUserPasswordAsync",
];
for (const method of lifecycleWrites) {
  const start = ldapService.indexOf(
    `public Task<AdServiceResult<AdDirectoryObjectSummary>> ${method}(`,
  );
  assert.notEqual(start, -1, `${method} doit exister dans le service LDAP.`);
  const guard = ldapService.indexOf("KoxoAuthorityResult()", start);
  const writesEnabled = ldapService.indexOf("_configuration.WritesEnabled", start);
  assert.ok(
    guard !== -1 && writesEnabled !== -1 && guard - writesEnabled < 900,
    `${method} doit refuser l'écriture quand KoXo fait autorité, avant toute liaison LDAP.`,
  );
}
assert.match(
  ldapService,
  /AD_LIFECYCLE_KOXO_AUTHORITY/,
  "Le refus d'autorité KoXo doit porter un code explicite.",
);

// Le mandat conservé : les groupes de services restent pilotés par API-INTERNAL.
for (const method of ["AddGroupMemberAsync", "RemoveGroupMemberAsync"]) {
  const start = ldapService.indexOf(
    `public Task<AdServiceResult<AdDirectoryObjectSummary>> ${method}(`,
  );
  const body = ldapService.slice(start, start + 700);
  assert.doesNotMatch(
    body,
    /KoxoAuthorityResult/,
    `${method} ne doit pas être bloquée : l'appartenance aux groupes reste le mandat d'API-INTERNAL.`,
  );
}

// Le changement de mot de passe du portail passe par KoXo quand KoXo en est
// l'autorité : une écriture LDAP serait écrasée à la synchronisation suivante,
// sans erreur visible, et le client perdrait ses accès.
const apiProgram = await readApi("apps/api-internal/Program.cs");
assert.match(
  apiProgram,
  /koxoOwnsPassword/,
  "La route /internal/profile/password doit distinguer le cas où KoXo fait autorité.",
);
assert.match(
  apiProgram,
  /KOXO_PASSWORD_HANDOFF_UNAVAILABLE/,
  "Un relais KoXo indisponible doit refuser le changement plutôt que d'écrire en LDAP.",
);
assert.match(
  apiProgram,
  /AD_PASSWORD_CHANGE_PENDING_KOXO/,
  "Le message doit dire que l'application aux services attend la synchronisation.",
);

// Le condensat portail et le secret destiné à KoXo forment une seule unité de
// travail. Les déposer l'un après l'autre laisse une fenêtre : si la seconde
// écriture échoue, KoXo applique plus tard à l'annuaire un mot de passe que le
// portail ignore, et le client ouvre NextCloud, RDS et VPN avec un mot de passe
// et le portail avec un autre — sans que rien ne le signale.
const passwordRouteBody = apiProgram.slice(
  apiProgram.indexOf('"/internal/profile/password"'),
  apiProgram.indexOf('"/internal/profile/password"') + 14000,
);
assert.match(
  passwordRouteBody,
  /pendingPasswords\.Seal\(/,
  "La route doit sceller le secret sans l'écrire, pour que l'écriture ait lieu dans la transaction du condensat.",
);
assert.match(
  passwordRouteBody,
  /TryChangePasswordWithKoxoHandoffAsync/,
  "Condensat portail et secret KoXo doivent être écrits par la même unité de travail.",
);
assert.doesNotMatch(
  passwordRouteBody,
  /pendingPasswords\.PublishAsync/,
  "Publier le secret hors de la transaction rouvre la fenêtre de désynchronisation.",
);
assert.match(
  passwordRouteBody,
  /PASSWORD_CHANGE_STORAGE_UNAVAILABLE/,
  "Une persistance indisponible doit être annoncée telle quelle, sans succès partiel.",
);

// Même invariant sur le parcours d'inscription, qui partage le magasin.
const signupService = await readApi("apps/api-internal/Services/SignupService.cs");
assert.match(
  signupService,
  /_pendingPasswords\.Seal\(/,
  "L'inscription doit sceller le secret destiné à KoXo au lieu de le publier.",
);
assert.doesNotMatch(
  signupService,
  /_pendingPasswords\.PublishAsync/,
  "L'inscription ne doit pas déposer le secret avant le commit du mot de passe portail.",
);
assert.match(
  signupService,
  /PASSWORD_CHANGE_STORAGE_UNAVAILABLE/,
  "Un échec d'enregistrement du mot de passe d'inscription doit être annoncé, pas masqué.",
);

// La conversion d'un essai ne déplace pas l'identité en LDAP sous autorité KoXo.
const demoConversion = await readApi(
  "apps/api-internal/Services/DemoConversionService.cs",
);
const moveIdentity = demoConversion.slice(
  demoConversion.indexOf("private async Task<bool> MoveIdentityAsync("),
);
assert.match(
  moveIdentity.slice(0, 400),
  /_adConfiguration\.KoxoOwnsDirectory/,
  "Le déplacement d'identité doit être court-circuité quand KoXo fait autorité.",
);

console.log("Vérification du contrat sécurité AD V0.19 + V0.25 briques 1/2a/2b/2c réussie.");
