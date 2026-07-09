"use client";

import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import type { CheckoutRecurringMutationResponse } from "@kermaria/shared";

import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type RecurringCheckoutItemRemoveButtonProps = {
  offerId: string;
};

export function RecurringCheckoutItemRemoveButton({
  offerId,
}: RecurringCheckoutItemRemoveButtonProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleRemove() {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    setIsSubmitting(true);

    const result = await requestBffJson<CheckoutRecurringMutationResponse>(
      "/api/checkout/subscriptions/items/remove",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ offerId }),
      },
    );

    if (result.ok) {
      window.dispatchEvent(new Event("kermaria:checkout-changed"));
      router.refresh();
    } else {
      isSubmittingRef.current = false;
      setIsSubmitting(false);
    }
  }

  return (
    <SubmitButton
      className="button button-ghost"
      idleLabel="Retirer"
      isSubmitting={isSubmitting}
      submittingLabel="..."
      onClick={handleRemove}
      type="button"
    />
  );
}
