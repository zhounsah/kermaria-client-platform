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
        <fieldset className="payment-rail-group">
          <legend>Choisir le mode de paiement</legend>
          <div
            aria-label="Rail de paiement"
            className="payment-rail-options"
            role="radiogroup"
          >
            <label
              className={
                rail === "stripe"
                  ? "payment-rail-option payment-rail-option-active"
                  : "payment-rail-option"
              }
            >
              <input
                checked={rail === "stripe"}
                className="visually-hidden"
                name={`payment-rail-${offerId}`}
                onChange={() => setRail("stripe")}
                type="radio"
                value="stripe"
              />
              <span className="payment-rail-title">Carte bancaire</span>
              <span className="payment-rail-hint">Paiement via Stripe</span>
            </label>
            <label
              className={
                rail === "paypal"
                  ? "payment-rail-option payment-rail-option-active"
                  : "payment-rail-option"
              }
            >
              <input
                checked={rail === "paypal"}
                className="visually-hidden"
                name={`payment-rail-${offerId}`}
                onChange={() => setRail("paypal")}
                type="radio"
                value="paypal"
              />
              <span className="payment-rail-title">PayPal</span>
              <span className="payment-rail-hint">Paiement sur compte PayPal</span>
            </label>
          </div>
        </fieldset>
      ) : null}
      <button
        className="button"
        disabled={isSubmitting}
        onClick={handleClick}
        type="button"
      >
        {isSubmitting ? "Redirection..." : `Souscrire à ${offerName}`}
      </button>
      {error ? (
        <p className="payment-inline-error" role="alert">
          {error}
        </p>
      ) : null}
    </div>
  );
}
