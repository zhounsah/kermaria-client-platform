"use client";

import Link from "next/link";
import { useEffect, useId, useRef, useState } from "react";
import { usePathname } from "next/navigation";

import type { CartSummary, CheckoutSummary } from "@kermaria/shared";

import { requestBffJson } from "@/lib/client-api";
import { formatCurrencyFromCents } from "@/lib/formatters";

const EMPTY_SUMMARY: CheckoutSummary = {
  cart: {
    items: [],
    itemCount: 0,
    subtotalCents: 0,
    currency: "EUR",
  },
  recurring: {
    items: [],
    itemCount: 0,
    subtotalCents: 0,
    currency: "EUR",
  },
  totalItemCount: 0,
  hasMixedCheckout: false,
};

export function HeaderCartDrawer() {
  const pathname = usePathname();
  const drawerId = useId();
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [hoverOpen, setHoverOpen] = useState(false);
  const [pinnedOpen, setPinnedOpen] = useState(false);
  const [summary, setSummary] = useState<CheckoutSummary>(EMPTY_SUMMARY);
  const [error, setError] = useState<string | null>(null);

  function releaseDrawerFocus() {
    const activeElement = document.activeElement;
    if (
      activeElement instanceof HTMLElement &&
      containerRef.current?.contains(activeElement)
    ) {
      activeElement.blur();
    }
  }

  function closePinnedDrawer() {
    setPinnedOpen(false);
    releaseDrawerFocus();
  }

  useEffect(() => {
    let ignore = false;

    async function loadSummary() {
      const result = await requestBffJson<CheckoutSummary>(
        "/api/checkout/summary",
        { method: "GET" },
      );
      if (ignore) {
        return;
      }

      if (result.ok) {
        setSummary(result.data);
        setError(null);
        return;
      }

      if (shouldFallbackToLegacyCart(result.error.code)) {
        const cartResult = await requestBffJson<CartSummary>("/api/cart", {
          method: "GET",
        });
        if (ignore) {
          return;
        }

        if (cartResult.ok) {
          setSummary(buildLegacyCheckoutSummary(cartResult.data));
          setError(null);
          return;
        }
      }

      setSummary(EMPTY_SUMMARY);
      setError(result.error.message);
    }

    void loadSummary();
    window.addEventListener("kermaria:checkout-changed", loadSummary);
    return () => {
      ignore = true;
      window.removeEventListener("kermaria:checkout-changed", loadSummary);
    };
  }, [pathname]);

  useEffect(() => {
    setHoverOpen(false);
    setPinnedOpen(false);
  }, [pathname]);

  useEffect(() => {
    if (!pinnedOpen) {
      return;
    }

    function handlePointerDown(event: PointerEvent) {
      if (!containerRef.current?.contains(event.target as Node)) {
        closePinnedDrawer();
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        closePinnedDrawer();
      }
    }

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [pinnedOpen]);

  const immediateTotal =
    summary.cart.subtotalCents + summary.recurring.subtotalCents;
  const cartPreviewItems = summary.cart.items.slice(0, 2);
  const recurringPreviewItems = summary.recurring.items.slice(0, 2);
  const open = hoverOpen || pinnedOpen;

  return (
    <div
      className={`header-cart${open ? " header-cart-open" : ""}`}
      onBlurCapture={(event) => {
        if (event.currentTarget.contains(event.relatedTarget as Node | null)) {
          return;
        }

        setHoverOpen(false);
      }}
      onFocusCapture={() => setHoverOpen(true)}
      onMouseEnter={() => setHoverOpen(true)}
      onMouseLeave={() => setHoverOpen(false)}
      ref={containerRef}
    >
      <button
        aria-controls={drawerId}
        aria-expanded={open}
        aria-label="Voir le panier"
        className="header-cart-trigger"
        onClick={() =>
          setPinnedOpen((current) => {
            if (current) {
              releaseDrawerFocus();
            }

            return !current;
          })
        }
        type="button"
      >
        <span className="header-cart-icon" aria-hidden="true">
          <svg viewBox="0 0 24 24">
            <path
              d="M7 6h14l-1.7 7.4a2 2 0 0 1-2 1.6H9.2a2 2 0 0 1-2-1.5L5 3H2"
              fill="none"
              stroke="currentColor"
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth="1.8"
            />
            <circle cx="10" cy="20" r="1.7" fill="currentColor" />
            <circle cx="18" cy="20" r="1.7" fill="currentColor" />
          </svg>
        </span>
        <span className="header-cart-label">Panier</span>
        <span className="header-cart-count">{summary.totalItemCount}</span>
      </button>

      <div
        aria-hidden="true"
        className="header-cart-hover-bridge"
      />

      <div className="header-cart-drawer" id={drawerId}>
        <div className="header-cart-drawer-banner">
          <strong>Panier unifié</strong>
          <span>{summary.totalItemCount} élément(s) en cours</span>
        </div>

        {error ? (
          <p className="header-cart-drawer-error">{error}</p>
        ) : (
          <>
            <div className="header-cart-sections">
              <section className="header-cart-section">
                <div className="header-cart-section-header">
                  <strong>Achats ponctuels</strong>
                  <span>{formatCurrencyFromCents(summary.cart.subtotalCents)}</span>
                </div>
                <p>
                  {summary.cart.itemCount > 0
                    ? `${summary.cart.itemCount} ligne(s) prêtes à être réglées en une commande.`
                    : "Aucun achat ponctuel pour l'instant."}
                </p>
                {cartPreviewItems.length > 0 ? (
                  <ul className="header-cart-preview-list">
                    {cartPreviewItems.map((item) => (
                      <li className="header-cart-preview-item" key={item.offerId}>
                        <span>{item.name}</span>
                        <strong>{formatCurrencyFromCents(item.lineTotalCents)}</strong>
                      </li>
                    ))}
                  </ul>
                ) : null}
              </section>

              <section className="header-cart-section">
                <div className="header-cart-section-header">
                  <strong>Abonnements récurrents</strong>
                  <span>{formatCurrencyFromCents(summary.recurring.subtotalCents)}</span>
                </div>
                <p>
                  {summary.recurring.itemCount > 0
                    ? `${summary.recurring.itemCount} abonnement(s) avec facture de premier terme.`
                    : "Aucun abonnement récurrent sélectionné."}
                </p>
                {recurringPreviewItems.length > 0 ? (
                  <ul className="header-cart-preview-list">
                    {recurringPreviewItems.map((item) => (
                      <li className="header-cart-preview-item" key={item.offerId}>
                        <span>{item.name}</span>
                        <strong>
                          {formatCurrencyFromCents(item.firstChargeAmountCents)}
                        </strong>
                      </li>
                    ))}
                  </ul>
                ) : null}
              </section>
            </div>

            <div className="header-cart-drawer-total">
              <span>Total estimatif immédiat</span>
              <strong>{formatCurrencyFromCents(immediateTotal)}</strong>
            </div>

            <div className="header-cart-actions">
              <Link className="button button-ghost" href="/souscrire">
                Continuer
              </Link>
              <Link className="button" href="/panier">
                Voir le panier
              </Link>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function buildLegacyCheckoutSummary(cart: CartSummary): CheckoutSummary {
  return {
    cart,
    recurring: EMPTY_SUMMARY.recurring,
    totalItemCount: cart.itemCount,
    hasMixedCheckout: false,
  };
}

function shouldFallbackToLegacyCart(code: string) {
  return [
    "ROUTE_NOT_FOUND",
    "SQL_UNAVAILABLE",
    "INTERNAL_ERROR",
    "INTERNAL_API_UNAVAILABLE",
    "INVALID_INTERNAL_RESPONSE",
  ].includes(code);
}
