import Link from "next/link";

import { AdminCommercialDocumentCreateForm } from "@/components/AdminCommercialDocumentCreateForm";
import { AdminDataTable } from "@/components/AdminDataTable";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
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
  getAdminCommercialDocuments,
  getAdminCustomers,
  getAdminServiceRequests,
  resolveDataSource,
} from "@/lib/internal-api";

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
  const [documentsResult, customersResult, serviceRequestsResult] =
    await Promise.all([
      getAdminCommercialDocuments(),
      getAdminCustomers(),
      getAdminServiceRequests(),
    ]);

  const documents = documentsResult.data.filter((document) =>
    (!customerReference || document.customerReference === customerReference)
    && (!serviceRequestId || document.serviceRequestId === serviceRequestId)
  );
  const source = resolveDataSource([
    documentsResult.source,
    customersResult.source,
    serviceRequestsResult.source,
  ]);

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Suivi commercial" tone="info" />}
        description="Créer des brouillons, ajouter des lignes, partager côté client puis annuler si nécessaire, sans jamais émettre de facture officielle."
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

      {!customersResult.error && !serviceRequestsResult.error ? (
        <SectionCard ariaLabel="Création d'un document commercial">
          <h2>Créer un brouillon</h2>
          <AdminCommercialDocumentCreateForm
            customers={customersResult.data}
            initialCustomerReference={customerReference}
            initialServiceRequestId={serviceRequestId}
            serviceRequests={serviceRequestsResult.data}
          />
        </SectionCard>
      ) : (
        <ErrorState
          compact
          description="Les listes clients ou demandes nécessaires à la création sont indisponibles."
          reference={
            customersResult.error
              ? customersResult.correlationId
              : serviceRequestsResult.correlationId
          }
          title="Création indisponible"
        />
      )}

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
            "Détail",
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
        source={source}
      />
    </>
  );
}

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
