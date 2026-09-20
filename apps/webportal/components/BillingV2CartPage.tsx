"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import type {
  BillingV2Cart,
  BillingV2CartCommandRequest,
  BillingV2CartItem,
  BillingV2CartQuote,
  BillingV2PublicCatalog,
  BillingV2PublicService,
  BillingV2PublicTier,
} from "@kermaria/shared";

import {
  commandBillingV2CartClient,
  describeCartCommandFailure,
  notifyBillingV2CartChanged,
} from "@/lib/billing-v2-cart-client";
import {
  formatCommitmentDurationLabel,
  resolveServicePublicLabel,
} from "@/lib/billing-v2-formules";
import { BillingV2PricingSummary } from "@/components/BillingV2PricingSummary";

type Props = {
  catalog: BillingV2PublicCatalog;
};

type PageState = "loading" | "empty" | "ready" | "expired" | "unavailable";

/**
 * Façade publique du Cart. Elle ne crée jamais de panier au montage :
 * `get_current` est une lecture sans touch ni cookie Cart. Toutes les
 * décisions de tier, quantité, suppression, engagement et règlement restent
 * réévaluées par API-INTERNAL et reviennent ici sous forme de Cart/Quote.
 */
export function BillingV2CartPage({ catalog }: Props) {
  const [pageState, setPageState] = useState<PageState>("loading");
  const [cart, setCart] = useState<BillingV2Cart | null>(null);
  const [quote, setQuote] = useState<BillingV2CartQuote | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pendingAction, setPendingAction] = useState<string | null>(null);
  const cartRef = useRef<BillingV2Cart | null>(null);

  useEffect(() => {
    cartRef.current = cart;
  }, [cart]);

  const refreshQuote = useCallback(async (cartId: string) => {
    const result = await commandBillingV2CartClient({ command: "quote", cartId });
    if (!result.ok) {
      setError(describeCartCommandFailure(
        result.error.code,
        "Le récapitulatif du panier n’a pas pu être recalculé.",
      ));
      return;
    }
    if (result.data.cart) {
      setCart(result.data.cart);
    }
    setQuote(result.data.quote);
  }, []);

  const loadCurrent = useCallback(async () => {
    setPageState("loading");
    setError(null);
    const result = await commandBillingV2CartClient({
      command: "get_current",
      currency: catalog.currency,
    });
    if (!result.ok) {
      if (result.error.code === "CART_NOT_FOUND") {
        setCart(null);
        setQuote(null);
        setPageState("empty");
        return;
      }
      setPageState("unavailable");
      setError(describeCartCommandFailure(
        result.error.code,
        "Le panier est momentanément indisponible.",
      ));
      return;
    }

    if (result.data.code === "CART_NOT_FOUND" || !result.data.cart) {
      setCart(null);
      setQuote(null);
      setPageState("empty");
      return;
    }
    if (result.data.code === "CART_EXPIRED" || result.data.cart.status === "expired") {
      setCart(null);
      setQuote(null);
      setPageState("expired");
      return;
    }

    setCart(result.data.cart);
    if (result.data.cart.items.length === 0) {
      setQuote(null);
      setPageState("empty");
      return;
    }
    setPageState("ready");
    await refreshQuote(result.data.cart.id);
  }, [catalog.currency, refreshQuote]);

  useEffect(() => {
    let disposed = false;
    // Le montage reste en état loading ; la lecture est seulement planifiée
    // après l'installation de l'effet, ce qui évite un rendu en cascade et
    // permet au cleanup du Strict Mode d'annuler son premier passage.
    queueMicrotask(() => {
      if (!disposed) void loadCurrent();
    });
    return () => {
      disposed = true;
    };
  }, [loadCurrent]);

  const mutate = useCallback(async (
    action: string,
    createRequest: (current: BillingV2Cart) => BillingV2CartCommandRequest,
    successMessage?: string,
  ) => {
    const current = cartRef.current;
    if (!current) return;
    setPendingAction(action);
    setError(null);
    setNotice(null);
    try {
      const result = await commandBillingV2CartClient(createRequest(current));
      if (!result.ok) {
        const message = describeCartCommandFailure(
          result.error.code,
          "Cette modification n’a pas pu être appliquée au panier.",
        );
        setError(message);
        if (result.error.code === "CART_VERSION_CONFLICT") {
          await loadCurrent();
        }
        return;
      }
      if (!result.data.cart) {
        setError("Le panier n’a pas pu être mis à jour. Réessayez dans quelques instants.");
        return;
      }
      setCart(result.data.cart);
      setPageState(result.data.cart.items.length === 0 ? "empty" : "ready");
      notifyBillingV2CartChanged();
      setNotice(successMessage ?? "Votre panier a été mis à jour.");
      await refreshQuote(result.data.cart.id);
    } finally {
      setPendingAction(null);
    }
  }, [loadCurrent, refreshQuote]);

  const serviceByCode = useMemo(() => new Map(
    catalog.services.map((service) => [service.code, service]),
  ), [catalog.services]);

  if (pageState === "loading") {
    return (
      <section className="cart-storefront cart-storefront-loading" aria-live="polite">
        <p>Chargement de votre panier…</p>
      </section>
    );
  }

  if (pageState === "unavailable") {
    return (
      <section className="cart-storefront cart-storefront-empty" aria-labelledby="cart-title">
        <h1 id="cart-title">Votre panier</h1>
        <p>{error ?? "Le panier est momentanément indisponible."}</p>
        <button className="button button-secondary" onClick={() => void loadCurrent()} type="button">
          Réessayer
        </button>
      </section>
    );
  }

  if (pageState === "expired") {
    return (
      <section className="cart-storefront cart-storefront-empty" aria-labelledby="cart-title">
        <h1 id="cart-title">Votre panier a expiré</h1>
        <p>Votre ancienne sélection n’est plus active. Vous pouvez reprendre votre choix dans les tarifs ou les offres.</p>
        <CartDiscoveryActions />
      </section>
    );
  }

  if (!cart || pageState === "empty") {
    return (
      <section className="cart-storefront cart-storefront-empty" aria-labelledby="cart-title">
        <h1 id="cart-title">Votre panier est vide</h1>
        <p>Ajoutez un service à la carte ou personnalisez une offre pour retrouver votre sélection ici.</p>
        <CartDiscoveryActions />
      </section>
    );
  }

  const commercialItems = cart.items.filter((item) => !item.isStructural);
  const structuralItems = cart.items.filter((item) => item.isStructural);
  const selectedCommitment = catalog.commitments.find(
    (commitment) => commitment.code === cart.commitmentCode,
  ) ?? null;
  const paymentOptions = selectedCommitment?.paymentOptions ?? [];

  return (
    <section className="cart-storefront" aria-labelledby="cart-title">
      <header className="cart-storefront-heading">
        <p className="eyebrow">Votre sélection</p>
        <h1 id="cart-title">Votre panier</h1>
        <p>Vérifiez vos services et vos options. La souscription sera disponible à l’étape suivante.</p>
      </header>

      {notice ? <p className="cart-storefront-notice" aria-live="polite">{notice}</p> : null}
      {error ? <p className="cart-storefront-error" aria-live="assertive">{error}</p> : null}

      <div className="cart-storefront-layout">
        <div className="cart-storefront-items">
          {commercialItems.map((item) => (
            <CartItemCard
              cart={cart}
              item={item}
              key={item.id}
              pending={pendingAction !== null}
              service={serviceByCode.get(item.serviceCode)}
              onRemove={() => void mutate(`remove:${item.id}`, (current) => ({
                command: "remove_item",
                cartId: current.id,
                itemId: item.id,
                expectedVersion: current.version,
              }), "Le service a été retiré du panier.")}
              onTierChange={(tierCode) => void mutate(`tier:${item.id}`, (current) => updateItemRequest(current, item, {
                tierCode,
                quantity: item.quantity,
              }), "Le palier a été mis à jour.")}
              onQuantityChange={(quantity) => void mutate(`quantity:${item.id}`, (current) => updateItemRequest(current, item, {
                tierCode: item.tierCode,
                quantity,
              }), "La quantité a été mise à jour.")}
            />
          ))}

          {structuralItems.length > 0 ? (
            <details className="cart-storefront-included">
              <summary>Inclus dans votre abonnement</summary>
              <ul>
                {structuralItems.map((item) => (
                  <li key={item.id}>{publicItemLabel(item, serviceByCode.get(item.serviceCode))}</li>
                ))}
              </ul>
            </details>
          ) : null}

          <section className="cart-storefront-settings" aria-labelledby="cart-commitment-title">
            <fieldset>
              <legend id="cart-commitment-title">Engagement</legend>
              <p>La durée s’applique à l’ensemble du panier.</p>
              <div className="cart-storefront-choice-list">
                {catalog.commitments.map((commitment) => (
                  <label key={commitment.code}>
                    <input
                      checked={cart.commitmentCode === commitment.code}
                      disabled={pendingAction !== null}
                      name="cart-commitment"
                      onChange={() => void mutate("commitment", (current) => ({
                        command: "set_commitment",
                        cartId: current.id,
                        expectedVersion: current.version,
                        commitmentCode: commitment.code,
                      }), "La durée d’engagement a été mise à jour.")}
                      type="radio"
                      value={commitment.code}
                    />
                    <span>{formatCommitmentDurationLabel(commitment.months, commitment.name)}</span>
                  </label>
                ))}
              </div>
            </fieldset>

            <fieldset>
              <legend>Mode de paiement</legend>
              {selectedCommitment ? (
                <div className="cart-storefront-choice-list">
                  {paymentOptions.map((option) => (
                    <label key={option.paymentMode}>
                      <input
                        checked={cart.paymentMode === option.paymentMode}
                        disabled={pendingAction !== null}
                        name="cart-payment-mode"
                        onChange={() => void mutate("payment-mode", (current) => ({
                          command: "set_payment_mode",
                          cartId: current.id,
                          expectedVersion: current.version,
                          paymentMode: option.paymentMode,
                        }), "Le mode de paiement a été mis à jour.")}
                        type="radio"
                        value={option.paymentMode}
                      />
                      <span>{option.paymentMode === "upfront" ? "Paiement en une fois" : "Mensuel"}</span>
                    </label>
                  ))}
                </div>
              ) : (
                <p>Choisissez d’abord une durée d’engagement pour voir les modes de paiement disponibles.</p>
              )}
            </fieldset>
          </section>
        </div>

        <CartQuoteSummary quote={quote} />
      </div>
    </section>
  );
}

function CartItemCard({
  cart,
  item,
  pending,
  service,
  onTierChange,
  onQuantityChange,
  onRemove,
}: {
  cart: BillingV2Cart;
  item: BillingV2CartItem;
  pending: boolean;
  service: BillingV2PublicService | undefined;
  onTierChange: (tierCode: string) => void;
  onQuantityChange: (quantity: number) => void;
  onRemove: () => void;
}) {
  const canEdit = item.canEdit;
  const canRemove = item.canRemove;
  const tiers = availableTiers(cart, item, service);
  const canChangeTier = canEdit && item.tierCode !== null && tiers.length > 1;
  const canChangeQuantity = canEdit && item.minimumQuantity < item.maximumQuantity;
  const label = publicItemLabel(item, service);
  const tier = item.tierCode
    ? tiers.find((candidate) => candidate.code === item.tierCode)
      ?? service?.tiers.find((candidate) => candidate.code === item.tierCode)
    : null;

  return (
    <article className="cart-storefront-item">
      <div className="cart-storefront-item-main">
        <div>
          <h2>{label}</h2>
          {tier ? <p className="cart-storefront-item-detail">{tier.label}</p> : null}
          {item.displayReason === "included_with_offer" ? <p className="cart-storefront-item-linked">Inclus avec cette offre.</p> : null}
          {item.displayReason === "required_for_selection" ? <p className="cart-storefront-item-linked">Requis par votre sélection actuelle.</p> : null}
        </div>
      </div>

      {canChangeTier ? (
        <label className="cart-storefront-control">
          <span>{service?.tierSelectorLabel?.trim() || "Option"}</span>
          <select
            disabled={pending}
            onChange={(event) => onTierChange(event.currentTarget.value)}
            value={item.tierCode ?? ""}
          >
            {tiers.map((candidate) => (
              <option key={candidate.code} value={candidate.code}>{candidate.label}</option>
            ))}
          </select>
        </label>
      ) : null}

      {canChangeQuantity ? (
        <div className="cart-storefront-quantity" aria-label={`Quantité pour ${label}`}>
          <span>Quantité</span>
          <div>
            <button
              aria-label={`Réduire la quantité de ${label}`}
              disabled={pending || item.quantity <= item.minimumQuantity}
              onClick={() => onQuantityChange(item.quantity - 1)}
              type="button"
            >
              −
            </button>
            <output aria-live="polite">{item.quantity}</output>
            <button
              aria-label={`Augmenter la quantité de ${label}`}
              disabled={pending || item.quantity >= item.maximumQuantity}
              onClick={() => onQuantityChange(item.quantity + 1)}
              type="button"
            >
              +
            </button>
          </div>
        </div>
      ) : null}

      {canRemove ? (
        <button className="cart-storefront-remove" disabled={pending} onClick={onRemove} type="button">
          Retirer
        </button>
      ) : null}
    </article>
  );
}

function CartQuoteSummary({ quote }: { quote: BillingV2CartQuote | null }) {
  const blockers = quote ? [
    ...quote.dependencyIssues,
    ...quote.scopeIssues,
    ...quote.configurationIssues,
  ].filter((issue) => issue.blocking && Boolean(issue.customerMessage)) : [];

  return (
    <aside className="cart-storefront-summary" aria-live="polite" aria-labelledby="cart-summary-title">
      <h2 id="cart-summary-title">Récapitulatif</h2>
      {!quote ? <p>Calcul du récapitulatif…</p> : (
        <>
          <BillingV2PricingSummary
            currency={quote.currency}
            lines={quote.lines.map((line) => ({
              id: `${line.cartItemId}-${line.servicePriceId}`,
              label: resolveServicePublicLabel(line.serviceCode, line.label || "Service inclus"),
              detail: line.detail,
              quantity: line.quantity,
              amountCents: line.amountCents,
            }))}
            oneTimeDueNowCents={quote.oneTimeDueNowCents}
            recurringDiscountCents={quote.recurringDiscountCents}
            recurringSubtotalCents={quote.recurringSubtotalCents}
            recurringTotalCents={quote.recurringTotalCents}
            totalDueNowCents={quote.totalDueNowCents}
          />
          {blockers.length > 0 ? (
            <div className="cart-storefront-summary-warning" role="alert">
              <p>Avant de poursuivre :</p>
              <ul>
                {blockers.map((issue) => (
                  <li key={`${issue.code}-${issue.cartItemId ?? "cart"}`}>
                    {issue.customerMessage}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
          <p className="cart-storefront-quote-expiry">Prix de votre panier actualisé. Valable jusqu’au {formatDateTime(quote.expiresAtUtc)}.</p>
        </>
      )}
      <button className="button" disabled type="button">Passer à la souscription</button>
      <p className="cart-storefront-next-step">Disponible à l’étape suivante.</p>
      <Link className="button button-secondary" href="/tarifs">Continuer mes achats</Link>
    </aside>
  );
}

function CartDiscoveryActions() {
  return (
    <div className="cart-storefront-discovery-actions">
      <Link className="button" href="/tarifs">Découvrir les tarifs</Link>
      <Link className="button button-secondary" href="/offres">Voir les offres</Link>
    </div>
  );
}

function updateItemRequest(
  cart: BillingV2Cart,
  item: BillingV2CartItem,
  changes: { tierCode: string | null; quantity: number },
): BillingV2CartCommandRequest {
  return {
    command: "update_item",
    cartId: cart.id,
    itemId: item.id,
    expectedVersion: cart.version,
    item: {
      serviceCode: item.serviceCode,
      tierCode: changes.tierCode,
      quantity: changes.quantity,
      scopeTemplate: item.scopeTemplate,
      subjectBinding: item.subjectBinding,
      ...(item.sourcePresetItemId ? {
        origin: "preset" as const,
        sourcePresetId: cart.sourcePresetId,
        sourcePresetItemId: item.sourcePresetItemId,
      } : { origin: "direct" as const }),
    },
  };
}

function availableTiers(
  cart: BillingV2Cart,
  item: BillingV2CartItem,
  service: BillingV2PublicService | undefined,
): BillingV2PublicTier[] {
  if (!service) return [];
  if (!item.sourcePresetItemId) {
    return service.tiers.filter((tier) => tier.publicSelectable);
  }
  const permitted = new Set(
    cart.presetDefinition
      ?.filter((definition) => definition.serviceCode === item.serviceCode
        && definition.scopeTemplate === item.scopeTemplate
        && definition.customerEditable
        && definition.tierCode !== null)
      .map((definition) => definition.tierCode) ?? [],
  );
  return service.tiers.filter((tier) => tier.publicSelectable && permitted.has(tier.code));
}

function publicItemLabel(item: BillingV2CartItem, service: BillingV2PublicService | undefined) {
  return resolveServicePublicLabel(item.serviceCode, service?.name ?? "Service inclus");
}

function formatDateTime(value: string) {
  const instant = new Date(value);
  const date = new Intl.DateTimeFormat("fr-FR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(instant);
  const time = new Intl.DateTimeFormat("fr-FR", {
    hour: "2-digit",
    minute: "2-digit",
  }).format(instant);
  return `${date} à ${time}`;
}
