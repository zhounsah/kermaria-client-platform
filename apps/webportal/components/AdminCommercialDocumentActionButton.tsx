"use client";

import type { CommercialDocumentMutationResponse } from "@kermaria/shared";
import { useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type AdminCommercialDocumentActionButtonProps = {
  documentId: string;
  action: "share" | "cancel";
  disabled?: boolean;
};

export function AdminCommercialDocumentActionButton({
  documentId,
  action,
  disabled = false,
}: AdminCommercialDocumentActionButtonProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);

  async function handleAction() {
    if (disabled || isSubmittingRef.current) {
      return;
    }

    if (
      action === "cancel"
      && !window.confirm(
        "Confirmer l'annulation ? Le document restera traçable mais ne deviendra jamais une facture officielle.",
      )
    ) {
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<CommercialDocumentMutationResponse>(
      `/api/admin/commercial-documents/${encodeURIComponent(documentId)}/${action}`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({}),
      },
    );

    if (result.ok) {
      setMessage({
        tone: "success",
        text:
          action === "share"
            ? "Le document a été partagé côté portail client."
            : "Le document a été annulé.",
      });
      router.refresh();
    } else {
      setMessage({ tone: "error", text: result.error.message });
    }

    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  return (
    <div className="workflow-form">
      {message ? (
        <FormMessage
          title={message.tone === "success" ? "Action enregistrée" : "Échec"}
          tone={message.tone}
        >
          <p>{message.text}</p>
        </FormMessage>
      ) : null}
      <SubmitButton
        className={action === "cancel" ? "button button-secondary" : "button"}
        disabled={disabled}
        idleLabel={action === "share" ? "Partager au client" : "Annuler le document"}
        isSubmitting={isSubmitting}
        submittingLabel={action === "share" ? "Partage..." : "Annulation..."}
        onClick={handleAction}
        type="button"
      />
    </div>
  );
}
