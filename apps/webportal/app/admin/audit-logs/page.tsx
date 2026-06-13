import { AdminDataTable } from "@/components/AdminDataTable";
import { AuditEventBadge } from "@/components/AuditEventBadge";
import { EmptyState } from "@/components/EmptyState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminAuditLogs } from "@/lib/internal-api";

export const metadata = { title: "Audits - Administration" };
export const dynamic = "force-dynamic";

export default async function AdminAuditLogsPage() {
  await requireAdminSession();
  const result = await getAdminAuditLogs();

  return (
    <>
      <PageHeader
        action={<StatusBadge label="100 derniers événements" tone="info" />}
        description="Les audits ne contiennent ni mot de passe, ni token, ni cookie, ni payload sensible complet."
        eyebrow="Administration interne"
        title="Journal d'audit"
      />
      {result.data.length > 0 ? (
        <AdminDataTable
          caption="Journal d'audit récent"
          columns={[
            "Date",
            "Acteur",
            "Action",
            "Résultat",
            "Motif",
            "Client",
            "Corrélation",
            "Adresse",
          ]}
          rows={result.data.map((audit) => [
            formatDateTime(audit.occurredAt),
            audit.actor,
            audit.action,
            <AuditEventBadge
              key={`${audit.correlationId}-outcome`}
              outcome={audit.outcome}
            />,
            audit.reasonCode ?? "-",
            audit.customerReference ?? "Système",
            <code key={`${audit.correlationId}-id`}>
              {audit.correlationId}
            </code>,
            audit.sourceAddress ?? "Non disponible",
          ])}
        />
      ) : (
        <EmptyState
          description="Aucun audit récent n'est disponible."
          title="Aucun audit"
        />
      )}
      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
