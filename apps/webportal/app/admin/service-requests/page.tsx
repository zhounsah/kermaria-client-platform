import Link from "next/link";

import { AdminDataTable } from "@/components/AdminDataTable";
import { AdminRequestFilters } from "@/components/AdminRequestFilters";
import { EmptyState } from "@/components/EmptyState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { RequestStatusBadge } from "@/components/RequestStatusBadge";
import { RequestAttentionBadge } from "@/components/RequestAttentionBadge";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminServiceRequestsFiltered } from "@/lib/internal-api";

export const metadata = { title: "Demandes service - Administration" };
export const dynamic = "force-dynamic";

type PageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function AdminServiceRequestsPage({
  searchParams,
}: PageProps) {
  await requireAdminSession();
  const filters = await searchParams;
  const status = first(filters.status);
  const attention = first(filters.attention);
  const order = first(filters.order) ?? "newest";
  const query = new URLSearchParams();
  if (status) query.set("status", status);
  if (attention) query.set("attention", attention);
  query.set("order", order);
  const result = await getAdminServiceRequestsFiltered(`?${query}`);

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Traitement manuel" tone="info" />}
        description="Le workflow suit l’étude de la demande sans créer de devis, facture, paiement ou service automatiquement."
        eyebrow="Administration interne"
        title="Demandes de service"
      />
      <AdminRequestFilters
        attention={attention}
        order={order}
        requestType="service"
        status={status}
      />
      {result.data.length > 0 ? (
        <AdminDataTable
          caption="Demandes de service"
          columns={[
            "Référence",
            "Client",
            "Catalogue",
            "Sujet",
            "Statut",
            "Suivi",
            "Mise à jour",
            "Détail",
          ]}
          rows={result.data.map((request) => [
            <code key={`${request.id}-reference`}>{request.reference}</code>,
            `${request.customerName} (${request.customerReference})`,
            request.catalogItemName,
            request.subject,
            <RequestStatusBadge
              key={`${request.id}-status`}
              requestType="service"
              status={request.status}
            />,
            <RequestAttentionBadge
              hasRecentClientReply={request.hasRecentClientReply}
              key={`${request.id}-attention`}
              requiresAttention={request.requiresAttention}
            />,
            formatDateTime(request.updatedAt),
            <Link
              className="table-action"
              href={`/admin/service-requests/${encodeURIComponent(request.id)}`}
              key={`${request.id}-detail`}
            >
              Consulter
            </Link>,
          ])}
        />
      ) : (
        <EmptyState
          description="Aucune demande de service ne correspond aux filtres."
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
