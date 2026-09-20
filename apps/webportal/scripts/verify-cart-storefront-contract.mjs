import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const read = (relative) => readFileSync(resolve(root, relative), "utf8");

const tariffs = read("components/PublicCommercialTariffCatalog.tsx");
const directAdd = read("components/BillingV2DirectCartAdd.tsx");
const cartPage = read("components/BillingV2CartPage.tsx");
const cartRoute = read("app/api/billing-v2/cart/route.ts");
const cartClient = read("lib/billing-v2-cart-client.ts");
const header = read("components/BillingV2CartHeaderLink.tsx");
const shell = read("components/PublicShell.tsx");
const routes = read("lib/public-route-config.ts");
const cartService = read("../api-internal/Services/BillingV2CartService.cs");
const cartModel = read("../api-internal/Services/BillingV2CartModel.cs");
const catalog = read("../api-internal/Services/BillingV2PublicCatalogService.cs");
const publicProjection = read("lib/public-commercial-catalog.ts");
const migration = read("../api-internal/Migrations/MariaDb/093_billing_v2_cart_item_origin.sql");
const pricingSummary = read("components/BillingV2PricingSummary.tsx");

assert.match(tariffs, /service\.directlyOrderable[\s\S]*BillingV2DirectCartAdd/,
  "Seul un service déclaré directement compatible avec le Cart rend le CTA d'ajout.");
assert.doesNotMatch(tariffs, /orderingMode === "quote"[\s\S]*BillingV2DirectCartAdd/,
  "Un service sur devis ne peut pas recevoir le composant d'ajout direct.");
assert.doesNotMatch(tariffs, /orderingMode === "offer_component"[\s\S]*BillingV2DirectCartAdd/,
  "Un composant d'offre ne peut pas être acheté seul depuis /tarifs.");
assert.match(directAdd, /command: "add_item"/);
assert.match(directAdd, /origin: "direct"/);
const directCommandPayload = directAdd.slice(
  directAdd.indexOf('command: "add_item"'),
  directAdd.indexOf("if (!addedItem.ok)"),
);
assert.doesNotMatch(directCommandPayload, /amountCents|monthlyPrice|unitPrice|dueNow|discount/,
  "Le CTA direct ne transporte aucune autorité tarifaire navigateur.");
assert.doesNotMatch(directCommandPayload, /scopeTemplate|subjectBinding/,
  "Le CTA direct ne choisit pas la portée Cart : elle est résolue par API-INTERNAL depuis le catalogue.");
assert.match(directAdd, /command: "current"/,
  "Un Cart n'est créé depuis /tarifs qu'après le clic explicite Ajouter.");
assert.match(directAdd, /CART_ITEM_TIER_CONFLICT[\s\S]*Voir mon panier/,
  "Un conflit de palier depuis /tarifs doit mener vers la ligne à vérifier dans le panier.");
assert.match(cartService, /CART_DIRECT_QUANTITY_NOT_CONFIGURED/,
  "Sans metadata catalogue de volume, le direct reste unitaire et fail-closed.");
assert.match(catalog, /IsCartDirectEligible[\s\S]*ConfigurationPolicy/,
  "L'éligibilité directe est calculée par API-INTERNAL, pas par la carte Web.");
assert.match(catalog, /BillingV2DirectOrderingEligibilityPolicy\.Evaluate/,
  "La projection publique partage le prédicat fail-closed de commande directe.");
assert.match(cartService, /public_ordering_mode, configuration_policy/,
  "La commande directe relit publicOrderingMode et la policy de configuration.");
assert.match(cartService, /BillingV2CatalogScopeTemplatePolicy\.TryMapToCartTemplate\(scope/,
  "La commande directe normalise la portée catalogue avant de persister un CartItem.");
assert.match(cartService, /return new\("CART_SCOPE_UNSUPPORTED"\)/,
  "Un scope catalogue sans traduction déterministe est refusé avant l'insertion Cart.");
assert.match(cartService, /HasCurrentInitialPriceAsync/,
  "La commande directe exige un prix initial actif avant de créer un CartItem.");
assert.doesNotMatch(publicProjection, /resolveStorefrontDirectTariffAction/,
  "Le catalogue /tarifs ne bascule plus vers une liste codée de services directs.");
assert.match(cartService, /price\.currency = @currency/,
  "Le prix direct doit appartenir à la devise unique du Cart.");
assert.match(cartService, /component\.Currency, cart\.Currency/,
  "Le CartQuote refuse un composant d'une autre devise au lieu de l'additionner ou de l'ignorer.");
assert.match(cartService, /PlanFormulaImport[\s\S]*CART_MERGE_REQUIRES_REVIEW/,
  "L'import de formule vers un Cart déjà direct fusionne seulement les lignes non ambiguës.");
assert.ok(cartService.includes("service/tier/quantite/scope"),
  "La déduplication pack/direct repose sur une identité métier, jamais sur un montant ou un libellé.");

assert.match(cartPage, /command: "get_current"/,
  "/panier lit le Cart courant sans utiliser la commande créatrice current.");
assert.doesNotMatch(cartPage, /command: "current"/,
  "/panier ne doit pas matérialiser de Cart simplement à l'ouverture.");
assert.match(cartPage, /expectedVersion: current\.version/,
  "Les mutations de /panier restent protégées par version optimiste.");
assert.match(cartPage, /command: "update_item"/);
assert.match(cartPage, /command: "remove_item"/);
assert.match(cartPage, /command: "set_commitment"/);
assert.match(cartPage, /command: "set_payment_mode"/);
assert.match(cartPage, /command: "quote"/);
assert.match(cartPage, /const canEdit = item\.canEdit/,
  "L'édition doit être projetée par API-INTERNAL depuis le rôle courant.");
assert.match(cartPage, /const canRemove = item\.canRemove/,
  "Le retrait doit être projeté par API-INTERNAL depuis le rôle courant.");
assert.doesNotMatch(cartPage, /item\.origin !== "dependency"/,
  "La provenance historique ne doit pas bloquer seule une mutation publique.");
assert.match(cartPage, /Prix de votre panier actualisé\. Valable jusqu’au/,
  "Le récapitulatif public ne doit pas exposer Billing V2.");
assert.match(cartPage, /issue\.customerMessage/,
  "Un avertissement de readiness doit provenir de la projection publique serveur.");
assert.match(cartPage, /filter\(\(issue\) => issue\.blocking && Boolean\(issue\.customerMessage\)\)/,
  "Seul un blocage courant avec une explication serveur peut produire une alerte panier.");
assert.doesNotMatch(cartPage, /Une précision sera nécessaire avant de poursuivre\./,
  "Le panier ne doit plus afficher un avertissement vague sans action client.");
assert.doesNotMatch(cartPage, /Votre panier nécessite une modification avant de poursuivre\./,
  "Le portail ne réintroduit pas un fallback vague lorsque le serveur connaît le blocker.");
assert.match(cartPage, /BillingV2PricingSummary/,
  "Le récapitulatif financier provient de la projection de quote serveur.");
assert.match(cartPage, /quote\.lines/,
  "Les lignes financières restent celles calculées par le quote.");
assert.doesNotMatch(cartPage, /reduce\([^)]*amountCents|amountCents\s*\+/,
  "Le panier ne recompose aucun prix depuis les items affichés.");
assert.match(pricingSummary, /Prix avant remise/);
assert.match(pricingSummary, /Frais ponctuels/);
assert.doesNotMatch(cartPage, /checkout|subscription|provider|BillingEvent|PaymentAttempt|provisioning/i,
  "La page panier de Phase 3 ne contient aucun raccordement financier ou provisioning.");

assert.match(header, /command: "get_current"/);
assert.doesNotMatch(header, /command: "current"/,
  "Le badge ne crée pas de Cart à la visite du site.");
assert.match(header, /item\.countsAsCommercialSelection/,
  "Le badge doit utiliser la contribution commerciale dynamique du serveur.");
assert.doesNotMatch(header, /item\.origin !== "dependency"/,
  "Le badge ne doit pas déduire un rôle courant de la provenance historique.");
assert.match(shell, /BillingV2CartHeaderLink/);
assert.match(routes, /"\/panier"/);
assert.match(cartRoute, /"get_current"/);
assert.match(cartRoute, /!sessionToken && !anonymousToken/,
  "Sans cookie Cart, le BFF répond localement sans créer de propriétaire anonyme.");
assert.match(cartClient, /requestBffJson/);
assert.match(cartClient, /notifyBillingV2CartChanged/);

assert.match(cartModel, /BillingV2CartItemOrigins/);
assert.match(cartModel, /IsRequiredByPreset/);
assert.match(cartModel, /IsRequiredByDependency/);
assert.match(cartModel, /CountsAsCommercialSelection/);
assert.match(cartService, /ProjectItemRolesAsync/,
  "La projection de rôle doit être calculée côté API-INTERNAL.");
assert.doesNotMatch(cartService, /existing\.Origin == BillingV2CartItemOrigins\.Dependency/,
  "Une dépendance historique ne doit pas bloquer seule update_item.");
assert.match(migration, /ADD COLUMN IF NOT EXISTS origin VARCHAR\(24\) NULL/,
  "093 ajoute une provenance explicite sans toucher aux Carts existants.");
assert.doesNotMatch(cartService, /IBillingV2AuthoritativeCheckoutService|IProviderCheckout|IBillingEvent|IPaymentAttempt|IOutbox/,
  "Le service Cart reste isolé du checkout, des providers, des événements et du provisioning.");

console.log("Contrat boutique /tarifs, /panier et badge Cart vérifié.");
