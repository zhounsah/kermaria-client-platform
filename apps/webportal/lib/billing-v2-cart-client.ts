import type {
  BillingV2CartCheckoutRequest,
  BillingV2CartCheckoutResponse,
  BillingV2CartCheckoutStatusResponse,
  BillingV2CartCommandRequest,
  BillingV2CartCommandResponse,
} from "@kermaria/shared";

import { requestBffJson } from "@/lib/client-api";

/**
 * Point d'entree client unique des commandes Cart. `requestBffJson` ajoute le
 * jeton CSRF commun a toute mutation POST ; aucun composant boutique ne fait
 * de fetch direct ni ne connait les details de session/cookie.
 */
export function commandBillingV2CartClient(request: BillingV2CartCommandRequest) {
  return requestBffJson<BillingV2CartCommandResponse>("/api/billing-v2/cart", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
}

/** La confirmation ne contient qu'une référence de quote, jamais un montant. */
export function checkoutBillingV2CartClient(request: BillingV2CartCheckoutRequest) {
  return requestBffJson<BillingV2CartCheckoutResponse>("/api/billing-v2/cart/checkout", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
}

/** La reprise ne declenche aucune mutation Cart, provider ou outbox. */
export function getBillingV2CartCheckoutStatusClient(cartId?: string | null) {
  const suffix = cartId ? `?cartId=${encodeURIComponent(cartId)}` : "";
  return requestBffJson<BillingV2CartCheckoutStatusResponse>(
    `/api/billing-v2/cart/checkout-status${suffix}`,
    { method: "GET" },
  );
}

/**
 * Le header est persistant dans le shell public. Cette notification locale ne
 * transporte aucun Cart ni montant : elle lui demande seulement de relire le
 * compteur depuis le BFF après une mutation déjà confirmée par le serveur.
 */
export function notifyBillingV2CartChanged() {
  window.dispatchEvent(new Event("billing-v2-cart-changed"));
}

export function describeCartCommandFailure(code: string, fallback: string) {
  switch (code) {
    case "CART_VERSION_CONFLICT":
      return "Votre panier a été modifié dans un autre onglet. Nous avons chargé sa dernière version.";
    case "CART_ITEM_ALREADY_PRESENT":
      return "Ce service est déjà dans votre panier.";
    case "CART_ITEM_TIER_CONFLICT":
    case "CART_MERGE_REQUIRES_REVIEW":
      return "Ce service est déjà présent avec une autre configuration. Vous pouvez le vérifier dans votre panier.";
    case "CART_DEPENDENCY_REQUIRED":
    case "CART_STRUCTURAL_ITEM_REQUIRED":
    case "CART_PRESET_ITEM_REQUIRED":
      return "Cet élément est nécessaire à la configuration actuelle et ne peut pas être retiré seul.";
    case "CART_NOT_FOUND":
    case "CART_EXPIRED":
      return "Votre ancien panier a expiré. Vous pouvez recommencer votre sélection.";
    case "SQL_UNAVAILABLE":
    case "INTERNAL_API_UNAVAILABLE":
      return "Le panier est momentanément indisponible. Réessayez dans quelques instants.";
    case "CART_DIRECT_NOT_ELIGIBLE":
      return "Ce service n’est pas disponible à l’achat individuel.";
    case "CART_DIRECT_QUANTITY_NOT_CONFIGURED":
      return "La quantité de ce service est définie dans votre panier lorsqu’une option de volume est disponible.";
    case "CART_DIRECT_CONFIGURATION_REQUIRED":
    case "CART_CONFIGURATION_REQUIRED":
    case "CART_CONFIGURATION_AFFECTS_PRICE":
      return "Ce service nécessite une configuration complémentaire avant de pouvoir être ajouté.";
    default:
      return fallback;
  }
}
