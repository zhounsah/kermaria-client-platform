import Link from "next/link";

import { AdminDataTable } from "@/components/AdminDataTable";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { RequestStatusBadge } from "@/components/RequestStatusBadge";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminActivity } from "@/lib/internal-api";

export const metadata = { title: "Flux d'activité - Administration" };
export const dynamic = "force-dynamic";

export default async function AdminActivityPage() {
  await requireAdminSession();
  const result = await getAdminActivity();
  const activity = result.data;

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Lecture seule" tone="info" />}
        description="Derniers messages publics échangés sur les demandes support et service. Pour les événements techniques, voir le journal d'audit."
        eyebrow="Administration interne"
        title="Flux d'activité"
      />

      {result.error ? (
        <ErrorState
          description="Le flux d'activité est temporairement indisponible."
          reference={result.correlationId}
          title="Activité indisponible"
        />
      ) : !activity || activity.recentActivities.length === 0 ? (
        <EmptyState
          description="Aucun message public récent n'est disponible."
          title="Aucune activité publique"
        />
      ) : (
        <AdminDataTable
          caption="Dernières activités publiques"
          columns={[
            "Date",
            "Demande",
            "Client",
            "Sujet",
            "Auteur",
            "Statut",
            "Détail",
          ]}
          rows={activity.recentActivities.map((item) => [
            formatDateTime(item.occurredAt),
            <code key={`${item.requestType}-${item.requestId}-reference`}>
              {item.reference}
            </code>,
            `${item.customerName} (${item.customerReference})`,
            item.subject,
            <StatusBadge
              key={`${item.requestType}-${item.requestId}-author`}
              label={
                item.authorType === "client"
                  ? "Réponse client"
                  : "Équipe Kermaria"
              }
              tone={item.authorType === "client" ? "warning" : "info"}
            />,
            <RequestStatusBadge
              key={`${item.requestType}-${item.requestId}-status`}
              requestType={item.requestType}
              status={item.status}
            />,
            <Link
              className="table-action"
              href={
                item.requestType === "support"
                  ? `/admin/support-requests/${encodeURIComponent(item.requestId)}`
                  : `/admin/service-requests/${encodeURIComponent(item.requestId)}`
              }
              key={`${item.requestType}-${item.requestId}-detail`}
            >
              Consulter
            </Link>,
          ])}
        />
      )}

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
