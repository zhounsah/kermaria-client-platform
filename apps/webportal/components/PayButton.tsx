"use client";

import { useRef, useState } from "react";

import { SubmitButton } from "@/components/SubmitButton";
import { FormMessage } from "@/components/FormMessage";
import { requestBffJson } from "@/lib/client-api";

type PayButtonProps = {
  documentId: string;
  paypalEnabled: boolean;
  stripeEnabled: boolean;
};

type CreatePaymentResponse = {
  approveUrl: string;
};

export function PayButton({
  documentId,
  paypalEnabled,
  stripeEnabled,
}: PayButtonProps) {
  const isSubmittingRef = useRef(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [rail, setRail] = useState<"paypal" | "stripe">(
    stripeEnabled ? "stripe" : "paypal",
  );
  const showRailChoice = paypalEnabled && stripeEnabled;

  async function handlePay() {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setError(null);

    const result = await requestBffJson<CreatePaymentResponse>(
      rail === "stripe"
        ? "/api/payments/stripe/create-intent"
        : "/api/payment/paypal/create",
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
      {showRailChoice ? (
        <div className="field" style={{ marginBottom: "0.75rem" }} role="radiogroup" aria-label="Rail de paiement">
          <label style={{ marginRight: "1rem" }}>
            <input
              checked={rail === "stripe"}
              name="payment-rail"
              onChange={() => setRail("stripe")}
              type="radio"
              value="stripe"
            />{" "}
            Stripe
          </label>
          <label>
            <input
              checked={rail === "paypal"}
              name="payment-rail"
              onChange={() => setRail("paypal")}
              type="radio"
              value="paypal"
            />{" "}
            PayPal
          </label>
        </div>
      ) : null}
      <SubmitButton
        className="button"
        idleLabel={rail === "stripe" ? "Payer via Stripe" : "Payer via PayPal"}
        isSubmitting={isSubmitting}
        submittingLabel="Redirection vers le paiement…"
        onClick={handlePay}
        type="button"
      />
    </div>
  );
}
