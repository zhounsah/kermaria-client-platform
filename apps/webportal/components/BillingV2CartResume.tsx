"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import type { BillingV2CartCommandResponse } from "@kermaria/shared";
import { requestBffJson } from "@/lib/client-api";

/** Reprise Cart après login : le token anonyme est claimé par le BFF/API,
 * puis cette vue ne transporte qu'un code de preset public dans l'URL. */
export function BillingV2CartResume() {
  const router = useRouter();
  useEffect(() => {
    void (async () => {
      // Idempotent : sans cookie anonyme (ou après claim), API-INTERNAL
      // répond CART_NOT_FOUND et la reprise continue vers le Cart client.
      await requestBffJson<BillingV2CartCommandResponse>("/api/billing-v2/cart", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ command: "claim_current" }),
      });
      const result = await requestBffJson<BillingV2CartCommandResponse>("/api/billing-v2/cart", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ command: "current", currency: "EUR" }),
      });
      const code = result.ok ? result.data.cart?.sourcePresetCode : null;
      router.replace(code ? `/formules/${encodeURIComponent(code)}` : "/formules");
    })();
  }, [router]);
  return <p className="formules-empty">Reprise de votre configuration…</p>;
}
