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

type CreatePlanResponse = {
  paypalPlanId: string;
  paypalProductId: string;
  mode: "sandbox" | "live";
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
  const paypalPlanIdSandbox = offer?.paypalPlanIdSandbox ?? null;
  const paypalPlanIdLive = offer?.paypalPlanIdLive ?? null;
  const planLocked = paypalPlanIdSandbox !== null || paypalPlanIdLive !== null;
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isCreatingPlan, setIsCreatingPlan] = useState(false);
  const [planMessage, setPlanMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current) {
      return;
    }

    const payload = {
      name: name.trim(),
      description: description.trim(),
      category: category.trim(),
      unitLabel: unitLabel.trim(),
      priceAmountCents: Number.parseInt(priceAmountCents, 10),
      status,
      displayOrder: Number.parseInt(displayOrder, 10),
      billingCadence,
      paypalPlanIdSandbox,
      paypalPlanIdLive,
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

  async function handleCreatePlan() {
    if (!offer || isCreatingPlan) {
      return;
    }
    setIsCreatingPlan(true);
    setPlanMessage(null);

    const result = await requestBffJson<CreatePlanResponse>(
      `/api/admin/catalog/${encodeURIComponent(offer.id)}/paypal-plan`,
      { method: "POST" },
    );

    if (result.ok) {
      setPlanMessage({
        tone: "success",
        text: `Plan PayPal ${result.data.mode} créé : ${result.data.paypalPlanId}`,
      });
      router.refresh();
    } else {
      setPlanMessage({ tone: "error", text: result.error.message });
    }

    setIsCreatingPlan(false);
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
            disabled={planLocked}
            inputMode="numeric"
            onChange={(event) => setPriceAmountCents(event.target.value)}
            value={priceAmountCents}
          />
          {planLocked ? (
            <span className="field-hint">
              Le prix est figé car au moins un plan PayPal a été créé pour
              cette offre. Pour changer le prix, créez une nouvelle offre.
            </span>
          ) : null}
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
            disabled={planLocked}
            onChange={(event) => setBillingCadence(
              event.target.value as CommercialOfferBillingCadence,
            )}
            value={billingCadence}
          >
            <option value="one_time">Ponctuelle</option>
            <option value="monthly">Mensuelle</option>
          </select>
        </label>
        <div>
          <span className="field-hint" style={{ display: "block" }}>
            Plan PayPal sandbox : {paypalPlanIdSandbox ?? "non créé"}
          </span>
          <span className="field-hint" style={{ display: "block" }}>
            Plan PayPal live : {paypalPlanIdLive ?? "non créé"}
          </span>
        </div>
      </div>
      {offer && billingCadence === "monthly" ? (
        <div>
          <button
            className="button"
            disabled={isCreatingPlan}
            onClick={handleCreatePlan}
            type="button"
          >
            {isCreatingPlan
              ? "Création du plan PayPal..."
              : "Créer le plan PayPal pour le mode actif"}
          </button>
          <p className="field-hint">
            Crée un product + plan PayPal pour le mode PAYPAL_MODE en cours
            (sandbox ou live) et enregistre l&apos;identifiant. Si le plan
            existe déjà pour ce mode, l&apos;appel est refusé.
          </p>
          {planMessage ? (
            <FormMessage
              title={planMessage.tone === "success" ? "Plan créé" : "Échec"}
              tone={planMessage.tone}
            >
              <p>{planMessage.text}</p>
            </FormMessage>
          ) : null}
        </div>
      ) : null}
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
