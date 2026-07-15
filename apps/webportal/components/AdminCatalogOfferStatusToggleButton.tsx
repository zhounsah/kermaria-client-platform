"use client";

import type {
  CommercialOfferMutationResponse,
  CommercialOfferSummary,
} from "@kermaria/shared";
import { useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { requestBffJson } from "@/lib/client-api";

type AdminCatalogOfferStatusToggleButtonProps = {
  offer: CommercialOfferSummary;
};

export function AdminCatalogOfferStatusToggleButton({
  offer,
}: AdminCatalogOfferStatusToggleButtonProps) {
  const router = useRouter();
  const isActive = offer.status === "active";
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);

  async function handleClick() {
    if (isSubmitting) {
      return;
    }

    const confirmText = isActive
      ? `Désactiver l'offre « ${offer.name} » ? Elle ne sera plus proposée aux clients mais l'historique reste conservé.`
      : `Réactiver l'offre « ${offer.name} » ? Elle redeviendra visible côté client.`;

    if (!window.confirm(confirmText)) {
      return;
    }

    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<CommercialOfferMutationResponse>(
      `/api/admin/catalog/${encodeURIComponent(offer.id)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: offer.name,
          description: offer.description,
          category: offer.category,
          unitLabel: offer.unitLabel,
          priceAmountCents: offer.priceAmountCents,
          externalReference: offer.externalReference,
          technicalServiceReferences: offer.technicalServiceReferences,
          provisioningGroupSamAccountNames:
            offer.provisioningGroupSamAccountNames,
          status: isActive ? "inactive" : "active",
          displayOrder: offer.displayOrder,
          billingCadence: offer.billingCadence,
          setupFeeAmountCents: offer.setupFeeAmountCents,
          billingIntervalMonths: offer.billingIntervalMonths,
          commitmentMonths: offer.commitmentMonths,
          paymentMode: offer.paymentMode,
          publicPackCode: offer.publicPackCode,
          paypalPlanIdSandbox: offer.paypalPlanIdSandbox,
          paypalPlanIdLive: offer.paypalPlanIdLive,
          stripePriceIdTest: offer.stripePriceIdTest,
          stripePriceIdLive: offer.stripePriceIdLive,
        }),
      },
    );

    if (result.ok) {
      router.refresh();
    } else {
      setMessage({ tone: "error", text: result.error.message });
      setIsSubmitting(false);
    }
  }

  return (
    <div className="catalog-deactivate-block">
      <button
        className={isActive ? "button button-danger" : "button button-secondary"}
        disabled={isSubmitting}
        onClick={handleClick}
        type="button"
      >
        {isSubmitting
          ? "Enregistrement..."
          : isActive
            ? "Désactiver l'offre"
            : "Réactiver l'offre"}
      </button>
      <p className="field-hint">
        La désactivation est réversible : l&apos;offre disparaît du catalogue
        client mais reste consultable depuis l&apos;administration et conserve
        ses abonnements existants.
      </p>
      {message ? (
        <FormMessage title="Échec" tone="error">
          <p>{message.text}</p>
        </FormMessage>
      ) : null}
    </div>
  );
}
