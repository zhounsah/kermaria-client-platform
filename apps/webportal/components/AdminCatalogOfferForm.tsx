"use client";

import type {
  CommercialOfferBillingCadence,
  CommercialOfferMutationResponse,
  CommercialOfferSummary,
} from "@kermaria/shared";
import { FormEvent, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type AdminCatalogOfferFormProps = {
  offer?: CommercialOfferSummary;
};

export function AdminCatalogOfferForm({ offer }: AdminCatalogOfferFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [name, setName] = useState(offer?.name ?? "");
  const [description, setDescription] = useState(offer?.description ?? "");
  const [category, setCategory] = useState(offer?.category ?? "");
  const [unitLabel, setUnitLabel] = useState(offer?.unitLabel ?? "");
  const [priceAmountCents, setPriceAmountCents] = useState(
    String(offer?.priceAmountCents ?? 0),
  );
  const [displayOrder, setDisplayOrder] = useState(
    String(offer?.displayOrder ?? 0),
  );
  const [status, setStatus] = useState(offer?.status ?? "active");
  const [billingCadence, setBillingCadence] =
    useState<CommercialOfferBillingCadence>(
      offer?.billingCadence ?? "one_time",
    );
  const [paypalPlanId, setPaypalPlanId] = useState(offer?.paypalPlanId ?? "");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current) {
      return;
    }

    const trimmedPlanId = paypalPlanId.trim();
    const payload = {
      name: name.trim(),
      description: description.trim(),
      category: category.trim(),
      unitLabel: unitLabel.trim(),
      priceAmountCents: Number.parseInt(priceAmountCents, 10),
      status,
      displayOrder: Number.parseInt(displayOrder, 10),
      billingCadence,
      paypalPlanId:
        billingCadence === "monthly" && trimmedPlanId.length > 0
          ? trimmedPlanId
          : null,
    };

    if (
      payload.name.length < 3
      || payload.description.length < 3
      || payload.category.length < 2
      || payload.unitLabel.length < 1
      || !Number.isInteger(payload.priceAmountCents)
      || payload.priceAmountCents < 0
      || !Number.isInteger(payload.displayOrder)
      || payload.displayOrder < 0
      || (payload.paypalPlanId !== null
        && !/^[A-Za-z0-9_-]{1,64}$/.test(payload.paypalPlanId))
    ) {
      setMessage({
        tone: "error",
        text: "Vérifiez les champs du catalogue avant d'enregistrer.",
      });
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<CommercialOfferMutationResponse>(
      offer
        ? `/api/admin/catalog/${encodeURIComponent(offer.id)}`
        : "/api/admin/catalog",
      {
        method: offer ? "PATCH" : "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );

    if (result.ok) {
      setMessage({
        tone: "success",
        text: offer
          ? "L'offre a été mise à jour."
          : "L'offre a été créée.",
      });
      router.refresh();
    } else {
      setMessage({ tone: "error", text: result.error.message });
    }

    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  return (
    <form className="form-card" onSubmit={handleSubmit}>
      <div className="form-grid">
        <label>
          Nom de l&apos;offre
          <input
            maxLength={200}
            onChange={(event) => setName(event.target.value)}
            value={name}
          />
        </label>
        <label>
          Catégorie
          <input
            maxLength={100}
            onChange={(event) => setCategory(event.target.value)}
            value={category}
          />
        </label>
      </div>
      <label>
        Description courte
        <textarea
          maxLength={1000}
          onChange={(event) => setDescription(event.target.value)}
          rows={4}
          value={description}
        />
      </label>
      <div className="form-grid">
        <label>
          Unité
          <input
            maxLength={40}
            onChange={(event) => setUnitLabel(event.target.value)}
            value={unitLabel}
          />
        </label>
        <label>
          Prix indicatif HT (centimes)
          <input
            inputMode="numeric"
            onChange={(event) => setPriceAmountCents(event.target.value)}
            value={priceAmountCents}
          />
        </label>
      </div>
      <div className="form-grid">
        <label>
          Ordre d&apos;affichage
          <input
            inputMode="numeric"
            onChange={(event) => setDisplayOrder(event.target.value)}
            value={displayOrder}
          />
        </label>
        <label>
          Statut
          <select
            onChange={(event) => setStatus(
              event.target.value as "active" | "inactive",
            )}
            value={status}
          >
            <option value="active">Active</option>
            <option value="inactive">Inactive</option>
          </select>
        </label>
      </div>
      <div className="form-grid">
        <label>
          Cadence de facturation
          <select
            onChange={(event) => {
              const next = event.target
                .value as CommercialOfferBillingCadence;
              setBillingCadence(next);
              if (next === "one_time") {
                setPaypalPlanId("");
              }
            }}
            value={billingCadence}
          >
            <option value="one_time">Ponctuelle</option>
            <option value="monthly">Mensuelle</option>
          </select>
        </label>
        <label>
          Identifiant PayPal Plan
          <input
            disabled={billingCadence !== "monthly"}
            maxLength={64}
            onChange={(event) => setPaypalPlanId(event.target.value)}
            placeholder="P-XXXXXXXXXXXXXXXXXXX"
            value={paypalPlanId}
          />
          <span className="field-hint">
            Créé manuellement dans le dashboard PayPal pour les offres
            mensuelles ; laisser vide pour les offres ponctuelles.
          </span>
        </label>
      </div>
      {message ? (
        <FormMessage
          title={message.tone === "success" ? "Catalogue enregistré" : "Échec"}
          tone={message.tone}
        >
          <p>{message.text}</p>
        </FormMessage>
      ) : null}
      <SubmitButton
        idleLabel={offer ? "Mettre à jour l'offre" : "Créer l'offre"}
        isSubmitting={isSubmitting}
        submittingLabel="Enregistrement..."
      />
    </form>
  );
}
