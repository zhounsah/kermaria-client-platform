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
import {
  formatBillingIntervalMonths,
  formatCommitmentMonths,
  formatCurrencyFromCents,
  formatPaymentModeLabel,
} from "@/lib/formatters";

type AdminCatalogOfferFormProps = {
  offer?: CommercialOfferSummary;
};

type CreatePlanResponse = {
  paypalPlanId: string;
  paypalProductId: string;
  mode: "sandbox" | "live";
};

type CreateStripePriceResponse = {
  stripePriceId: string;
  stripeProductId: string;
  mode: "test" | "live";
};

export function AdminCatalogOfferForm({ offer }: AdminCatalogOfferFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [name, setName] = useState(offer?.name ?? "");
  const [description, setDescription] = useState(offer?.description ?? "");
  const [category, setCategory] = useState(offer?.category ?? "");
  const [unitLabel, setUnitLabel] = useState(offer?.unitLabel ?? "");
  const [externalReference, setExternalReference] = useState(
    offer?.externalReference ?? "",
  );
  const [technicalServiceReferences, setTechnicalServiceReferences] = useState(
    (offer?.technicalServiceReferences ?? []).join("\n"),
  );
  const [
    provisioningGroupSamAccountNames,
    setProvisioningGroupSamAccountNames,
  ] = useState((offer?.provisioningGroupSamAccountNames ?? []).join("\n"));
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
  const stripePriceIdTest = offer?.stripePriceIdTest ?? null;
  const stripePriceIdLive = offer?.stripePriceIdLive ?? null;
  const setupFeeAmountCents = offer?.setupFeeAmountCents ?? null;
  const billingIntervalMonths = offer?.billingIntervalMonths ?? null;
  const commitmentMonths = offer?.commitmentMonths ?? null;
  const paymentMode = offer?.paymentMode ?? null;
  const publicPackCode = offer?.publicPackCode ?? null;
  const planLocked =
    paypalPlanIdSandbox !== null
    || paypalPlanIdLive !== null
    || stripePriceIdTest !== null
    || stripePriceIdLive !== null;
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isCreatingPlan, setIsCreatingPlan] = useState(false);
  const [isCreatingStripePrice, setIsCreatingStripePrice] = useState(false);
  const [planMessage, setPlanMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);
  const [stripePriceMessage, setStripePriceMessage] = useState<{
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
      externalReference: externalReference.trim() || null,
      technicalServiceReferences: splitReferenceList(technicalServiceReferences),
      provisioningGroupSamAccountNames: splitReferenceList(
        provisioningGroupSamAccountNames,
      ),
      priceAmountCents: Number.parseInt(priceAmountCents, 10),
      status,
      displayOrder: Number.parseInt(displayOrder, 10),
      billingCadence,
      setupFeeAmountCents,
      billingIntervalMonths,
      commitmentMonths,
      paymentMode,
      publicPackCode,
      paypalPlanIdSandbox,
      paypalPlanIdLive,
      stripePriceIdTest,
      stripePriceIdLive,
    };

    if (
      payload.name.length < 3
      || payload.description.length < 3
      || payload.category.length < 2
      || payload.unitLabel.length < 1
      || (
        payload.externalReference !== null
        && !/^[A-Za-z0-9._-]{1,100}$/.test(payload.externalReference)
      )
      || payload.technicalServiceReferences.some((entry) =>
        !/^[A-Za-z0-9._-]{1,100}$/.test(entry)
      )
      || payload.provisioningGroupSamAccountNames.some((entry) =>
        !/^[A-Za-z0-9._-]{1,100}$/.test(entry)
      )
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
      if (offer) {
        router.refresh();
      } else {
        router.push(`/admin/catalog/${encodeURIComponent(result.data.id)}`);
      }
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

  async function handleCreateStripePrice() {
    if (!offer || isCreatingStripePrice) {
      return;
    }
    setIsCreatingStripePrice(true);
    setStripePriceMessage(null);

    const result = await requestBffJson<CreateStripePriceResponse>(
      `/api/admin/catalog/${encodeURIComponent(offer.id)}/stripe-price`,
      { method: "POST" },
    );

    if (result.ok) {
      setStripePriceMessage({
        tone: "success",
        text: `Prix Stripe ${result.data.mode} créé : ${result.data.stripePriceId}`,
      });
      router.refresh();
    } else {
      setStripePriceMessage({ tone: "error", text: result.error.message });
    }

    setIsCreatingStripePrice(false);
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
        Référence externe
        <input
          maxLength={100}
          onChange={(event) => setExternalReference(event.target.value)}
          placeholder="Ex. ACCES-VPN ou PACK-PRO-1M-MENS"
          value={externalReference}
        />
        <span className="field-hint">
          Référence stable utilisée par les souscriptions, les options et le
          provisionning.
        </span>
      </label>
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
          Services techniques couverts
          <textarea
            maxLength={2000}
            onChange={(event) =>
              setTechnicalServiceReferences(event.target.value)
            }
            placeholder={"Une référence par ligne\nEx. ACCES-VPN"}
            rows={5}
            value={technicalServiceReferences}
          />
          <span className="field-hint">
            Pour un pack ou une option composite, listez ici les services
            techniques activables.
          </span>
        </label>
        <label>
          Groupes AD provisionnés
          <textarea
            maxLength={2000}
            onChange={(event) =>
              setProvisioningGroupSamAccountNames(event.target.value)
            }
            placeholder={"Un groupe par ligne\nEx. GG_VPN"}
            rows={5}
            value={provisioningGroupSamAccountNames}
          />
          <span className="field-hint">
            Pour une offre technique directement provisionnable, listez ici les
            groupes AD associés.
          </span>
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
          <span className="field-hint" style={{ display: "block" }}>
            Prix Stripe test : {stripePriceIdTest ?? "non créé"}
          </span>
          <span className="field-hint" style={{ display: "block" }}>
            Prix Stripe live : {stripePriceIdLive ?? "non créé"}
          </span>
        </div>
      </div>
      {publicPackCode ? (
        <div className="content-panel" style={{ marginTop: 12, padding: 16 }}>
          <strong>Metadonnees pack public</strong>
          <dl className="profile-details" style={{ marginTop: 12 }}>
            <div>
              <dt>Code pack</dt>
              <dd>{publicPackCode}</dd>
            </div>
            <div>
              <dt>Engagement</dt>
              <dd>{formatCommitmentMonths(commitmentMonths)}</dd>
            </div>
            <div>
              <dt>Mode de paiement</dt>
              <dd>{formatPaymentModeLabel(paymentMode)}</dd>
            </div>
            <div>
              <dt>Intervalle facture</dt>
              <dd>{formatBillingIntervalMonths(billingIntervalMonths)}</dd>
            </div>
            <div>
              <dt>Mise en service</dt>
              <dd>
                {setupFeeAmountCents === null
                  ? "—"
                  : `${formatCurrencyFromCents(setupFeeAmountCents)} HT`}
              </dd>
            </div>
          </dl>
          <p className="field-hint">
            Ces metadonnees sont preservees automatiquement lors des mises a
            jour de la fiche.
          </p>
        </div>
      ) : null}
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
          <button
            className="button"
            disabled={isCreatingStripePrice}
            onClick={handleCreateStripePrice}
            style={{ marginTop: "0.5rem" }}
            type="button"
          >
            {isCreatingStripePrice
              ? "Création du prix Stripe..."
              : "Créer le prix Stripe pour le mode actif"}
          </button>
          <p className="field-hint">
            Crée un product + price Stripe pour le mode STRIPE_MODE en cours
            (test ou live) et enregistre l&apos;identifiant. Si le prix
            existe déjà pour ce mode, l&apos;appel est refusé.
          </p>
          {stripePriceMessage ? (
            <FormMessage
              title={stripePriceMessage.tone === "success" ? "Prix créé" : "Échec"}
              tone={stripePriceMessage.tone}
            >
              <p>{stripePriceMessage.text}</p>
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

function splitReferenceList(value: string) {
  return Array.from(new Set(
    value
      .split(/\r?\n|[,;]/)
      .map((entry) => entry.trim())
      .filter((entry) => entry.length > 0),
  ));
}
