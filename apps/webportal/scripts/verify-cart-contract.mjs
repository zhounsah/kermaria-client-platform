import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

// --- Artefacts ---
const sharedTypes = await read("../../packages/shared/src/index.ts");
const migration = await read(
  "../../apps/api-internal/Migrations/MariaDb/028_alacarte_cart.sql",
);
const recurringCheckoutMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/029_billed_recurring_checkout.sql",
);
const recurringOfferCadenceBackfillMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/030_recurring_offer_cadence_backfill.sql",
);
const cartContracts = await read(
  "../../apps/api-internal/Contracts/CartContracts.cs",
);
const checkoutContracts = await read(
  "../../apps/api-internal/Contracts/CheckoutContracts.cs",
);
const cartRepoInterface = await read(
  "../../apps/api-internal/Data/Repositories/ICartRepository.cs",
);
const cartRepoMaria = await read(
  "../../apps/api-internal/Data/Repositories/MariaDbCartRepository.cs",
);
const recurringCheckoutRepo = await read(
  "../../apps/api-internal/Data/Repositories/MariaDbRecurringCheckoutRepository.cs",
);
const cartRepoMock = await read(
  "../../apps/api-internal/Data/Repositories/MockCartRepository.cs",
);
const commercialRepoInterface = await read(
  "../../apps/api-internal/Data/Repositories/ICommercialRepository.cs",
);
const commercialRepoMaria = await read(
  "../../apps/api-internal/Data/Repositories/MariaDbCommercialRepository.cs",
);
const cartService = await read(
  "../../apps/api-internal/Services/CartService.cs",
);
const recurringCheckoutService = await read(
  "../../apps/api-internal/Services/RecurringCheckoutService.cs",
);
const cartProvisioning = await read(
  "../../apps/api-internal/Services/Provisioning/CartProvisioningTrigger.cs",
);
const invoiceIssuingService = await read(
  "../../apps/api-internal/Services/InvoiceIssuingService.cs",
);
const programCs = await read("../../apps/api-internal/Program.cs");
const internalApi = await read("lib/internal-api.ts");
const cartGetRoute = await read("app/api/cart/route.ts");
const cartAddRoute = await read("app/api/cart/items/route.ts");
const cartRemoveRoute = await read("app/api/cart/items/remove/route.ts");
const cartConfirmRoute = await read("app/api/cart/confirm/route.ts");
const checkoutSummaryRoute = await read("app/api/checkout/summary/route.ts");
const recurringCheckoutItemsRoute = await read(
  "app/api/checkout/subscriptions/items/route.ts",
);
const recurringCheckoutRemoveRoute = await read(
  "app/api/checkout/subscriptions/items/remove/route.ts",
);
const recurringCheckoutConfirmRoute = await read(
  "app/api/checkout/subscriptions/confirm/route.ts",
);
const souscrirePage = await read("app/souscrire/page.tsx");
const cartPage = await read("app/panier/page.tsx");
const appShell = await read("components/AppShell.tsx");
const addToCartButton = await read("components/AddToCartButton.tsx");
const addRecurringCheckoutButton = await read(
  "components/AddRecurringCheckoutButton.tsx",
);
const cartConfirmButton = await read("components/CartConfirmButton.tsx");
const recurringCheckoutConfirmButton = await read(
  "components/RecurringCheckoutConfirmButton.tsx",
);
const headerCartDrawer = await read("components/HeaderCartDrawer.tsx");

// --- Schema (migration 028) ---
assert.match(
  migration,
  /CREATE TABLE IF NOT EXISTS cart_items/i,
  "La table cart_items doit etre creee.",
);
assert.match(
  migration,
  /UNIQUE KEY uq_cart_items_customer_offer \(customer_id, offer_id\)/,
  "Un panier ne peut contenir qu'une ligne par offre (unicite customer+offer).",
);
assert.match(
  migration,
  /ALTER TABLE commercial_documents\s+ADD COLUMN IF NOT EXISTS origin VARCHAR\(32\)/,
  "commercial_documents.origin doit etre ajoute pour tagger les documents panier.",
);
assert.match(
  recurringCheckoutMigration,
  /CREATE TABLE IF NOT EXISTS recurring_checkout_items/i,
  "La migration recurring checkout doit creer recurring_checkout_items.",
);
assert.match(
  recurringCheckoutMigration,
  /CREATE TABLE IF NOT EXISTS commercial_document_line_subscriptions/i,
  "La migration recurring checkout doit creer le lien document-ligne-souscription.",
);
assert.match(
  recurringOfferCadenceBackfillMigration,
  /SAVE-PERSO[\s\S]*SUPERV-SERVICE/,
  "Le backfill doit remettre les offres recurrentes historiques dans le bon tunnel.",
);
assert.match(
  recurringOfferCadenceBackfillMigration,
  /unit_label = 'Forfait'/,
  "L'offre one-shot de demonstration doit etre normalisee pour ne plus se presenter comme mensuelle.",
);

// --- Types partages ---
assert.match(
  sharedTypes,
  /export interface CartSummary/,
  "CartSummary doit etre expose cote types partages.",
);
assert.match(
  sharedTypes,
  /export interface CartConfirmResponse/,
  "CartConfirmResponse doit etre expose.",
);
assert.match(
  sharedTypes,
  /export interface CheckoutSummary/,
  "CheckoutSummary doit etre expose cote types partages.",
);
assert.match(
  sharedTypes,
  /export interface RecurringCheckoutItem/,
  "RecurringCheckoutItem doit etre expose cote types partages.",
);
assert.match(
  sharedTypes,
  /export interface CheckoutRecurringConfirmResponse/,
  "CheckoutRecurringConfirmResponse doit etre expose.",
);

// --- Contrats C# ---
assert.match(cartContracts, /record CartSummaryResponse/);
assert.match(cartContracts, /record CartConfirmResponse/);
assert.match(checkoutContracts, /record CheckoutSummaryResponse/);
assert.match(checkoutContracts, /record CheckoutRecurringConfirmResponse/);
assert.match(
  cartContracts,
  /record CartAddRequest\(string\? OfferId, int\? Quantity\)/,
  "CartAddRequest doit accepter offerId + quantity optionnelle.",
);

// --- Repository panier ---
assert.match(cartRepoInterface, /interface ICartRepository/);
assert.match(cartRepoInterface, /UpsertItemAsync/);
assert.match(cartRepoMaria, /INSERT INTO cart_items/);
assert.match(
  cartRepoMaria,
  /ON DUPLICATE KEY UPDATE/,
  "L'ajout doit etre idempotent (upsert de la quantite).",
);
assert.match(
  cartRepoMaria,
  /ReadRequired\(reader, "offer_id"\)/,
  "La lecture MariaDB de offer_id doit tolerer le vrai type renvoye par MariaDB.",
);
assert.match(
  recurringCheckoutRepo,
  /recurring_checkout_items/,
  "Le repository recurring checkout doit persister recurring_checkout_items.",
);
assert.match(
  cartRepoMock,
  /class MockCartStore/,
  "Un store en memoire doit exister pour le mode mock.",
);

// --- Materialisation document ---
assert.match(
  commercialRepoInterface,
  /Task<CartDocumentCreationResult> CreateCartDocumentAsync/,
  "ICommercialRepository doit exposer CreateCartDocumentAsync.",
);
assert.match(
  commercialRepoInterface,
  /Task<CartPaidDocumentContext\?> GetCartPaidDocumentContextAsync/,
  "ICommercialRepository doit exposer le contexte de reglement panier.",
);
assert.match(
  commercialRepoMaria,
  /'client_cart'/,
  "Le document panier doit etre tagge origin = 'client_cart'.",
);
assert.match(
  commercialRepoMaria,
  /'shared_with_customer'/,
  "Le document panier doit etre cree pret a etre emis (shared_with_customer).",
);

// --- Regles metier du service ---
assert.match(
  cartService,
  /CadenceOneTime/,
  "Le panier doit refuser les offres non one-shot.",
);
assert.match(
  cartService,
  /CartOfferNotEligibleException/,
  "Une exception dediee doit sanctionner les offres non eligibles.",
);
assert.match(
  cartService,
  /EmptyCartException/,
  "La confirmation d'un panier vide doit etre refusee.",
);
assert.match(
  cartService,
  /CreateCartDocumentAsync[\s\S]*IssueInvoiceAsync/,
  "La confirmation doit creer le document puis l'emettre.",
);
assert.match(
  recurringCheckoutService,
  /CreateBilledPendingAsync/,
  "Le recurring checkout doit creer des souscriptions facturees avant paiement.",
);
assert.match(
  recurringCheckoutService,
  /CreateRecurringCheckoutDocumentAsync/,
  "Le recurring checkout doit creer une facture initiale groupee.",
);

// --- Provisioning « le cas echeant » ---
assert.match(
  cartProvisioning,
  /client_cart/,
  "Le provisioning ne doit se declencher que pour les documents panier.",
);
assert.match(
  cartProvisioning,
  /ReconcileAsync/,
  "Le declencheur doit reconcilier le provisioning existant.",
);
assert.match(
  invoiceIssuingService,
  /_cartProvisioning\.OnDocumentPaidAsync/,
  "ConfirmPaymentAsync doit appeler le provisioning panier (tous rails).",
);
assert.match(
  invoiceIssuingService,
  /_billedSubscriptions\.OnDocumentPaidAsync/,
  "ConfirmPaymentAsync doit aussi declencher l'activation des souscriptions facturees.",
);

// --- Endpoints + DI ---
for (const route of [
  '"/internal/portal/cart"',
  '"/internal/portal/cart/items"',
  '"/internal/portal/cart/items/remove"',
  '"/internal/portal/cart/confirm"',
  '"/internal/portal/checkout/summary"',
  '"/internal/portal/checkout/subscriptions/items"',
  '"/internal/portal/checkout/subscriptions/items/remove"',
  '"/internal/portal/checkout/subscriptions/confirm"',
]) {
  assert.ok(
    programCs.includes(route),
    `L'endpoint ${route} doit etre expose.`,
  );
}
assert.match(programCs, /AddScoped<ICartService, CartService>/);
assert.match(programCs, /AddScoped<ICartRepository>/);
assert.match(
  programCs,
  /AddScoped<ICartProvisioningTrigger, CartProvisioningTrigger>/,
);
assert.match(programCs, /CART_OFFER_NOT_ELIGIBLE/);
assert.match(programCs, /"CART_EMPTY"/);

// --- BFF WEBPORTAL ---
assert.match(cartGetRoute, /handlePortalGet<CartSummary>/);
assert.match(cartAddRoute, /\/internal\/portal\/cart\/items/);
assert.match(cartRemoveRoute, /\/internal\/portal\/cart\/items\/remove/);
assert.match(cartConfirmRoute, /handlePortalMutation/);
assert.match(internalApi, /export function getCart\(\)/);
assert.match(internalApi, /export function getCheckoutSummary\(\)/);
assert.match(internalApi, /getCheckoutSummaryWithLegacyFallback/);
assert.match(checkoutSummaryRoute, /\/internal\/portal\/checkout\/summary/);
assert.match(
  recurringCheckoutItemsRoute,
  /\/internal\/portal\/checkout\/subscriptions\/items/,
);
assert.match(
  recurringCheckoutRemoveRoute,
  /\/internal\/portal\/checkout\/subscriptions\/items\/remove/,
);
assert.match(
  recurringCheckoutConfirmRoute,
  /\/internal\/portal\/checkout\/subscriptions\/confirm/,
);

// --- UI ---
assert.match(
  souscrirePage,
  /AddToCartButton/,
  "La page souscrire doit permettre l'ajout au panier.",
);
assert.match(
  souscrirePage,
  /PublicPackCard/,
  "La page souscrire doit afficher les packs recurrents.",
);
assert.match(
  souscrirePage,
  /billingCadence === "one_time"/,
  "Seules les offres one-shot sont proposees a l'ajout panier.",
);
assert.match(souscrirePage, /href="\/panier"/);
assert.match(cartPage, /CartConfirmButton/);
assert.match(cartPage, /RecurringCheckoutConfirmButton/);
assert.match(cartPage, /getCheckoutSummary/);
assert.match(appShell, /HeaderCartDrawer/);
assert.match(
  cartConfirmButton,
  /\/commercial-documents\//,
  "La confirmation doit rediriger vers le document a regler.",
);
assert.match(addToCartButton, /\/api\/cart\/items/);
assert.match(
  addRecurringCheckoutButton,
  /\/api\/checkout\/subscriptions\/items/,
);
assert.match(
  recurringCheckoutConfirmButton,
  /\/commercial-documents\//,
  "La confirmation recurring checkout doit rediriger vers le document a regler.",
);
assert.match(headerCartDrawer, /\/api\/checkout\/summary/);
assert.match(headerCartDrawer, /\/api\/cart/);
assert.match(headerCartDrawer, /header-cart-drawer/);

// --- Garde-fou : pas de nouveau rail de paiement ---
assert.doesNotMatch(
  [cartConfirmButton, recurringCheckoutConfirmButton, cartPage].join("\n"),
  /create-intent|paypal\/create/,
  "Le recap unifie ne doit pas introduire de nouveau rail direct : le reglement passe par le document.",
);

console.log("Vérification du contrat panier à la carte V0.35 réussie.");
