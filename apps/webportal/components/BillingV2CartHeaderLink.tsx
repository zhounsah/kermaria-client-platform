"use client";

import Link from "next/link";
import { ShoppingBag } from "lucide-react";
import { useCallback, useEffect, useState } from "react";

import { commandBillingV2CartClient } from "@/lib/billing-v2-cart-client";

type Props = {
  onNavigate?: () => void;
};

/**
 * Compteur de lecture seule. Sans cookie Cart, le BFF répond localement
 * CART_NOT_FOUND : le header ne crée ni Cart, ni token opaque, ni session.
 */
export function BillingV2CartHeaderLink({ onNavigate }: Props) {
  const [count, setCount] = useState<number | null>(null);

  const refresh = useCallback(async () => {
    const result = await commandBillingV2CartClient({
      command: "get_current",
      currency: "EUR",
    });
    if (!result.ok || !result.data.cart || result.data.code === "CART_EXPIRED") {
      setCount(null);
      return;
    }
    // API-INTERNAL résout la contribution commerciale depuis la composition
    // courante. La provenance historique ne suffit pas à compter une ligne.
    const next = result.data.cart.items.filter((item) => item.countsAsCommercialSelection).length;
    setCount(next > 0 ? next : null);
  }, []);

  useEffect(() => {
    let disposed = false;
    // Décaler la première lecture évite une écriture d'état synchrone pendant
    // l'installation de l'effet React, tout en restant une lecture seule.
    queueMicrotask(() => {
      if (!disposed) void refresh();
    });
    window.addEventListener("billing-v2-cart-changed", refresh);
    return () => {
      disposed = true;
      window.removeEventListener("billing-v2-cart-changed", refresh);
    };
  }, [refresh]);

  return (
    <Link className="public-header-cart" href="/panier" onClick={onNavigate}>
      <ShoppingBag aria-hidden="true" size={18} strokeWidth={1.8} />
      <span>Panier</span>
      {count ? <span className="public-header-cart-count">{count}</span> : null}
    </Link>
  );
}
