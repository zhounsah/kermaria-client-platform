"use client";

import type {
  MockSubmissionResponse,
  ServiceCatalogItem,
  ServiceRequestPayload,
} from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";
import {
  type FieldErrors,
  hasFieldErrors,
  validateServiceRequest,
} from "@/lib/form-validation";

type ServiceRequestFormProps = {
  services: ServiceCatalogItem[];
  initialCatalogItemId?: string;
};

type SubmissionState =
  | { status: "idle" | "submitting" }
  | { status: "success"; result: MockSubmissionResponse }
  | { status: "error"; message: string; correlationId?: string };

export function ServiceRequestForm({
  services,
  initialCatalogItemId,
}: ServiceRequestFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [submission, setSubmission] = useState<SubmissionState>({
    status: "idle",
  });
  const [fieldErrors, setFieldErrors] = useState<
    FieldErrors<keyof ServiceRequestPayload>
  >({});
  const preselectedCatalogItemId =
    initialCatalogItemId
    && services.some((service) => service.id === initialCatalogItemId)
      ? initialCatalogItemId
      : "";
  const [payload, setPayload] = useState<ServiceRequestPayload>({
    catalogItemId: preselectedCatalogItemId,
    subject: "",
    description: "",
  });

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current || services.length === 0) {
      return;
    }

    const validation = validateServiceRequest(payload);
    setFieldErrors(validation.errors);
    setPayload(validation.payload);

    if (hasFieldErrors(validation.errors)) {
      setSubmission({
        status: "error",
        message: "Vérifiez les champs signalés avant d’envoyer la demande.",
      });
      return;
    }

    isSubmittingRef.current = true;
    setSubmission({ status: "submitting" });

    try {
      const response = await requestBffJson<MockSubmissionResponse>(
        "/api/service-requests",
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(validation.payload),
        },
      );

      if (!response.ok && response.status === 401) {
        router.replace("/login");
        router.refresh();
        return;
      }

      if (!response.ok) {
        setSubmission({
          status: "error",
          message: response.error.message,
          correlationId: response.error.correlationId,
        });
        return;
      }

      setPayload({
        catalogItemId: "",
        subject: "",
        description: "",
      });
      setFieldErrors({});
      setSubmission({ status: "success", result: response.data });
    } finally {
      isSubmittingRef.current = false;
    }
  }

  return (
    <form
      action="/api/service-requests"
      className="form-card"
      method="post"
      noValidate
      onSubmit={handleSubmit}
    >
      {submission.status === "success" ? (
        <FormMessage
          title={
            submission.result.persisted
              ? "Demande enregistrée"
              : "Demande reçue en mode démonstration"
          }
          tone="success"
        >
          <p>
            Référence <strong>{submission.result.reference}</strong>.{" "}
            {submission.result.persisted
              ? "Votre demande a été transmise. Elle sera étudiée avant toute activation."
              : "Aucune donnée durable n’a été créée."}{" "}
            Aucun devis, contrat, paiement ou provisioning automatique n’a été
            déclenché.
          </p>
        </FormMessage>
      ) : null}
      {submission.status === "error" ? (
        <FormMessage title="Demande non envoyée" tone="error">
          <p>{submission.message}</p>
          {submission.correlationId ? (
            <p>Référence : {submission.correlationId}</p>
          ) : null}
        </FormMessage>
      ) : null}

      <label>
        Service souhaité
        <select
          aria-describedby={
            fieldErrors.catalogItemId
              ? "service-catalog-error"
              : undefined
          }
          aria-invalid={Boolean(fieldErrors.catalogItemId)}
          disabled={services.length === 0}
          name="catalogItemId"
          onChange={(event) => {
            setFieldErrors((current) => ({
              ...current,
              catalogItemId: undefined,
            }));
            setPayload((current) => ({
              ...current,
              catalogItemId: event.target.value,
            }));
          }}
          required
          value={payload.catalogItemId}
        >
          <option value="">Sélectionner une prestation</option>
          {services.map((service) => (
            <option key={service.id} value={service.id}>
              {service.name}
            </option>
          ))}
        </select>
        {fieldErrors.catalogItemId ? (
          <span className="field-error" id="service-catalog-error">
            {fieldErrors.catalogItemId}
          </span>
        ) : null}
      </label>

      <label>
        Objet
        <input
          aria-describedby={
            fieldErrors.subject
              ? "service-subject-error"
              : "service-subject-hint"
          }
          aria-invalid={Boolean(fieldErrors.subject)}
          autoComplete="off"
          disabled={services.length === 0}
          maxLength={160}
          name="subject"
          onChange={(event) => {
            setFieldErrors((current) => ({
              ...current,
              subject: undefined,
            }));
            setPayload((current) => ({
              ...current,
              subject: event.target.value,
            }));
          }}
          placeholder="Ex. Demande d'accès VPN privé"
          required
          type="text"
          value={payload.subject}
        />
        {fieldErrors.subject ? (
          <span className="field-error" id="service-subject-error">
            {fieldErrors.subject}
          </span>
        ) : (
          <span className="field-hint" id="service-subject-hint">
            3 à 160 caractères.
          </span>
        )}
      </label>

      <label>
        Description de la demande
        <textarea
          aria-describedby={
            fieldErrors.description
              ? "service-description-error"
              : "service-description-hint"
          }
          aria-invalid={Boolean(fieldErrors.description)}
          disabled={services.length === 0}
          maxLength={4000}
          name="description"
          onChange={(event) => {
            setFieldErrors((current) => ({
              ...current,
              description: undefined,
            }));
            setPayload((current) => ({
              ...current,
              description: event.target.value,
            }));
          }}
          placeholder="Précisez le besoin sans inclure d'identifiants, mots de passe ou données confidentielles."
          required
          rows={6}
          value={payload.description}
        />
        {fieldErrors.description ? (
          <span className="field-error" id="service-description-error">
            {fieldErrors.description}
          </span>
        ) : (
          <span className="field-hint" id="service-description-hint">
            10 à 4 000 caractères, sans identifiant ni donnée confidentielle.
          </span>
        )}
      </label>

      <div className="form-footer">
        <p className="form-helper">
          La demande sera étudiée avant toute activation. Elle ne crée ni
          commande, ni contrat, ni paiement.
        </p>
        <SubmitButton
          disabled={services.length === 0}
          idleLabel="Transmettre la demande"
          isSubmitting={submission.status === "submitting"}
          submittingLabel="Transmission en cours..."
        />
      </div>
    </form>
  );
}
