import { AdminDataTable } from "@/components/AdminDataTable";
import { EmptyState } from "@/components/EmptyState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminServiceRequests } from "@/lib/internal-api";

export const metadata = { title: "Demandes service - Administration" };
export const dynamic = "force-dynamic";

export default async function AdminServiceRequestsPage() {
  await requireAdminSession();
  const result = await getAdminServiceRequests();

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Lecture seule" tone="info" />}
        description="Cette vue ne génère aucun devis, contrat, facture ou paiement."
        eyebrow="Administration interne"
        title="Demandes de service"
      />
      {result.data.length > 0 ? (
        <AdminDataTable
          caption="Demandes de service"
          columns={[
            "Référence",
            "Client",
            "Catalogue",
            "Sujet",
            "Description",
            "Statut",
            "Persistée",
            "Création",
          ]}
          rows={result.data.map((request) => [
            <code key={`${request.reference}-reference`}>
              {request.reference}
            </code>,
            `${request.customerName} (${request.customerReference})`,
            request.catalogItemName,
            request.subject,
            request.descriptionPreview || "Non renseignée",
            request.status,
            request.persisted ? "Oui" : "Non",
            formatDateTime(request.createdAt),
          ])}
        />
      ) : (
        <EmptyState
          description="Aucune demande de service n'est disponible."
          title="Aucune demande"
        />
      )}
      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
