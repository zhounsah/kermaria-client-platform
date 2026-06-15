import Link from "next/link";

import { AdminDataTable } from "@/components/AdminDataTable";
import { AuditEventBadge } from "@/components/AuditEventBadge";
import { EmptyState } from "@/components/EmptyState";
import { MetricCard } from "@/components/MetricCard";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { RequestStatusBadge } from "@/components/RequestStatusBadge";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import {
  getAdminActivity,
  getAdminOverview,
} from "@/lib/internal-api";

export const metadata = {
  title: "Administration",
};

export const dynamic = "force-dynamic";

export default async function AdminOverviewPage() {
  await requireAdminSession();
  const [overviewResult, activityResult] = await Promise.all([
    getAdminOverview(),
    getAdminActivity(),
  ]);
  const overview = overviewResult.data;
  const activity = activityResult.data;

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Lecture seule" tone="info" />}
        description="Suivi interne minimal de l'activité du portail. Aucune action destructive ou opération Active Directory n'est disponible."
        eyebrow="Administration interne"
        title="Vue d'ensemble"
      />

      {activity ? (
        <>
          <div className="metrics-grid admin-metrics">
            <MetricCard
              detail="Ouvertes, en cours ou avec réponse client"
              label="Support à traiter"
              tone="amber"
              value={String(activity.supportToHandleCount)}
            />
            <MetricCard
              detail="Reçues, en étude ou avec réponse client"
              label="Services à traiter"
              value={String(activity.serviceToHandleCount)}
            />
            <MetricCard
              detail="Dernier message public envoyé par un client"
              label="Réponses client"
              tone="amber"
              value={String(activity.recentClientReplyCount)}
            />
            <MetricCard
              detail="Demandes support nécessitant un retour client"
              label="En attente client"
              tone="slate"
              value={String(activity.waitingForCustomerCount)}
            />
            <MetricCard
              detail="Workflows support et service non terminés"
              label="Demandes actives"
              tone="green"
              value={String(activity.activeRequestCount)}
            />
          </div>

          {activity.recentActivities.length > 0 ? (
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
          ) : (
            <EmptyState
              description="Aucun message public récent n'est disponible."
              title="Aucune activité publique"
            />
          )}
        </>
      ) : (
        <EmptyState
          description="Le centre d'activité est temporairement indisponible."
          title="Activité indisponible"
        />
      )}

      {overview ? (
        <>
          <div className="metrics-grid metrics-grid-three">
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
        correlationId={
          activityResult.error
            ? activityResult.correlationId
            : overviewResult.correlationId
        }
        source={
          activityResult.error
            ? activityResult.source
            : overviewResult.source
        }
      />
    </>
  );
}
