"use client";

import Link from "next/link";
import { useMemo, useState } from "react";

import type {
  PublicCommercialMoney,
  PublicCommercialService,
} from "@kermaria/shared";

import {
  commandBillingV2CartClient,
  describeCartCommandFailure,
  notifyBillingV2CartChanged,
} from "@/lib/billing-v2-cart-client";
import { ASSISTANCE_SERVICE_CODE, orderAssistanceTiers } from "@/lib/assistance-offers";

type Props = {
  currency: string;
  service: PublicCommercialService;
};

/**
 * Ajout individuel volontairement sobre : le catalogue choisit les services
 * accessibles (`direct`), le navigateur transmet seulement code/palier, et
 * API-INTERNAL revalide l'ensemble avant de composer le Cart.
 */
export function BillingV2DirectCartAdd({ currency, service }: Props) {
  const selectableTiers = useMemo(
    () => {
      const tiers = service.tiers.filter((tier) => tier.selectable);
      return service.id === ASSISTANCE_SERVICE_CODE ? orderAssistanceTiers(tiers) : tiers;
    },
    [service.id, service.tiers],
  );
  const [tierCode, setTierCode] = useState(selectableTiers[0]?.id ?? "");
  const [quantityInput, setQuantityInput] = useState(String(service.directMinimumQuantity ?? 1));
  const [quantityError, setQuantityError] = useState(false);
  const isAssistancePro = service.id === "ASSISTANCE-MAINTENANCE" && ["ASSIST-PRO-START", "ASSIST-PRO-BUSINESS", "ASSIST-PRO-PREMIUM"].includes(tierCode);
  const minimumQuantity = service.directMinimumQuantity;
  const maximumQuantity = service.directMaximumQuantity;
  const quantityBoundsAvailable = minimumQuantity !== undefined && maximumQuantity !== undefined
    && Number.isInteger(minimumQuantity) && Number.isInteger(maximumQuantity)
    && minimumQuantity > 0 && maximumQuantity >= minimumQuantity;
  const [pending, setPending] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [added, setAdded] = useState(false);
  const [reviewRequired, setReviewRequired] = useState(false);
  const selectedTier = useMemo(
    () => selectableTiers.find((tier) => tier.id === tierCode) ?? null,
    [selectableTiers, tierCode],
  );

  async function addToCart() {
    const quantity = isAssistancePro ? Number(quantityInput) : 1;
    if (isAssistancePro && (!quantityBoundsAvailable || !/^\d+$/.test(quantityInput) || !Number.isInteger(quantity) || quantity < minimumQuantity || quantity > maximumQuantity)) {
      setQuantityError(true);
      return;
    }
    setQuantityError(false);
    setPending(true);
    setMessage(null);
    setReviewRequired(false);
    try {
      // `current` est appele uniquement apres le clic explicite : consulter
      // /tarifs reste sans cookie ni creation de Cart.
      const current = await commandBillingV2CartClient({ command: "current", currency });
      if (!current.ok || !current.data.cart) {
        setMessage(describeCartCommandFailure(
          current.ok ? "CART_NOT_FOUND" : current.error.code,
          "Le panier est momentanément indisponible. Réessayez dans quelques instants.",
        ));
        return;
      }
      const addedItem = await commandBillingV2CartClient({
        command: "add_item",
        cartId: current.data.cart.id,
        expectedVersion: current.data.cart.version,
        item: {
          serviceCode: service.id,
          tierCode: tierCode || null,
          quantity,
          origin: "direct",
        },
      });
      if (!addedItem.ok) {
        setReviewRequired(
          addedItem.error.code === "CART_ITEM_TIER_CONFLICT"
          || addedItem.error.code === "CART_MERGE_REQUIRES_REVIEW",
        );
        setMessage(describeCartCommandFailure(
          addedItem.error.code,
          "Ce service ne peut pas être ajouté au panier pour le moment.",
        ));
        return;
      }
      if (!addedItem.data.cart) {
        setMessage("Le panier n’a pas pu être mis à jour. Réessayez dans quelques instants.");
        return;
      }
      notifyBillingV2CartChanged();
      // Le devis Cart est toujours recalculé par Billing V2 : le prix présent
      // sur la carte est informatif et n'est jamais transmis comme autorité.
      const quoted = await commandBillingV2CartClient({
        command: "quote",
        cartId: addedItem.data.cart.id,
      });
      setAdded(true);
      setMessage(quoted.ok && quoted.data.quote?.commercialReadiness === "blocked"
        ? "Ajouté au panier. Une précision sera nécessaire avant la souscription."
        : addedItem.data.code === "CART_ITEM_ALREADY_PRESENT"
          ? "Ce service est déjà dans votre panier."
          : "Ajouté au panier.");
    } catch {
      setMessage("Le panier est momentanément indisponible. Réessayez dans quelques instants.");
    } finally {
      setPending(false);
    }
  }

  return (
    <div className="commercial-tariff-direct-add">
      {isAssistancePro && quantityBoundsAvailable ? <><label><span>Nombre de postes</span><input type="number" min={minimumQuantity} max={maximumQuantity} step={1} inputMode="numeric" value={quantityInput} onChange={(event) => { setQuantityInput(event.currentTarget.value); setQuantityError(false); }} disabled={pending} aria-invalid={quantityError} aria-describedby={quantityError ? `quantity-error-${service.id}` : undefined} /></label>{quantityError ? <p id={`quantity-error-${service.id}`} role="alert">Indiquez un nombre entier de postes entre {minimumQuantity} et {maximumQuantity}.</p> : null}</> : isAssistancePro ? <p>Les quantités disponibles sont momentanément indisponibles.</p> : null}
      {selectableTiers.length > 0 ? (
        <label>
          <span>{service.tierSelectorLabel?.trim() || "Option"}</span>
          <select
            disabled={pending}
            onChange={(event) => setTierCode(event.currentTarget.value)}
            value={tierCode}
          >
            {selectableTiers.map((tier) => (
              <option key={tier.id} value={tier.id}>{tier.label}</option>
            ))}
          </select>
        </label>
      ) : null}
      {selectedTier?.recurringPrice ? (
        <p className="commercial-tariff-direct-price">
          {formatMoney(selectedTier.recurringPrice)} TTC / mois
        </p>
      ) : null}
      <button className="button" disabled={pending || (isAssistancePro && !quantityBoundsAvailable)} onClick={() => void addToCart()} type="button">
        {pending ? "Ajout en cours…" : added ? "Ajouter à nouveau" : "Ajouter au panier"}
      </button>
      {message ? <p id={`quantity-feedback-${service.id}`} aria-live="polite" className="commercial-tariff-direct-feedback">{message} {added || reviewRequired ? <Link href="/panier">Voir mon panier</Link> : null}</p> : null}
    </div>
  );
}

function formatMoney(money: PublicCommercialMoney) {
  return new Intl.NumberFormat("fr-FR", {
    style: "currency",
    currency: money.currency,
  }).format(money.amountCents / 100);
}
