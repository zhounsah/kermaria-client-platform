"use client";

import { useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type IssuedInvoiceInfo = {
  bpceInvoiceId: string;
  fiscalNumber: string | null;
  status: string;
  issueDate: string;
  totalAmountCents: number;
  currency: string;
  pdfAvailable: boolean;
};

type IssueResult = {
  code: string;
  message: string;
  invoice?: IssuedInvoiceInfo;
};

type AdminInvoiceIssuingSectionProps = {
  documentId: string;
  issuable: boolean;
  existingInvoice?: IssuedInvoiceInfo | null;
};

export function AdminInvoiceIssuingSection({
  documentId,
  issuable,
  existingInvoice,
}: AdminInvoiceIssuingSectionProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);
  const [invoice, setInvoice] = useState<IssuedInvoiceInfo | null>(
    existingInvoice ?? null,
  );

  async function handleIssue() {
    if (!issuable || isSubmittingRef.current) return;

    if (
      !window.confirm(
        "Confirmer l'émission de cette facture chez BPCE ? Cette action est permanente : la facture sera numérotée et archivée côté banque.",
      )
    ) {
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<IssueResult>(
      `/api/admin/commercial-documents/${encodeURIComponent(documentId)}/issue`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ sendEmail: false }),
      },
    );

    if (result.ok) {
      setMessage({
        tone: "success",
        text: result.data.message,
      });
      if (result.data.invoice) {
        setInvoice(result.data.invoice);
      }
      router.refresh();
    } else {
      setMessage({ tone: "error", text: result.error.message });
    }

    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  if (invoice) {
    return (
      <div className="stack-list">
        <dl className="request-details">
          <div>
            <dt>Numéro fiscal</dt>
            <dd>{invoice.fiscalNumber ?? "En attente"}</dd>
          </div>
          <div>
            <dt>Référence BPCE</dt>
            <dd>{invoice.bpceInvoiceId}</dd>
          </div>
          <div>
            <dt>Statut BPCE</dt>
            <dd>{invoice.status}</dd>
          </div>
          <div>
            <dt>Date d&apos;émission</dt>
            <dd>{invoice.issueDate}</dd>
          </div>
        </dl>
        {invoice.pdfAvailable ? (
          <a
            className="button button-secondary"
            download
            href={`/api/admin/commercial-documents/${encodeURIComponent(documentId)}/invoice/pdf`}
          >
            Télécharger le PDF
          </a>
        ) : (
          <p className="form-hint">PDF en cours de génération — réessayez dans quelques instants.</p>
        )}
      </div>
    );
  }

  return (
    <div className="workflow-form">
      {message ? (
        <FormMessage
          title={message.tone === "success" ? "Facture émise" : "Échec"}
          tone={message.tone}
        >
          <p>{message.text}</p>
        </FormMessage>
      ) : null}
      {!issuable ? (
        <p className="form-hint">
          L&apos;émission est disponible uniquement pour les documents partagés avec le client.
        </p>
      ) : null}
      <SubmitButton
        className="button"
        disabled={!issuable}
        idleLabel="Émettre la facture BPCE"
        isSubmitting={isSubmitting}
        submittingLabel="Émission en cours..."
        onClick={handleIssue}
        type="button"
      />
    </div>
  );
}
