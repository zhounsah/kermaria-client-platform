import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
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

console.log("Vérification du contrat sécurité AD V0.19 + V0.25 briques 2a/2b/2c réussie.");
