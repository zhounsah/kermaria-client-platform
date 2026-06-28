import Link from "next/link";

import { AdminDataTable } from "@/components/AdminDataTable";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import {
  commercialDocumentStatus,
  commercialDocumentType,
  formatCurrencyFromCents,
  formatDateTime,
} from "@/lib/formatters";
import { getAdminCommercialDocuments } from "@/lib/internal-api";

export const metadata = {
  title: "Documents commerciaux - Administration",
};

export const dynamic = "force-dynamic";

type PageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function AdminCommercialDocumentsPage({
  searchParams,
}: PageProps) {
  await requireAdminSession();
  const filters = await searchParams;
  const customerReference = first(filters.customerReference);
  const serviceRequestId = first(filters.serviceRequestId);
  const documentsResult = await getAdminCommercialDocuments();

  const documents = documentsResult.data.filter((document) =>
    (!customerReference || document.customerReference === customerReference)
    && (!serviceRequestId || document.serviceRequestId === serviceRequestId)
  );

  const newDraftHref = (() => {
    const params = new URLSearchParams();
    if (customerReference) params.set("customerReference", customerReference);
    if (serviceRequestId) params.set("serviceRequestId", serviceRequestId);
    const query = params.toString();
    return query
      ? `/admin/commercial-documents/new?${query}`
      : "/admin/commercial-documents/new";
  })();

  return (
    <>
      <PageHeader
        action={
          <Link className="button" href={newDraftHref}>
            Nouveau brouillon
          </Link>
        }
        description="Suivi des brouillons, propositions et documents informatifs partagés avec les clients."
        eyebrow="Administration interne"
        title="Documents commerciaux"
      />

      <section className="content-panel admin-safety-panel">
        <div>
          <span className="card-kicker">Avertissement</span>
          <h2>Documents strictement informatifs</h2>
          <p>
            Ces documents sont informatifs et ne constituent pas des factures
            officielles. Aucun paiement n&apos;est possible depuis le portail.
          </p>
        </div>
        <StatusBadge label="Aucune numérotation fiscale" tone="warning" />
      </section>

      {documentsResult.error ? (
        <ErrorState
          description="Impossible de charger les documents commerciaux pour le moment."
          reference={documentsResult.correlationId}
          title="Documents indisponibles"
        />
      ) : documents.length === 0 ? (
        <EmptyState
          description="Aucun document ne correspond au périmètre demandé."
          title="Aucun document"
        />
      ) : (
        <AdminDataTable
          caption="Documents commerciaux"
          columns={[
            "Référence",
            "Client",
            "Titre",
            "Type",
            "Statut",
            "Demande liée",
            "Total",
            "Mise à jour",
            "Action",
          ]}
          rows={documents.map((document) => {
            const status = commercialDocumentStatus[document.status];
            return [
              <code key={`${document.id}-reference`}>
                {document.internalReference}
              </code>,
              `${document.customerName} (${document.customerReference})`,
              document.title,
              commercialDocumentType[document.documentType],
              <StatusBadge
                key={`${document.id}-status`}
                label={status.label}
                tone={status.tone}
              />,
              document.serviceRequestReference ?? "Aucune",
              formatCurrencyFromCents(document.totalAmountCents),
              formatDateTime(document.updatedAt),
              <Link
                className="table-action"
                href={`/admin/commercial-documents/${encodeURIComponent(document.id)}`}
                key={`${document.id}-detail`}
              >
                Consulter
              </Link>,
            ];
          })}
        />
      )}

      <MockNotice
        correlationId={documentsResult.correlationId}
        source={documentsResult.source}
      />
    </>
  );
}

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
