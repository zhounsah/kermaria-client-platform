"use client";

import type {
  MockSubmissionResponse,
  ServiceSummary,
  SupportRequestPayload,
} from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";
import {
  type FieldErrors,
  hasFieldErrors,
  validateSupportRequest,
} from "@/lib/form-validation";

type SupportRequestFormProps = {
  services: ServiceSummary[];
};

type SubmissionState =
  | { status: "idle" | "submitting" }
  | { status: "success"; result: MockSubmissionResponse }
  | { status: "error"; message: string; correlationId?: string };

export function SupportRequestForm({
  services,
}: SupportRequestFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [submission, setSubmission] = useState<SubmissionState>({
    status: "idle",
  });
  const [fieldErrors, setFieldErrors] = useState<
    FieldErrors<keyof SupportRequestPayload>
  >({});
  const [payload, setPayload] = useState<SupportRequestPayload>({
    serviceId: "",
    priority: "normal",
    subject: "",
    description: "",
  });

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current || services.length === 0) {
      return;
    }

    const validation = validateSupportRequest(payload);
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
        "/api/support-requests",
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
        serviceId: "",
        priority: "normal",
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
      action="/api/support-requests"
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
              ? "Votre demande a été transmise. Elle sera étudiée avant toute intervention."
              : "Aucune donnée durable ni aucun e-mail n’ont été créés."}
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
      {services.length === 0 ? (
        <FormMessage title="Aucun service disponible" tone="info">
          <p>
            Une demande support doit être liée à un service du compte. Le
            formulaire sera disponible dès qu’un service pourra être sélectionné.
          </p>
        </FormMessage>
      ) : null}

      <div className="form-grid">
        <label>
          Service concerné
          <select
            aria-describedby={
              fieldErrors.serviceId ? "support-service-error" : undefined
            }
            aria-invalid={Boolean(fieldErrors.serviceId)}
            disabled={services.length === 0}
            name="serviceId"
            onChange={(event) => {
              setFieldErrors((current) => ({
                ...current,
                serviceId: undefined,
              }));
              setPayload((current) => ({
                ...current,
                serviceId: event.target.value,
              }));
            }}
            required
            value={payload.serviceId}
          >
            <option value="">Sélectionner un service</option>
            {services.map((service) => (
              <option key={service.id} value={service.id}>
                {service.name}
              </option>
            ))}
          </select>
          {fieldErrors.serviceId ? (
            <span className="field-error" id="support-service-error">
              {fieldErrors.serviceId}
            </span>
          ) : null}
        </label>
        <label>
          Priorité
          <select
            disabled={services.length === 0}
            name="priority"
            onChange={(event) =>
              setPayload((current) => ({
                ...current,
                priority: event.target.value as SupportRequestPayload["priority"],
              }))
            }
            value={payload.priority}
          >
            <option value="low">Faible</option>
            <option value="normal">Normale</option>
            <option value="high">Haute</option>
          </select>
        </label>
      </div>

      <label>
        Objet
        <input
          aria-describedby={
            fieldErrors.subject
              ? "support-subject-error"
              : "support-subject-hint"
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
          placeholder="Ex. Vérification d'une sauvegarde"
          required
          type="text"
          value={payload.subject}
        />
        {fieldErrors.subject ? (
          <span className="field-error" id="support-subject-error">
            {fieldErrors.subject}
          </span>
        ) : (
          <span className="field-hint" id="support-subject-hint">
            3 à 160 caractères.
          </span>
        )}
      </label>

      <label>
        Description
        <textarea
          aria-describedby={
            fieldErrors.description
              ? "support-description-error"
              : "support-description-hint"
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
          placeholder="Décrivez le contexte. Ne saisissez aucun identifiant, mot de passe ou contenu confidentiel."
          required
          rows={6}
          value={payload.description}
        />
        {fieldErrors.description ? (
          <span className="field-error" id="support-description-error">
            {fieldErrors.description}
          </span>
        ) : (
          <span className="field-hint" id="support-description-hint">
            10 à 4 000 caractères, sans donnée confidentielle.
          </span>
        )}
      </label>

      <div className="form-footer">
        <p className="form-helper">
          Aucun e-mail ni traitement automatique. La demande sera examinée
          avant toute intervention.
        </p>
        <SubmitButton
          disabled={services.length === 0}
          idleLabel="Envoyer la demande"
          isSubmitting={submission.status === "submitting"}
          submittingLabel="Envoi en cours..."
        />
      </div>
    </form>
  );
}
