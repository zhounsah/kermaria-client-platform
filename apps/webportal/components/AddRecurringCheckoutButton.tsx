"use client";

import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import type { CheckoutRecurringMutationResponse } from "@kermaria/shared";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type AddRecurringCheckoutButtonProps = {
  offerId: string;
  label?: string;
};

export function AddRecurringCheckoutButton({
  offerId,
  label = "Ajouter au panier",
}: AddRecurringCheckoutButtonProps) {
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

    const result = await requestBffJson<CheckoutRecurringMutationResponse>(
      "/api/checkout/subscriptions/items",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ offerId }),
      },
    );

    isSubmittingRef.current = false;
    setIsSubmitting(false);

    if (result.ok) {
      setAdded(true);
      window.dispatchEvent(new Event("kermaria:checkout-changed"));
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
        idleLabel={added ? "Ajouté au panier" : label}
        isSubmitting={isSubmitting}
        submittingLabel="Ajout..."
        onClick={handleAdd}
        type="button"
      />
    </div>
  );
}
