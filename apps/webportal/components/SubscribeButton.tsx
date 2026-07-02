"use client";

import { useState } from "react";

import { requestBffJson } from "@/lib/client-api";

type SubscribeResponse = {
  subscriptionId: string | null;
  approveUrl: string;
};

type SubscribeButtonProps = {
  offerId: string;
  offerName: string;
  paypalEnabled: boolean;
  stripeEnabled: boolean;
};

export function SubscribeButton({
  offerId,
  offerName,
  paypalEnabled,
  stripeEnabled,
}: SubscribeButtonProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [rail, setRail] = useState<"paypal" | "stripe">(
    stripeEnabled ? "stripe" : "paypal",
  );
  const showRailChoice = paypalEnabled && stripeEnabled;

  async function handleClick() {
    if (isSubmitting) {
      return;
    }

    setIsSubmitting(true);
    setError(null);

    const result = await requestBffJson<SubscribeResponse>(
      "/api/subscriptions/create",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ offerId, rail }),
      },
    );

    if (result.ok) {
      window.location.assign(result.data.approveUrl);
      return;
    }

    setError(result.error.message);
    setIsSubmitting(false);
  }

  return (
    <div>
      {showRailChoice ? (
        <div role="radiogroup" aria-label="Rail de paiement" style={{ marginBottom: 6 }}>
          <label style={{ marginRight: "1rem" }}>
            <input
              checked={rail === "stripe"}
              name={`payment-rail-${offerId}`}
              onChange={() => setRail("stripe")}
              type="radio"
              value="stripe"
            />{" "}
            Stripe
          </label>
          <label>
            <input
              checked={rail === "paypal"}
              name={`payment-rail-${offerId}`}
              onChange={() => setRail("paypal")}
              type="radio"
              value="paypal"
            />{" "}
            PayPal
          </label>
        </div>
      ) : null}
      <button
        className="button"
        disabled={isSubmitting}
        onClick={handleClick}
        type="button"
      >
        {isSubmitting
          ? "Redirection..."
          : `Souscrire à ${offerName}`}
      </button>
      {error ? (
        <p
          className="field-hint"
          role="alert"
          style={{ marginTop: 6, color: "var(--danger)" }}
        >
          {error}
        </p>
      ) : null}
    </div>
  );
}
