"use client";

import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import type { CheckoutRecurringConfirmResponse } from "@kermaria/shared";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

export function RecurringCheckoutConfirmButton() {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleConfirm() {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setError(null);

    const result = await requestBffJson<CheckoutRecurringConfirmResponse>(
      "/api/checkout/subscriptions/confirm",
      { method: "POST" },
    );

    if (result.ok) {
      window.dispatchEvent(new Event("kermaria:checkout-changed"));
      router.push(`/commercial-documents/${result.data.documentId}`);
    } else {
      setError(result.error.message);
      isSubmittingRef.current = false;
      setIsSubmitting(false);
    }
  }

  return (
    <div className="workflow-form">
      {error ? (
        <FormMessage title="Impossible de confirmer" tone="error">
          <p>{error}</p>
        </FormMessage>
      ) : null}
      <SubmitButton
        className="button"
        idleLabel="Confirmer mes abonnements"
        isSubmitting={isSubmitting}
        submittingLabel="Création de la facture..."
        onClick={handleConfirm}
        type="button"
      />
    </div>
  );
}
