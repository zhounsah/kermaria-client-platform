import { AdminDataTable } from "@/components/AdminDataTable";
import { AuditEventBadge } from "@/components/AuditEventBadge";
import { EmptyState } from "@/components/EmptyState";
import { MetricCard } from "@/components/MetricCard";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminOverview } from "@/lib/internal-api";

export const metadata = {
  title: "Administration",
};

export const dynamic = "force-dynamic";

export default async function AdminOverviewPage() {
  await requireAdminSession();
  const result = await getAdminOverview();
  const overview = result.data;

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Lecture seule" tone="info" />}
        description="Suivi interne minimal de l'activité du portail. Aucune action destructive ou opération Active Directory n'est disponible."
        eyebrow="Administration interne"
        title="Vue d'ensemble"
      />

      {overview ? (
        <>
          <div className="metrics-grid admin-metrics">
            <MetricCard
              detail="Clients référencés"
              label="Clients"
              value={String(overview.customerCount)}
            />
            <MetricCard
              detail="Comptes actifs"
              label="Utilisateurs"
              tone="green"
              value={String(overview.activeUserCount)}
            />
            <MetricCard
              detail="Non révoquées et non expirées"
              label="Sessions actives"
              tone="slate"
              value={String(overview.activeSessionCount)}
            />
            <MetricCard
              detail="Demandes non clôturées"
              label="Support ouvert"
              tone="amber"
              value={String(overview.openSupportRequestCount)}
            />
            <MetricCard
              detail="Créées sur les 30 derniers jours"
              label="Demandes service"
              value={String(overview.recentServiceRequestCount)}
            />
          </div>

          <section className="content-panel admin-safety-panel">
            <div>
              <span className="card-kicker">État des intégrations</span>
              <h2>Active Directory : {overview.adMode}</h2>
              <p>
                Les opérations AD réelles restent désactivées. Cette interface
                ne provisionne aucun compte, VPN ou accès RDS.
              </p>
            </div>
            <StatusBadge label="Aucune opération active" tone="warning" />
          </section>

          {overview.recentAudits.length > 0 ? (
            <AdminDataTable
              caption="Derniers événements d'audit"
              columns={[
                "Date",
                "Acteur",
                "Action",
                "Résultat",
                "Client",
                "Corrélation",
              ]}
              rows={overview.recentAudits.map((audit) => [
                formatDateTime(audit.occurredAt),
                audit.actor,
                audit.action,
                <AuditEventBadge
                  key={`${audit.correlationId}-outcome`}
                  outcome={audit.outcome}
                />,
                audit.customerReference ?? "Système",
                <code key={`${audit.correlationId}-id`}>
                  {audit.correlationId}
                </code>,
              ])}
            />
          ) : (
            <EmptyState
              description="Aucun événement d'audit récent n'est disponible."
              title="Aucun audit"
            />
          )}
        </>
      ) : (
        <EmptyState
          description="Les données d'administration sont temporairement indisponibles."
          title="Vue d'ensemble indisponible"
        />
      )}

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
