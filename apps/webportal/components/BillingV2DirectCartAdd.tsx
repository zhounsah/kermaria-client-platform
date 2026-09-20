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
    () => service.tiers.filter((tier) => tier.selectable),
    [service.tiers],
  );
  const [tierCode, setTierCode] = useState(selectableTiers[0]?.id ?? "");
  const [pending, setPending] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [added, setAdded] = useState(false);
  const [reviewRequired, setReviewRequired] = useState(false);
  const selectedTier = useMemo(
    () => selectableTiers.find((tier) => tier.id === tierCode) ?? null,
    [selectableTiers, tierCode],
  );

  async function addToCart() {
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
          quantity: 1,
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
          {formatMoney(selectedTier.recurringPrice)} / mois
        </p>
      ) : null}
      <button className="button" disabled={pending} onClick={() => void addToCart()} type="button">
        {pending ? "Ajout en cours…" : added ? "Ajouter à nouveau" : "Ajouter au panier"}
      </button>
      {message ? <p aria-live="polite" className="commercial-tariff-direct-feedback">{message} {added || reviewRequired ? <Link href="/panier">Voir mon panier</Link> : null}</p> : null}
    </div>
  );
}

function formatMoney(money: PublicCommercialMoney) {
  return new Intl.NumberFormat("fr-FR", {
    style: "currency",
    currency: money.currency,
  }).format(money.amountCents / 100);
}
