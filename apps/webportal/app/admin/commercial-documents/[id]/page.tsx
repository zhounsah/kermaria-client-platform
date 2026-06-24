import Link from "next/link";
import { notFound } from "next/navigation";

import { AdminCommercialDocumentActionButton } from "@/components/AdminCommercialDocumentActionButton";
import { AdminCommercialDocumentEditForm } from "@/components/AdminCommercialDocumentEditForm";
import { AdminCommercialDocumentLineForm } from "@/components/AdminCommercialDocumentLineForm";
import { AdminInvoiceIssuingSection } from "@/components/AdminInvoiceIssuingSection";
import { AdminSendReminderButton } from "@/components/AdminSendReminderButton";
import { CommercialDocumentLineTable } from "@/components/CommercialDocumentLineTable";
import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import {
  commercialDocumentStatus,
  commercialDocumentType,
  formatCurrencyFromCents,
  formatDateTime,
} from "@/lib/formatters";
import {
  getAdminCatalog,
  getAdminCommercialDocument,
  getAdminCommercialDocumentInvoice,
  getAdminServiceRequests,
} from "@/lib/internal-api";

export const metadata = {
  title: "Détail document commercial - Administration",
};

export const dynamic = "force-dynamic";

type PageProps = { params: Promise<{ id: string }> };

export default async function AdminCommercialDocumentDetailPage({
  params,
}: PageProps) {
  await requireAdminSession();
  const { id } = await params;
  const [documentResult, catalogResult, serviceRequestsResult, invoiceResult] =
    await Promise.all([
      getAdminCommercialDocument(id),
      getAdminCatalog(),
      getAdminServiceRequests(),
      getAdminCommercialDocumentInvoice(id),
    ]);

  if (documentResult.error) {
    return (
      <ErrorState
        action={
          <Link className="button" href="/admin/commercial-documents">
            Retour
          </Link>
        }
        description="Impossible de charger ce document commercial."
        reference={documentResult.correlationId}
        title="Document indisponible"
      />
    );
  }

  if (!documentResult.data) {
    notFound();
  }

  const document = documentResult.data;
  const status = commercialDocumentStatus[document.status] ?? {
    label: document.status,
    tone: "slate",
  };
  const isDraft = document.status === "draft";
  const canShare = document.status === "draft" || document.status === "pending_review";
  const canCancel = document.status !== "cancelled" && document.status !== "issued";
  const canIssue = document.status === "shared_with_customer";
  const isIssued = document.status === "issued";
  const canSendReminder = document.status === "issued";

  return (
    <>
      <PageHeader
        action={<StatusBadge label={status.label} tone={status.tone} />}
        description={`${document.customerName} · ${document.customerReference}`}
        eyebrow={document.internalReference}
        title={document.title}
      />

      {!isIssued ? (
        <section className="content-panel admin-safety-panel">
          <div>
            <span className="card-kicker">Phase de tests</span>
            <h2>Document commercial informatif</h2>
            <p>
              Ce document sert au suivi commercial interne et à l&apos;information
              du client. Il peut être émis en facture officielle via BPCE depuis la
              section « Émission » ci-dessous, uniquement lorsque son statut est
              « Partagé avec le client ».
            </p>
          </div>
          <StatusBadge label="Aucun paiement depuis ce portail" tone="warning" />
        </section>
      ) : (
        <section className="content-panel admin-safety-panel">
          <div>
            <span className="card-kicker">Facture officielle</span>
            <h2>Facture émise chez BPCE</h2>
            <p>
              Ce document a été émis en facture officielle. Il est numéroté, archivé
              côté banque et ne peut plus être modifié.
            </p>
          </div>
          <StatusBadge label="Facture émise" tone="success" />
        </section>
      )}

      <div className="request-detail-layout">
        <SectionCard ariaLabel="Informations générales du document">
          <h2>Informations générales</h2>
          <dl className="request-details">
            <div><dt>Type</dt><dd>{commercialDocumentType[document.documentType]}</dd></div>
            <div><dt>Créé par</dt><dd>{document.createdByDisplayName}</dd></div>
            <div><dt>Créé le</dt><dd>{formatDateTime(document.createdAt)}</dd></div>
            <div><dt>Mise à jour</dt><dd>{formatDateTime(document.updatedAt)}</dd></div>
            <div><dt>Partagé le</dt><dd>{document.sharedAt ? formatDateTime(document.sharedAt) : "Non partagé"}</dd></div>
            <div><dt>Demande liée</dt><dd>{document.serviceRequestId && document.serviceRequestReference ? <Link href={`/admin/service-requests/${encodeURIComponent(document.serviceRequestId)}`}>{document.serviceRequestReference}</Link> : "Aucune"}</dd></div>
            <div><dt>Sous-total HT</dt><dd>{formatCurrencyFromCents(document.subtotalAmountCents)}</dd></div>
            <div><dt>Total informatif</dt><dd>{formatCurrencyFromCents(document.totalAmountCents)}</dd></div>
          </dl>
          <p className="request-description">{document.disclaimer}</p>
        </SectionCard>

        <SectionCard ariaLabel="Mise à jour du brouillon">
          <h2>Mettre à jour le document</h2>
          {!serviceRequestsResult.error ? (
            <AdminCommercialDocumentEditForm
              document={document}
              serviceRequests={serviceRequestsResult.data}
            />
          ) : (
            <ErrorState
              compact
              description="Les demandes de service ne peuvent pas être chargées pour l'édition."
              reference={serviceRequestsResult.correlationId}
              title="Édition partielle"
            />
          )}
        </SectionCard>
      </div>

      <div className="request-detail-layout">
        <SectionCard ariaLabel="Actions de partage et d'annulation">
          <h2>Actions</h2>
          <div className="stack-list">
            <AdminCommercialDocumentActionButton
              action="share"
              disabled={!canShare}
              documentId={document.id}
            />
            <AdminCommercialDocumentActionButton
              action="cancel"
              disabled={!canCancel}
              documentId={document.id}
            />
          </div>
        </SectionCard>

        <SectionCard ariaLabel="Émission de la facture BPCE">
          <h2>Émission</h2>
          <AdminInvoiceIssuingSection
            documentId={document.id}
            existingInvoice={invoiceResult.data ?? null}
            issuable={canIssue}
          />
        </SectionCard>

        {canSendReminder ? (
          <SectionCard ariaLabel="Relance de paiement">
            <h2>Relance</h2>
            <p className="form-hint">
              Envoie un e-mail de relance au contact de facturation du client.
            </p>
            <AdminSendReminderButton documentId={document.id} />
          </SectionCard>
        ) : null}

        <SectionCard ariaLabel="Ajout de ligne">
          <h2>Ajouter une ligne</h2>
          {!catalogResult.error ? (
            <AdminCommercialDocumentLineForm
              disabled={!isDraft}
              documentId={document.id}
              offers={catalogResult.data}
            />
          ) : (
            <ErrorState
              compact
              description="Le catalogue administré est indisponible pour l'ajout de lignes."
              reference={catalogResult.correlationId}
              title="Catalogue indisponible"
            />
          )}
        </SectionCard>
      </div>

      <CommercialDocumentLineTable lines={document.lines} />

      {!catalogResult.error && document.lines.length > 0 ? (
        <section className="request-history-section">
          <div className="section-heading">
            <div>
              <h2>Modifier les lignes existantes</h2>
              <p>Les lignes restent modifiables uniquement tant que le document est brouillon.</p>
            </div>
          </div>
          <div className="stack-panels">
            {document.lines.map((line) => (
              <SectionCard
                ariaLabel={`Ligne ${line.label}`}
                className="stack-panel"
                key={line.id}
              >
                <h2>{line.label}</h2>
                <AdminCommercialDocumentLineForm
                  disabled={!isDraft}
                  documentId={document.id}
                  line={line}
                  offers={catalogResult.data}
                />
              </SectionCard>
            ))}
          </div>
        </section>
      ) : null}
    </>
  );
}
