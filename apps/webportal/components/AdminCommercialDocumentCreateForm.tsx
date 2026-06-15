"use client";

import type {
  AdminCustomerSummary,
  AdminServiceRequestSummary,
  CommercialDocumentMutationResponse,
} from "@kermaria/shared";
import { FormEvent, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

const defaultDisclaimer =
  "Document informatif - ne constitue pas une facture officielle.";

type AdminCommercialDocumentCreateFormProps = {
  customers: AdminCustomerSummary[];
  serviceRequests: AdminServiceRequestSummary[];
  initialCustomerReference?: string | null;
  initialServiceRequestId?: string | null;
};

export function AdminCommercialDocumentCreateForm({
  customers,
  serviceRequests,
  initialCustomerReference,
  initialServiceRequestId,
}: AdminCommercialDocumentCreateFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [customerReference, setCustomerReference] = useState(
    initialCustomerReference ?? customers[0]?.customerReference ?? "",
  );
  const [serviceRequestId, setServiceRequestId] = useState(
    initialServiceRequestId ?? "",
  );
  const [documentType, setDocumentType] = useState<
    "quote_draft" | "billing_draft" | "informational_invoice"
  >("quote_draft");
  const [status, setStatus] = useState<"draft" | "pending_review">("draft");
  const [title, setTitle] = useState("Proposition commerciale à vérifier");
  const [disclaimer, setDisclaimer] = useState(defaultDisclaimer);
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

    const payload = {
      customerReference: customerReference.trim(),
      documentType,
      title: title.trim(),
      currency: "EUR" as const,
      serviceRequestId: serviceRequestId.trim() || null,
      disclaimer: disclaimer.trim(),
      status,
    };

    if (
      !payload.customerReference
      || payload.title.length < 3
      || payload.disclaimer.length < 10
    ) {
      setMessage({
        tone: "error",
        text: "Complétez le brouillon avant de le créer.",
      });
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<CommercialDocumentMutationResponse>(
      "/api/admin/commercial-documents",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );

    if (result.ok) {
      router.push(
        `/admin/commercial-documents/${encodeURIComponent(result.data.id)}`,
      );
      router.refresh();
      return;
    }

    setMessage({ tone: "error", text: result.error.message });
    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  const filteredServiceRequests = serviceRequests.filter((request) =>
    !customerReference
    || request.customerReference === customerReference
  );

  return (
    <form className="form-card" onSubmit={handleSubmit}>
      <div className="form-grid">
        <label>
          Client
          <select
            onChange={(event) => setCustomerReference(event.target.value)}
            value={customerReference}
          >
            {customers.map((customer) => (
              <option
                key={customer.customerReference}
                value={customer.customerReference}
              >
                {customer.displayName} ({customer.customerReference})
              </option>
            ))}
          </select>
        </label>
        <label>
          Type de document
          <select
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
      </div>
      <div className="form-grid">
        <label>
          Statut initial
          <select
            onChange={(event) => setStatus(
              event.target.value as typeof status,
            )}
            value={status}
          >
            <option value="draft">Brouillon</option>
            <option value="pending_review">À vérifier</option>
          </select>
        </label>
        <label>
          Demande de service liée
          <select
            onChange={(event) => setServiceRequestId(event.target.value)}
            value={serviceRequestId}
          >
            <option value="">Aucune</option>
            {filteredServiceRequests.map((request) => (
              <option key={request.id} value={request.id}>
                {request.reference} - {request.subject}
              </option>
            ))}
          </select>
        </label>
      </div>
      <label>
        Titre
        <input
          maxLength={200}
          onChange={(event) => setTitle(event.target.value)}
          value={title}
        />
      </label>
      <label>
        Disclaimer
        <textarea
          maxLength={500}
          onChange={(event) => setDisclaimer(event.target.value)}
          rows={4}
          value={disclaimer}
        />
      </label>
      {message ? (
        <FormMessage
          title="Échec"
          tone={message.tone}
        >
          <p>{message.text}</p>
        </FormMessage>
      ) : null}
      <SubmitButton
        idleLabel="Créer le brouillon"
        isSubmitting={isSubmitting}
        submittingLabel="Création..."
      />
    </form>
  );
}
