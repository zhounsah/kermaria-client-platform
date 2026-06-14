import Link from "next/link";

import { AdminDataTable } from "@/components/AdminDataTable";
import { AdminRequestFilters } from "@/components/AdminRequestFilters";
import { EmptyState } from "@/components/EmptyState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { RequestStatusBadge } from "@/components/RequestStatusBadge";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminSupportRequestsFiltered } from "@/lib/internal-api";

export const metadata = { title: "Support - Administration" };
export const dynamic = "force-dynamic";

type PageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function AdminSupportRequestsPage({
  searchParams,
}: PageProps) {
  await requireAdminSession();
  const filters = await searchParams;
  const status = first(filters.status);
  const priority = first(filters.priority);
  const order = first(filters.order) ?? "newest";
  const query = new URLSearchParams();
  if (status) query.set("status", status);
  if (priority) query.set("priority", priority);
  query.set("order", order);
  const result = await getAdminSupportRequestsFiltered(`?${query}`);

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Workflow contrôlé" tone="info" />}
        description="Consultez les demandes, filtrez leur suivi et ouvrez un détail pour changer le statut ou ajouter une note."
        eyebrow="Administration interne"
        title="Demandes support"
      />
      <AdminRequestFilters
        order={order}
        priority={priority}
        requestType="support"
        status={status}
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
            "Mise à jour",
            "Détail",
          ]}
          rows={result.data.map((request) => [
            <code key={`${request.id}-reference`}>{request.reference}</code>,
            `${request.customerName} (${request.customerReference})`,
            request.serviceName,
            request.priority,
            <RequestStatusBadge
              key={`${request.id}-status`}
              requestType="support"
              status={request.status}
            />,
            request.subject,
            formatDateTime(request.updatedAt),
            <Link
              className="table-action"
              href={`/admin/support-requests/${encodeURIComponent(request.id)}`}
              key={`${request.id}-detail`}
            >
              Consulter
            </Link>,
          ])}
        />
      ) : (
        <EmptyState
          description="Aucune demande support ne correspond aux filtres."
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

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
