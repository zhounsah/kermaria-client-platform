"use client";

import type {
  ApiError,
  MockSubmissionResponse,
  ServiceSummary,
  SupportRequestPayload,
} from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { useState } from "react";

type SupportRequestFormProps = {
  services: ServiceSummary[];
};

type SubmissionState =
  | { status: "idle" | "submitting" }
  | { status: "success"; result: MockSubmissionResponse }
  | { status: "error"; message: string };

export function SupportRequestForm({
  services,
}: SupportRequestFormProps) {
  const router = useRouter();
  const [submission, setSubmission] = useState<SubmissionState>({
    status: "idle",
  });
  const [payload, setPayload] = useState<SupportRequestPayload>({
    serviceId: "",
    priority: "normal",
    subject: "",
    description: "",
  });

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmission({ status: "submitting" });

    try {
      const response = await fetch("/api/support-requests", {
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
        serviceId: "",
        priority: "normal",
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
      action="/api/support-requests"
      className="form-card"
      method="post"
      onSubmit={handleSubmit}
    >
      {submission.status === "success" ? (
        <div className="feedback-message" role="status">
          <strong>
            {submission.result.persisted
              ? "Demande enregistrée."
              : "Demande mock reçue."}
          </strong>
          <span>
            Référence {submission.result.reference}.{" "}
            <code>
              persisted: {String(submission.result.persisted)}
            </code>
            .{" "}
            {submission.result.persisted
              ? "La demande est persistée dans MariaDB par API-INTERNAL. Aucun e-mail ni traitement immédiat n'est déclenché."
              : "Aucune donnée n'a été persistée ni envoyée par e-mail."}
          </span>
        </div>
      ) : null}
      {submission.status === "error" ? (
        <div className="feedback-message feedback-error" role="alert">
          <strong>Simulation non envoyée.</strong>
          <span>{submission.message}</span>
        </div>
      ) : null}

      <div className="form-grid">
        <label>
          Service concerné
          <select
            name="serviceId"
            onChange={(event) =>
              setPayload((current) => ({
                ...current,
                serviceId: event.target.value,
              }))
            }
            required
            value={payload.serviceId}
          >
            <option value="">Sélectionner un service</option>
            {services.map((service) => (
              <option key={service.id} value={service.id}>
                {service.name}
              </option>
            ))}
            <option value="account">Compte client</option>
          </select>
        </label>
        <label>
          Priorité
          <select
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
          autoComplete="off"
          maxLength={160}
          name="subject"
          onChange={(event) =>
            setPayload((current) => ({
              ...current,
              subject: event.target.value,
            }))
          }
          placeholder="Ex. Vérification d'une sauvegarde"
          required
          type="text"
          value={payload.subject}
        />
      </label>

      <label>
        Description
        <textarea
          maxLength={4000}
          name="description"
          onChange={(event) =>
            setPayload((current) => ({
              ...current,
              description: event.target.value,
            }))
          }
          placeholder="Décrivez le contexte. Ne saisissez aucun identifiant, mot de passe ou contenu confidentiel."
          required
          rows={6}
          value={payload.description}
        />
      </label>

      <div className="form-footer">
        <p className="form-helper">
          Aucun e-mail ni traitement immédiat. La persistance dépend de la
          configuration MariaDB d&apos;API-INTERNAL.
        </p>
        <button
          className="button"
          disabled={submission.status === "submitting"}
          type="submit"
        >
          {submission.status === "submitting"
            ? "Simulation en cours..."
            : "Simuler l'envoi"}
        </button>
      </div>
    </form>
  );
}
