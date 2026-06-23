import Link from "next/link";
import { notFound } from "next/navigation";

import { CommercialDocumentLineTable } from "@/components/CommercialDocumentLineTable";
import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
import {
  commercialDocumentStatus,
  commercialDocumentType,
  formatCurrencyFromCents,
  formatDateTime,
} from "@/lib/formatters";
import { getCommercialDocument } from "@/lib/internal-api";

export const metadata = {
  title: "Détail document commercial",
};

export const dynamic = "force-dynamic";

type PageProps = { params: Promise<{ id: string }> };

export default async function CommercialDocumentDetailPage({
  params,
}: PageProps) {
  await requireClientSession();
  const { id } = await params;
  const result = await getCommercialDocument(id);

  if (result.error) {
    return (
      <ErrorState
        action={
          <Link className="button" href="/invoices">
            Retour aux documents
          </Link>
        }
        description="Impossible de charger ce document commercial informatif."
        reference={result.correlationId}
        title="Document indisponible"
      />
    );
  }

  if (!result.data) {
    notFound();
  }

  const document = result.data;
  const status = commercialDocumentStatus[document.status] ?? {
    label: document.status,
    tone: "slate" as const,
  };
  const isIssued = document.status === "issued";

  return (
    <>
      <PageHeader
        action={<StatusBadge label={status.label} tone={status.tone} />}
        description={commercialDocumentType[document.documentType] ?? document.documentType}
        eyebrow={document.internalReference}
        title={document.title}
      />

      {!isIssued ? (
        <div className="security-warning">
          <span className="warning-symbol" aria-hidden="true">!</span>
          <div>
            <strong>Document commercial — ne constitue pas encore une facture.</strong>
            <p>
              Ce document est partagé à titre informatif. Votre prestataire
              vous contactera pour la suite.
            </p>
          </div>
        </div>
      ) : null}

      <div className="request-detail-layout">
        <SectionCard ariaLabel="Informations du document commercial">
          <h2>Informations générales</h2>
          <dl className="request-details">
            <div><dt>Type</dt><dd>{commercialDocumentType[document.documentType] ?? document.documentType}</dd></div>
            <div><dt>Statut</dt><dd>{status.label}</dd></div>
            <div><dt>Créé le</dt><dd>{formatDateTime(document.createdAt)}</dd></div>
            <div><dt>Mise à jour</dt><dd>{formatDateTime(document.updatedAt)}</dd></div>
            <div><dt>Partagé le</dt><dd>{document.sharedAt ? formatDateTime(document.sharedAt) : "Non partagé"}</dd></div>
            <div><dt>Demande liée</dt><dd>{document.serviceRequestId && document.serviceRequestReference ? <Link href={`/request-service/${encodeURIComponent(document.serviceRequestId)}`}>{document.serviceRequestReference}</Link> : "Aucune"}</dd></div>
          </dl>
        </SectionCard>

        <SectionCard ariaLabel="Synthèse financière">
          <h2>{isIssued ? "Montants facturés" : "Synthèse indicative"}</h2>
          <dl className="request-details">
            <div><dt>Sous-total HT</dt><dd>{formatCurrencyFromCents(document.subtotalAmountCents)}</dd></div>
            <div><dt>Taxes</dt><dd>{formatCurrencyFromCents(document.taxAmountCents)}</dd></div>
            <div><dt>Total {isIssued ? "facturé" : "indicatif"}</dt><dd>{formatCurrencyFromCents(document.totalAmountCents)}</dd></div>
            <div><dt>Devise</dt><dd>{document.currency}</dd></div>
          </dl>
          {!isIssued ? (
            <p className="request-description">{document.disclaimer}</p>
          ) : null}
        </SectionCard>
      </div>

      <CommercialDocumentLineTable lines={document.lines} />
    </>
  );
}
