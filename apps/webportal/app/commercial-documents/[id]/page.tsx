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
  const status = commercialDocumentStatus[document.status];

  return (
    <>
      <PageHeader
        action={<StatusBadge label={status.label} tone={status.tone} />}
        description={commercialDocumentType[document.documentType]}
        eyebrow={document.internalReference}
        title={document.title}
      />

      <div className="security-warning">
        <span className="warning-symbol" aria-hidden="true">
          !
        </span>
        <div>
          <strong>Document informatif - ne constitue pas une facture officielle.</strong>
          <p>
            Ce document sert au suivi commercial interne et à l&apos;information
            du client. Aucun paiement n&apos;est possible depuis le portail.
          </p>
        </div>
      </div>

      <div className="request-detail-layout">
        <SectionCard ariaLabel="Informations du document commercial">
          <h2>Informations générales</h2>
          <dl className="request-details">
            <div><dt>Type</dt><dd>{commercialDocumentType[document.documentType]}</dd></div>
            <div><dt>Statut</dt><dd>{status.label}</dd></div>
            <div><dt>Créé le</dt><dd>{formatDateTime(document.createdAt)}</dd></div>
            <div><dt>Mise à jour</dt><dd>{formatDateTime(document.updatedAt)}</dd></div>
            <div><dt>Partagé le</dt><dd>{document.sharedAt ? formatDateTime(document.sharedAt) : "Non partagé"}</dd></div>
            <div><dt>Demande liée</dt><dd>{document.serviceRequestId && document.serviceRequestReference ? <Link href={`/request-service/${encodeURIComponent(document.serviceRequestId)}`}>{document.serviceRequestReference}</Link> : "Aucune"}</dd></div>
          </dl>
        </SectionCard>

        <SectionCard ariaLabel="Synthèse financière informative">
          <h2>Synthèse informative</h2>
          <dl className="request-details">
            <div><dt>Sous-total HT</dt><dd>{formatCurrencyFromCents(document.subtotalAmountCents)}</dd></div>
            <div><dt>Taxes indicatives</dt><dd>{formatCurrencyFromCents(document.taxAmountCents)}</dd></div>
            <div><dt>Total informatif</dt><dd>{formatCurrencyFromCents(document.totalAmountCents)}</dd></div>
            <div><dt>Devise</dt><dd>{document.currency}</dd></div>
          </dl>
          <p className="request-description">{document.disclaimer}</p>
        </SectionCard>
      </div>

      <CommercialDocumentLineTable lines={document.lines} />
    </>
  );
}
