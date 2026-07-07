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
const cartContracts = await read(
  "../../apps/api-internal/Contracts/CartContracts.cs",
);
const cartRepoInterface = await read(
  "../../apps/api-internal/Data/Repositories/ICartRepository.cs",
);
const cartRepoMaria = await read(
  "../../apps/api-internal/Data/Repositories/MariaDbCartRepository.cs",
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
const souscrirePage = await read("app/souscrire/page.tsx");
const cartPage = await read("app/panier/page.tsx");
const addToCartButton = await read("components/AddToCartButton.tsx");
const cartConfirmButton = await read("components/CartConfirmButton.tsx");

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

// --- Contrats C# ---
assert.match(cartContracts, /record CartSummaryResponse/);
assert.match(cartContracts, /record CartConfirmResponse/);
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

// --- Endpoints + DI ---
for (const route of [
  '"/internal/portal/cart"',
  '"/internal/portal/cart/items"',
  '"/internal/portal/cart/items/remove"',
  '"/internal/portal/cart/confirm"',
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

// --- UI ---
assert.match(
  souscrirePage,
  /AddToCartButton/,
  "La page souscrire doit permettre l'ajout au panier.",
);
assert.match(
  souscrirePage,
  /billingCadence === "one_time"/,
  "Seules les offres one-shot sont proposees a l'ajout panier.",
);
assert.match(souscrirePage, /href="\/panier"/);
assert.match(cartPage, /CartConfirmButton/);
assert.match(cartPage, /getCart/);
assert.match(
  cartConfirmButton,
  /\/commercial-documents\//,
  "La confirmation doit rediriger vers le document a regler.",
);
assert.match(addToCartButton, /\/api\/cart\/items/);

// --- Garde-fou : pas de nouveau rail de paiement ---
assert.doesNotMatch(
  [cartConfirmButton, cartPage].join("\n"),
  /create-intent|paypal\/create/,
  "Le panier ne doit pas introduire de nouveau rail : le reglement passe par le document.",
);

console.log("Vérification du contrat panier à la carte V0.35 réussie.");
