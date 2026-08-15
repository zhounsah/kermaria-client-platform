"use client";

import { useEffect, useRef, useState } from "react";
import type { MutableRefObject } from "react";

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

const BillingV2PendingProviderSessionCode =
  "BILLING_V2_CHECKOUT_PENDING_PROVIDER_SESSION";
const BillingV2PendingProviderRetryDelayMs = 2000;
const BillingV2PendingProviderMaxAttempts = 6;

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
  const idempotencyKeyRef = useRef<string | null>(null);
  const showRailChoice = paypalEnabled && stripeEnabled;

  useEffect(() => {
    idempotencyKeyRef.current = null;
    setError(null);
  }, [offerId, rail]);

  async function handleClick() {
    if (isSubmitting) {
      return;
    }

    setIsSubmitting(true);
    setError(null);

    const idempotencyKey = getOrCreateIdempotencyKey(
      idempotencyKeyRef,
      offerId,
      rail,
    );
    const requestInit = {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Idempotency-Key": idempotencyKey,
      },
      body: JSON.stringify({ offerId, rail }),
    } satisfies RequestInit;

    for (
      let attempt = 1;
      attempt <= BillingV2PendingProviderMaxAttempts;
      attempt += 1
    ) {
      const result = await requestBffJson<SubscribeResponse>(
        "/api/subscriptions/create",
        requestInit,
      );

      if (result.ok) {
        window.location.assign(result.data.approveUrl);
        return;
      }

      if (shouldRetryBillingV2PendingProviderSession(result, attempt)) {
        await waitForBillingV2PendingProviderRetry();
        continue;
      }

      setError(result.error.message);
      setIsSubmitting(false);
      return;
    }
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

function getOrCreateIdempotencyKey(
  ref: MutableRefObject<string | null>,
  offerId: string,
  rail: string,
) {
  if (!ref.current) {
    const nonce =
      globalThis.crypto?.randomUUID?.()
      ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
    ref.current = `subscription-${offerId}-${rail}-${nonce}`;
  }

  return ref.current;
}

function shouldRetryBillingV2PendingProviderSession(
  result: {
    ok: false;
    status: number;
    error: { code: string };
  },
  attempt: number,
) {
  return (
    result.status === 409
    && result.error.code === BillingV2PendingProviderSessionCode
    && attempt < BillingV2PendingProviderMaxAttempts
  );
}

function waitForBillingV2PendingProviderRetry() {
  return new Promise<void>((resolve) => {
    window.setTimeout(resolve, BillingV2PendingProviderRetryDelayMs);
  });
}
