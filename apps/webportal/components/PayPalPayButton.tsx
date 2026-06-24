"use client";

import { useRef, useState } from "react";

import { SubmitButton } from "@/components/SubmitButton";
import { FormMessage } from "@/components/FormMessage";
import { requestBffJson } from "@/lib/client-api";

type PayPalPayButtonProps = {
  documentId: string;
};

type CreateOrderResponse = {
  orderId: string;
  approveUrl: string;
};

export function PayPalPayButton({ documentId }: PayPalPayButtonProps) {
  const isSubmittingRef = useRef(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handlePay() {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setError(null);

    const result = await requestBffJson<CreateOrderResponse>(
      "/api/payment/paypal/create",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ documentId }),
      },
    );

    if (result.ok) {
      window.location.href = result.data.approveUrl;
    } else {
      setError(result.error.message);
      isSubmittingRef.current = false;
      setIsSubmitting(false);
    }
  }

  return (
    <div className="workflow-form">
      {error ? (
        <FormMessage title="Échec" tone="error">
          <p>{error}</p>
        </FormMessage>
      ) : null}
      <SubmitButton
        className="button"
        idleLabel="Payer via PayPal"
        isSubmitting={isSubmitting}
        submittingLabel="Redirection vers PayPal…"
        onClick={handlePay}
        type="button"
      />
    </div>
  );
}
