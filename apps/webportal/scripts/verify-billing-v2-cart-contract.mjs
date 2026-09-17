import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const read = (relative) => readFileSync(resolve(root, relative), "utf8");
const assert = (condition, message) => {
  if (!condition) throw new Error(message);
};

const route = read("app/api/billing-v2/cart/route.ts");
const cookie = read("lib/cart-cookie.ts");
const transport = read("lib/internal-api.ts");
const cartService = read("../api-internal/Services/BillingV2CartService.cs");
const cartMigration = read("../api-internal/Migrations/MariaDb/090_billing_v2_cart_model.sql");

assert(route.includes("sanitizeCartCommand"), "Le BFF Cart doit filtrer le payload navigateur.");
assert(route.includes("rejectInvalidPortalCsrf"), "Toute mutation Cart doit passer le garde CSRF.");
assert(route.includes("clearAnonymousCartToken"), "Un claim reussi doit retirer le token anonyme.");
assert(cookie.includes("randomBytes(32)"), "Le token Cart doit disposer de 256 bits d'entropie.");
assert(cookie.includes("CART_TOKEN_PATTERN"), "Le cookie Cart doit rejeter les tokens malformes.");
assert(cookie.includes("getSessionCookieOptions"), "Le cookie Cart doit reutiliser les options HttpOnly de session.");
assert(cookie.includes("maxAge: CART_COOKIE_MAX_AGE_SECONDS"), "Le cookie Cart doit expirer avec le Cart.");
assert(cookie.includes("expires: new Date(0)"), "Un claim reussi doit invalider le cookie anonyme precedent.");
assert(cookie.includes("refreshAnonymousCartToken"), "Le cookie anonyme doit pouvoir etre renouvele glissant.");
assert(route.includes("cartActivityRenewalCodes") && route.includes("refreshAnonymousCartToken(response, anonymousToken)"),
  "Une mutation Cart reussie doit renouveler le cookie anonyme avec le TTL du Cart.");
assert(!route.includes("anonymousSessionHash"), "Le BFF ne doit pas exposer le condensat de possession anonyme.");
assert(transport.includes("/internal/portal/billing-v2/carts/commands"), "Le BFF doit appeler l'API Cart interne.");
const currentStart = cartService.indexOf("public async Task<BillingV2CartMutationResult> GetOrCreateCurrentAsync");
const currentEnd = cartService.indexOf("public async Task<BillingV2CartMutationResult> GetAsync", currentStart);
const currentBody = cartService.slice(currentStart, currentEnd);
assert(currentBody.indexOf("ExpireInactiveAsync") < currentBody.indexOf("ReadCurrentAsync"),
  "current doit expirer transactionnellement les Carts open avant de rechercher le Cart courant.");
assert(cartService.includes("WHERE status = 'open' AND expires_at <= @now"),
  "L'expiration logique cible uniquement les Carts open dont le TTL est depasse.");
assert(currentBody.includes("TouchCartActivityAsync"),
  "current doit prolonger l'activite du Cart open existant avant de repondre.");
assert(!cartMigration.includes("GENERATED ALWAYS AS")
  && cartMigration.includes("open_customer_slot TINYINT NULL")
  && cartMigration.includes("open_anonymous_slot TINYINT NULL"),
  "090 doit utiliser des slots physiques nullable, sans generated columns.");
assert(cartMigration.includes("(customer_id, currency, open_customer_slot)")
  && cartMigration.includes("(anonymous_session_hash, currency, open_anonymous_slot)"),
  "090 doit imposer un Cart open par owner et devise via les slots.");
assert(cartMigration.includes("DROP INDEX IF EXISTS uq_billing_v2_carts_open_customer_currency")
  && cartMigration.includes("DROP COLUMN IF EXISTS open_customer_key")
  && cartMigration.includes("ADD COLUMN IF NOT EXISTS open_customer_slot")
  && cartMigration.includes("ADD UNIQUE KEY IF NOT EXISTS uq_billing_v2_carts_open_customer_currency"),
  "090 doit pouvoir reprendre une tentative partiellement appliquee sans generated columns.");
assert(cartService.includes("open_customer_slot = NULL")
  && cartService.includes("open_anonymous_slot = NULL")
  && cartService.includes("open_customer_slot = 1"),
  "Les transitions Cart doivent liberer ou transferer les slots dans leur transaction.");
assert(!route.includes("checkout") && !transport.slice(transport.indexOf("commandBillingV2Cart"), transport.indexOf("commandBillingV2Cart") + 1800).includes("provider"),
  "Le BFF Cart ne doit pas exposer de checkout ou provider.");

console.log("Contrat BFF Cart Billing V2 verifie.");
