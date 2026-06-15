"use client";

import type {
  AdminCommercialDocumentDetail,
  AdminServiceRequestSummary,
  CommercialDocumentMutationResponse,
} from "@kermaria/shared";
import { FormEvent, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type AdminCommercialDocumentEditFormProps = {
  document: AdminCommercialDocumentDetail;
  serviceRequests: AdminServiceRequestSummary[];
};

export function AdminCommercialDocumentEditForm({
  document,
  serviceRequests,
}: AdminCommercialDocumentEditFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [title, setTitle] = useState(document.title);
  const [documentType, setDocumentType] = useState(document.documentType);
  const [status, setStatus] = useState(
    document.status === "pending_review" ? "pending_review" : "draft",
  );
  const [serviceRequestId, setServiceRequestId] = useState(
    document.serviceRequestId ?? "",
  );
  const [disclaimer, setDisclaimer] = useState(document.disclaimer);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);
  const isDraft = document.status === "draft";
  const matchingServiceRequests = serviceRequests.filter((request) =>
    request.customerReference === document.customerReference
  );

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current || !isDraft) {
      return;
    }

    const payload = {
      customerReference: document.customerReference,
      documentType,
      title: title.trim(),
      currency: "EUR" as const,
      serviceRequestId: serviceRequestId.trim() || null,
      disclaimer: disclaimer.trim(),
      status,
    };

    if (payload.title.length < 3 || payload.disclaimer.length < 10) {
      setMessage({
        tone: "error",
        text: "Le brouillon doit conserver un titre et un disclaimer valides.",
      });
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<CommercialDocumentMutationResponse>(
      `/api/admin/commercial-documents/${encodeURIComponent(document.id)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );

    if (result.ok) {
      setMessage({
        tone: "success",
        text: "Le brouillon a été mis à jour.",
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
      <label>
        Titre
        <input
          disabled={!isDraft}
          maxLength={200}
          onChange={(event) => setTitle(event.target.value)}
          value={title}
        />
      </label>
      <div className="form-grid">
        <label>
          Type
          <select
            disabled={!isDraft}
            onChange={(event) => setDocumentType(
              event.target.value as typeof documentType,
            )}
            value={documentType}
          >
            <option value="quote_draft">Devis / proposition</option>
            <option value="billing_draft">Brouillon de suivi</option>
            <option value="informational_invoice">
              Document de facturation informatif
            </option>
          </select>
        </label>
        <label>
          Statut de préparation
          <select
            disabled={!isDraft}
            onChange={(event) => setStatus(
              event.target.value as typeof status,
            )}
            value={status}
          >
            <option value="draft">Brouillon</option>
            <option value="pending_review">À vérifier</option>
          </select>
        </label>
      </div>
      <label>
        Demande de service liée
        <select
          disabled={!isDraft}
          onChange={(event) => setServiceRequestId(event.target.value)}
          value={serviceRequestId}
        >
          <option value="">Aucune</option>
          {matchingServiceRequests.map((request) => (
            <option key={request.id} value={request.id}>
              {request.reference} - {request.subject}
            </option>
          ))}
        </select>
      </label>
      <label>
        Disclaimer
        <textarea
          disabled={!isDraft}
          maxLength={500}
          onChange={(event) => setDisclaimer(event.target.value)}
          rows={4}
          value={disclaimer}
        />
      </label>
      {!isDraft ? (
        <FormMessage title="Édition verrouillée" tone="info">
          <p>Seuls les brouillons permettent encore des modifications.</p>
        </FormMessage>
      ) : null}
      {message ? (
        <FormMessage
          title={message.tone === "success" ? "Brouillon enregistré" : "Échec"}
          tone={message.tone}
        >
          <p>{message.text}</p>
        </FormMessage>
      ) : null}
      <SubmitButton
        disabled={!isDraft}
        idleLabel="Mettre à jour le brouillon"
        isSubmitting={isSubmitting}
        submittingLabel="Enregistrement..."
      />
    </form>
  );
}
