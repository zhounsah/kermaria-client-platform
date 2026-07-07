"use client";

import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import type { CartMutationResponse } from "@kermaria/shared";
import { requestBffJson } from "@/lib/client-api";

type AddToCartButtonProps = {
  offerId: string;
  label?: string;
};

export function AddToCartButton({ offerId, label }: AddToCartButtonProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [added, setAdded] = useState(false);

  async function handleAdd() {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setError(null);

    const result = await requestBffJson<CartMutationResponse>(
      "/api/cart/items",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ offerId, quantity: 1 }),
      },
    );

    isSubmittingRef.current = false;
    setIsSubmitting(false);

    if (result.ok) {
      setAdded(true);
      router.refresh();
      window.setTimeout(() => setAdded(false), 2500);
    } else {
      setError(result.error.message);
    }
  }

  return (
    <div>
      {error ? (
        <FormMessage title="Impossible d'ajouter" tone="error">
          <p>{error}</p>
        </FormMessage>
      ) : null}
      <SubmitButton
        className="button"
        idleLabel={added ? "Ajouté ✓" : (label ?? "Ajouter au panier")}
        isSubmitting={isSubmitting}
        submittingLabel="Ajout…"
        onClick={handleAdd}
        type="button"
      />
    </div>
  );
}
