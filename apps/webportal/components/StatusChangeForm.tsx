"use client";

import type {
  RequestMutationResponse,
  RequestType,
  ServiceRequestStatus,
  SupportRequestStatus,
} from "@kermaria/shared";
import { FormEvent, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";
import {
  serviceRequestStatus,
  supportStatus,
} from "@/lib/formatters";

type StatusChangeFormProps = {
  currentStatus: SupportRequestStatus | ServiceRequestStatus;
  requestId: string;
  requestType: RequestType;
};

const terminalStatuses = new Set([
  "closed",
  "cancelled",
  "rejected",
  "completed",
]);

export function StatusChangeForm({
  currentStatus,
  requestId,
  requestType,
}: StatusChangeFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [status, setStatus] = useState(currentStatus);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);
  const definitions = requestType === "support"
    ? supportStatus
    : serviceRequestStatus;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current) {
      return;
    }

    if (
      status !== currentStatus
      && terminalStatuses.has(status)
      && !window.confirm(
        "Confirmer ce statut ? La demande restera traçable et pourra être réouverte manuellement.",
      )
    ) {
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<RequestMutationResponse>(
      `/api/admin/${requestType === "support" ? "support-requests" : "service-requests"}/${encodeURIComponent(requestId)}/status`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ status }),
      },
    );

    if (result.ok) {
      setMessage({
        tone: "success",
        text: result.data.changed
          ? "Le statut a été mis à jour et historisé."
          : "Le statut était déjà sélectionné. Aucun événement supplémentaire n’a été créé.",
      });
      router.refresh();
    } else {
      setMessage({ tone: "error", text: result.error.message });
    }

    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  return (
    <form className="workflow-form" onSubmit={handleSubmit}>
      <div className="field">
        <label htmlFor="request-status">Nouveau statut</label>
        <select
          id="request-status"
          onChange={(event) => setStatus(
            event.target.value as typeof status,
          )}
          value={status}
        >
          {Object.entries(definitions).map(([value, definition]) => (
            <option key={value} value={value}>
              {definition.label}
            </option>
          ))}
        </select>
        <span className="field-hint">
          Ce changement ne déclenche aucune activation automatique.
        </span>
      </div>

      {message ? (
        <FormMessage
          title={message.tone === "success" ? "Statut enregistré" : "Échec"}
          tone={message.tone}
        >
          <p>{message.text}</p>
        </FormMessage>
      ) : null}

      <SubmitButton
        idleLabel="Changer le statut"
        isSubmitting={isSubmitting}
        submittingLabel="Enregistrement…"
      />
    </form>
  );
}
