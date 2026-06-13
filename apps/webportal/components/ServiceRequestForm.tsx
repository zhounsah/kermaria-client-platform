"use client";

import type {
  ApiError,
  MockSubmissionResponse,
  ServiceCatalogItem,
  ServiceRequestPayload,
} from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { useState } from "react";

type ServiceRequestFormProps = {
  services: ServiceCatalogItem[];
};

type SubmissionState =
  | { status: "idle" | "submitting" }
  | { status: "success"; result: MockSubmissionResponse }
  | { status: "error"; message: string };

export function ServiceRequestForm({
  services,
}: ServiceRequestFormProps) {
  const router = useRouter();
  const [submission, setSubmission] = useState<SubmissionState>({
    status: "idle",
  });
  const [payload, setPayload] = useState<ServiceRequestPayload>({
    catalogItemId: "",
    subject: "",
    description: "",
  });

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmission({ status: "submitting" });

    try {
      const response = await fetch("/api/service-requests", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });

      if (response.status === 401) {
        router.replace("/login");
        router.refresh();
        return;
      }

      if (!response.ok) {
        const error = (await response.json()) as ApiError;
        setSubmission({
          status: "error",
          message: error.message || "La simulation n'a pas pu être traitée.",
        });
        return;
      }

      const result = (await response.json()) as MockSubmissionResponse;
      setPayload({
        catalogItemId: "",
        subject: "",
        description: "",
      });
      setSubmission({ status: "success", result });
    } catch {
      setSubmission({
        status: "error",
        message: "La simulation est temporairement indisponible.",
      });
    }
  }

  return (
    <form
      action="/api/service-requests"
      className="form-card"
      method="post"
      onSubmit={handleSubmit}
    >
      {submission.status === "success" ? (
        <div className="feedback-message" role="status">
          <strong>
            {submission.result.persisted
              ? "Demande de service enregistrée."
              : "Demande de service mock reçue."}
          </strong>
          <span>
            Référence {submission.result.reference}.{" "}
            <code>
              persisted: {String(submission.result.persisted)}
            </code>
            .{" "}
            {submission.result.persisted
              ? "La demande est persistée dans MariaDB par API-INTERNAL."
              : "Aucune donnée n'a été persistée."}{" "}
            Aucun devis, contrat ou paiement n&apos;a été créé.
          </span>
        </div>
      ) : null}
      {submission.status === "error" ? (
        <div className="feedback-message feedback-error" role="alert">
          <strong>Simulation non envoyée.</strong>
          <span>{submission.message}</span>
        </div>
      ) : null}

      <label>
        Service souhaité
        <select
          name="catalogItemId"
          onChange={(event) =>
            setPayload((current) => ({
              ...current,
              catalogItemId: event.target.value,
            }))
          }
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
      </label>

      <label>
        Objet
        <input
          autoComplete="off"
          maxLength={160}
          name="subject"
          onChange={(event) =>
            setPayload((current) => ({
              ...current,
              subject: event.target.value,
            }))
          }
          placeholder="Ex. Demande d'accès VPN privé"
          required
          type="text"
          value={payload.subject}
        />
      </label>

      <label>
        Description de la demande
        <textarea
          maxLength={4000}
          name="description"
          onChange={(event) =>
            setPayload((current) => ({
              ...current,
              description: event.target.value,
            }))
          }
          placeholder="Précisez le besoin sans inclure d'identifiants, mots de passe ou données confidentielles."
          required
          rows={6}
          value={payload.description}
        />
      </label>

      <div className="form-footer">
        <p className="form-helper">
          Cette V0.7 ne crée ni commande, ni contrat, ni paiement.
        </p>
        <button
          className="button"
          disabled={submission.status === "submitting"}
          type="submit"
        >
          {submission.status === "submitting"
            ? "Simulation en cours..."
            : "Simuler la demande"}
        </button>
      </div>
    </form>
  );
}
