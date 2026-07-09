"use client";

import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import type { CommercialDocumentMutationResponse } from "@/lib/commercial-document-api";
import { requestBffJson } from "@/lib/client-api";

type PaymentChoice = "paypal" | "stripe" | "manual";

type PayButtonProps = {
  documentId: string;
  paypalEnabled: boolean;
  stripeEnabled: boolean;
  bankTransferEnabled: boolean;
  initialPaymentMethod: PaymentChoice | null;
};

type CreatePaymentResponse = {
  approveUrl: string;
};

export function PayButton({
  documentId,
  paypalEnabled,
  stripeEnabled,
  bankTransferEnabled,
  initialPaymentMethod,
}: PayButtonProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [rail, setRail] = useState<PaymentChoice>(() => {
    if (initialPaymentMethod === "manual" && bankTransferEnabled) {
      return "manual";
    }

    if (initialPaymentMethod === "paypal" && paypalEnabled) {
      return "paypal";
    }

    if (initialPaymentMethod === "stripe" && stripeEnabled) {
      return "stripe";
    }

    if (stripeEnabled) {
      return "stripe";
    }

    if (paypalEnabled) {
      return "paypal";
    }

    return "manual";
  });
  const [bankTransferSaved, setBankTransferSaved] = useState(
    initialPaymentMethod === "manual",
  );

  const availableRails: PaymentChoice[] = [];
  if (stripeEnabled) {
    availableRails.push("stripe");
  }
  if (paypalEnabled) {
    availableRails.push("paypal");
  }
  if (bankTransferEnabled) {
    availableRails.push("manual");
  }

  const showRailChoice = availableRails.length > 1;
  const manualSelected = rail === "manual";
  const manualAlreadySaved = manualSelected && bankTransferSaved;

  async function handlePay() {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setError(null);

    if (manualSelected) {
      const result = await requestBffJson<CommercialDocumentMutationResponse>(
        `/api/commercial-documents/${documentId}/payment-method`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ paymentMethod: "manual" }),
        },
      );

      isSubmittingRef.current = false;
      setIsSubmitting(false);

      if (result.ok) {
        setBankTransferSaved(true);
        router.refresh();
      } else {
        setError(result.error.message);
      }

      return;
    }

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

  function handleRailChange(nextRail: PaymentChoice) {
    setRail(nextRail);
    setError(null);
    if (nextRail !== "manual") {
      setBankTransferSaved(false);
    }
  }

  const idleLabel = manualSelected
    ? manualAlreadySaved
      ? "Virement bancaire sélectionné"
      : "Choisir le virement bancaire"
    : rail === "stripe"
      ? "Payer via Stripe"
      : "Payer via PayPal";

  const submittingLabel = manualSelected
    ? "Enregistrement du virement..."
    : "Redirection vers le paiement...";

  return (
    <div className="workflow-form">
      {error ? (
        <FormMessage title="Échec" tone="error">
          <p>{error}</p>
        </FormMessage>
      ) : null}
      {showRailChoice ? (
        <fieldset className="payment-rail-group">
          <legend>Choisir le mode de règlement</legend>
          <div
            aria-label="Mode de règlement"
            className="payment-rail-options"
            role="radiogroup"
          >
            {stripeEnabled ? (
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
                  name={`payment-rail-${documentId}`}
                  onChange={() => handleRailChange("stripe")}
                  type="radio"
                  value="stripe"
                />
                <span className="payment-rail-title">Carte bancaire</span>
                <span className="payment-rail-hint">Paiement via Stripe</span>
              </label>
            ) : null}
            {paypalEnabled ? (
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
                  name={`payment-rail-${documentId}`}
                  onChange={() => handleRailChange("paypal")}
                  type="radio"
                  value="paypal"
                />
                <span className="payment-rail-title">PayPal</span>
                <span className="payment-rail-hint">
                  Paiement sur compte PayPal
                </span>
              </label>
            ) : null}
            {bankTransferEnabled ? (
              <label
                className={
                  manualSelected
                    ? "payment-rail-option payment-rail-option-active"
                    : "payment-rail-option"
                }
              >
                <input
                  checked={manualSelected}
                  className="visually-hidden"
                  name={`payment-rail-${documentId}`}
                  onChange={() => handleRailChange("manual")}
                  type="radio"
                  value="manual"
                />
                <span className="payment-rail-title">Virement bancaire</span>
                <span className="payment-rail-hint">
                  Enregistrer ce choix et régler avec l&apos;IBAN affiché
                </span>
              </label>
            ) : null}
          </div>
        </fieldset>
      ) : null}
      <SubmitButton
        className="button"
        disabled={manualAlreadySaved}
        idleLabel={idleLabel}
        isSubmitting={isSubmitting}
        onClick={handlePay}
        submittingLabel={submittingLabel}
        type="button"
      />
    </div>
  );
}
