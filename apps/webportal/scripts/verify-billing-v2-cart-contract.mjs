import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const read = (relative) => readFileSync(resolve(root, relative), "utf8");
const assert = (condition, message) => {
  if (!condition) throw new Error(message);
};

const route = read("app/api/billing-v2/cart/route.ts");
const cookie = read("lib/cart-cookie.ts");
const csrfClient = read("lib/client-api.ts");
const authMeRoute = read("app/api/auth/me/route.ts");
const transport = read("lib/internal-api.ts");
const cartService = read("../api-internal/Services/BillingV2CartService.cs");
const cartModel = read("../api-internal/Services/BillingV2CartModel.cs");
const cartPolicy = read("../api-internal/Services/BillingV2CartPolicy.cs");
const nativeSelectionResolver = read("../api-internal/Services/BillingV2NativeSelectionResolver.cs");
const apiProgram = read("../api-internal/Program.cs");
const cartMigration = read("../api-internal/Migrations/MariaDb/090_billing_v2_cart_model.sql");
const presetMetadataMigration = read("../api-internal/Migrations/MariaDb/091_billing_v2_preset_cart_metadata.sql");
const cartOriginMigration = read("../api-internal/Migrations/MariaDb/093_billing_v2_cart_item_origin.sql");
const cartConfigurator = read("components/BillingV2CartFormuleConfigurator.tsx");
const cartSchemaTests = read("../../tests/api-internal/BillingV2CartSchemaTests.cs");

assert(route.includes("sanitizeCartCommand"), "Le BFF Cart doit filtrer le payload navigateur.");
assert(route.includes('"initialize_preset"') && route.includes('"replace_preset"'),
  "Le BFF Cart doit exposer une initialisation de preset et un remplacement explicite.");
assert(route.includes('"import_formula_selection"')
  && route.includes("readBillingV2SelectionPayload(payload.formulaSelection)"),
  "Le BFF doit accepter le handoff explicite d'une formule apres filtrage de sa selection.");
assert(route.includes('"claim_current"'),
  "Le BFF Cart doit pouvoir reclamer le Cart anonyme lors de la reprise apres login.");
assert(route.includes('"get_current"') && route.includes("readOnlyCommands"),
  "Le BFF doit offrir une lecture current sans creation de Cart pour le panier et son badge.");
assert(route.includes("rejectInvalidPortalCsrf"), "Toute mutation Cart doit passer le garde CSRF.");
assert(route.indexOf("rejectInvalidPortalCsrf") < route.indexOf("commandBillingV2Cart"),
  "Un POST Cart sans CSRF valide doit etre refuse avant tout appel API-INTERNAL.");
assert(cartConfigurator.includes("requestBffJson<BillingV2CartCommandResponse>")
  && !cartConfigurator.includes('fetch("/api/billing-v2/cart"'),
  "Toutes les commandes Cart client doivent reutiliser le transport BFF CSRF commun.");
assert(csrfClient.includes("CSRF_HEADER_NAME")
  && csrfClient.includes('fetch("/api/auth/me"')
  && csrfClient.includes("csrfInitialization"),
  "Le transport BFF doit initialiser puis attacher le token CSRF commun de facon single-flight.");
assert(authMeRoute.includes("return unauthenticated(request, correlationId)")
  && authMeRoute.includes("ensureCsrfCookie(request, response)"),
  "Un visiteur anonyme doit obtenir le cookie CSRF commun sans session client.");
assert(!cookie.includes("CSRF") && !route.includes("CSRF_COOKIE_NAME"),
  "Le token opaque Cart ne doit jamais etre reutilise comme token CSRF.");
assert(route.includes("clearAnonymousCartToken"), "Un claim reussi doit retirer le token anonyme.");
assert(cookie.includes("randomBytes(32)"), "Le token Cart doit disposer de 256 bits d'entropie.");
assert(cookie.includes("CART_TOKEN_PATTERN"), "Le cookie Cart doit rejeter les tokens malformes.");
assert(cookie.includes("getSessionCookieOptions"), "Le cookie Cart doit reutiliser les options HttpOnly de session.");
assert(cookie.includes("maxAge: CART_COOKIE_MAX_AGE_SECONDS"), "Le cookie Cart doit expirer avec le Cart.");
assert(cookie.includes("expires: new Date(0)"), "Un claim reussi doit invalider le cookie anonyme precedent.");
assert(cookie.includes("refreshAnonymousCartToken"), "Le cookie anonyme doit pouvoir etre renouvele glissant.");
assert(cookie.includes("resolveAnonymousCartToken") && !cookie.includes("cookieStore.set(CART_COOKIE_NAME"),
  "Un token anonyme sans cookie reste un candidat jusqu'a une reponse Cart valide.");
assert(route.includes("cartActivityCommands") && route.includes("result.cart && cartActivityCommands.has(payload.command)")
  && route.includes("refreshAnonymousCartToken(response, anonymousToken)"),
  "Le renouvellement du cookie depend de la commande ayant touche le Cart, pas d'une copie des codes succes.");
assert(route.includes("resolveAnonymousCartToken") && !route.includes("ensureAnonymousCartToken"),
  "Le BFF ne doit pas ecrire un nouveau cookie avant le resultat API-INTERNAL.");
assert(route.includes("cartCreatingCommands") && route.includes('"import_formula_selection"'),
  "Seul l'import explicite de formule peut obtenir un token anonyme Cart avant son premier ajout.");
assert(!route.includes("anonymousSessionHash"), "Le BFF ne doit pas exposer le condensat de possession anonyme.");
assert(transport.includes("/internal/portal/billing-v2/carts/commands"), "Le BFF doit appeler l'API Cart interne.");
const currentStart = cartService.indexOf("public async Task<BillingV2CartMutationResult> GetOrCreateCurrentAsync");
const currentEnd = cartService.indexOf("public async Task<BillingV2CartMutationResult> GetAsync", currentStart);
const currentBody = cartService.slice(currentStart, currentEnd);
assert(currentBody.indexOf("ExpireInactiveCurrentAsync") < currentBody.indexOf("ReadCurrentAsync"),
  "current doit expirer transactionnellement les Carts open avant de rechercher le Cart courant.");
assert(currentBody.includes("CurrentTransactionMaxAttempts")
  && currentBody.includes("IsCurrentTransactionRetryable")
  && cartService.includes("exception.Number == 1213")
  && cartService.includes("exception.SqlState, \"40001\""),
"Le retry current doit etre borne et reserve aux deadlocks MariaDB.");
assert(currentBody.includes("IsDuplicateKey") && currentBody.includes("ReadCurrentAsync(connection, transaction, owner, currency, true"),
  "Une collision de cle unique current doit relire le Cart gagnant dans la transaction.");
assert(!cartService.includes("WHERE status = 'open' AND expires_at <= @now;"),
  "Le chemin Cart ne doit plus expirer globalement les Carts de tous les proprietaires.");
assert(cartService.includes("AND {ownerColumn} = @owner AND {openSlot} = 1")
  && cartService.includes("AND currency = @currency"),
"L'expiration current doit etre bornee a owner/devise et liberer les slots dans la meme ecriture.");
assert(currentBody.includes("TouchCartActivityAsync"),
  "current doit prolonger l'activite du Cart open existant avant de repondre.");
assert(cartService.includes("command=current owner_type={OwnerType}")
  && cartService.includes("retry_deadlock_count={RetryDeadlockCount}"),
"Les logs current doivent permettre de distinguer owner, resultat et retries sans exposer le token.");
assert(apiProgram.includes('CreateLogger("BillingV2CartCommands")')
  && apiProgram.includes("command={Command} owner_type={OwnerType} cart_id={CartId} result={Result}"),
"Les commandes Cart hors current doivent journaliser leur resultat sans token ni hash de possession.");
assert(cartModel.includes("public static class BillingV2CartMutationResults")
  && cartModel.includes('"CART_PRESET_INITIALIZED"')
  && cartModel.includes('"CART_FORMULA_SELECTION_IMPORTED"')
  && cartModel.includes('"CART_MERGE_REQUIRES_REVIEW"')
  && cartModel.includes("BillingV2CartMutationOutcome.Success => 200")
  && apiProgram.includes("BillingV2CartMutationResults.HttpStatusCode(result)")
  && !apiProgram.includes("CartResultStatusCode"),
"Le mapping Cart HTTP doit etre centralise dans le modele API, sans default local oublieux.");
assert(!cartConfigurator.includes("body.code.startsWith") && !cartConfigurator.includes(".includes(body.code)"),
  "Le frontend ne doit pas maintenir une seconde liste de codes Cart reussis.");
assert(apiProgram.includes("MySqlException => (") && apiProgram.includes("StatusCodes.Status503ServiceUnavailable")
  && apiProgram.includes('"SQL_UNAVAILABLE"'),
"Une indisponibilite SQL doit rester une reponse endpoint 503 controlee.");
assert(cartConfigurator.includes("bootstrapKeyRef") && cartConfigurator.includes("bootstrapGenerationRef"),
  "Le bootstrap du configurateur doit etre single-flight face au double Effect Strict Mode.");
assert(!cartConfigurator.includes("let cancelled = false"),
  "Un echec tardif ne doit pas annuler une initialisation reussie du meme bootstrap.");
assert(cartSchemaTests.includes("Task.WhenAll(Enumerable.Range(0, 10)")
  && cartSchemaTests.includes("GetOrCreateAnonymousCurrentAsync")
  && cartSchemaTests.includes("connection.BeginTransactionAsync")
  && cartSchemaTests.includes("BILLING_V2_TEST_MARIADB_CONNECTION"),
"Le test opt-in MariaDB doit exercer dix current concurrents dans des transactions distinctes.");
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
assert(cartService.includes("InitializeFromPresetAsync")
  && cartService.includes("CART_PRESET_CONFLICT")
  && cartService.includes("InsertPresetItemAsync"),
  "L'initialisation de preset doit etre transactionnelle, idempotente et server-authoritative.");
assert(route.includes('"add_preset_item"') && cartService.includes("AddPresetItemAsync")
  && cartService.includes("ReadPresetItemAsync"),
  "Une option de preset doit etre ajoutee par son identifiant autorise, jamais par un service arbitraire.");
assert(presetMetadataMigration.includes("selected_by_default")
  && presetMetadataMigration.includes("required_item = 0 OR selected_by_default = 1"),
  "La metadata de preset doit separer selection initiale et obligation.");
assert(cartService.includes("ProjectLegacySelectionAsync")
  && !cartService.includes("BillingV2AuthoritativeCheckoutService"),
  "La compatibilite legacy doit rester une projection Cart vers selection, jamais un checkout Cart.");
assert(cartOriginMigration.includes("ADD COLUMN IF NOT EXISTS origin VARCHAR(24) NULL"),
  "093 ajoute seulement la provenance d'item necessaire au Cart multi-origines, sans modifier les historiques.");
assert(cartService.includes("BillingV2CartItemOrigins.Direct")
  && cartService.includes("BillingV2CartItemOrigins.Dependency")
  && cartService.includes("BillingV2CartItemOrigins.Structural"),
  "Les origines direct/preset/dependance/structure sont ecrites par le serveur, jamais choisies par le navigateur.");
assert(cartPolicy.includes("public static class BillingV2CatalogScopeTemplatePolicy")
  && cartPolicy.includes('case "user":')
  && cartPolicy.includes("BillingV2CartScopeTemplates.PrimaryUser"),
  "La traduction catalogue user vers le scope Cart primary_user doit etre centralisee cote serveur.");
const resolveItemStart = cartService.indexOf("private async Task<ResolvedItem> ResolveItemAsync");
const resolveItemEnd = cartService.indexOf("private static async Task<bool> HasCurrentInitialPriceAsync", resolveItemStart);
const resolveItem = cartService.slice(resolveItemStart, resolveItemEnd);
assert(resolveItem.includes("BillingV2CatalogScopeTemplatePolicy.TryMapToCartTemplate(scope")
  && resolveItem.includes('return new("CART_SCOPE_UNSUPPORTED")')
  && !resolveItem.includes("command.ScopeTemplate ?? scope;\n        if (command.Origin == \"preset\"") ,
  "Un ajout direct doit etre normalise ou refuse avant INSERT, sans persister le scope catalogue brut.");
assert(nativeSelectionResolver.includes("BillingV2CatalogScopeTemplatePolicy.TryMapToCartTemplate")
  && !nativeSelectionResolver.includes("MapDefaultScopeType"),
  "Les parcours natifs et Cart doivent partager la meme traduction catalogue vers scope.");

console.log("Contrat BFF Cart Billing V2 verifie.");
