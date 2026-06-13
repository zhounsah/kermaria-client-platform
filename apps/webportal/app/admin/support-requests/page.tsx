import { AdminDataTable } from "@/components/AdminDataTable";
import { EmptyState } from "@/components/EmptyState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminSupportRequests } from "@/lib/internal-api";

export const metadata = { title: "Support - Administration" };
export const dynamic = "force-dynamic";

export default async function AdminSupportRequestsPage() {
  await requireAdminSession();
  const result = await getAdminSupportRequests();

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Lecture seule" tone="info" />}
        description="Les demandes peuvent être consultées, mais ni assignées, ni clôturées, ni supprimées dans cette version."
        eyebrow="Administration interne"
        title="Demandes support"
      />
      {result.data.length > 0 ? (
        <AdminDataTable
          caption="Demandes support"
          columns={[
            "Référence",
            "Client",
            "Service",
            "Priorité",
            "Statut",
            "Sujet",
            "Création",
            "Mise à jour",
          ]}
          rows={result.data.map((request) => [
            <code key={`${request.reference}-reference`}>
              {request.reference}
            </code>,
            `${request.customerName} (${request.customerReference})`,
            request.serviceName,
            request.priority,
            request.status,
            request.subject,
            formatDateTime(request.createdAt),
            formatDateTime(request.updatedAt),
          ])}
        />
      ) : (
        <EmptyState
          description="Aucune demande support n'est disponible."
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
