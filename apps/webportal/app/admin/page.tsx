import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { MetricCard } from "@/components/MetricCard";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
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

          <section className="content-panel quick-actions admin-overview-shortcuts">
            <div>
              <span className="card-kicker">Aller plus loin</span>
              <h2>Pages détaillées</h2>
              <p>
                Les flux complets sont disponibles dans les pages dédiées
                accessibles depuis la barre latérale.
              </p>
            </div>
            <div className="admin-overview-shortcut-grid">
              <Link className="quick-action" href="/admin/activity">
                <span>Flux d&apos;activité</span>
                <small>Derniers messages publics support / service</small>
              </Link>
              <Link className="quick-action" href="/admin/audit-logs">
                <span>Journal d&apos;audit</span>
                <small>100 derniers événements techniques</small>
              </Link>
              <Link className="quick-action" href="/admin/support-requests">
                <span>Demandes support</span>
                <small>Tickets clients à traiter</small>
              </Link>
              <Link className="quick-action" href="/admin/payments">
                <span>Paiements</span>
                <small>Factures à régler ou réglées</small>
              </Link>
            </div>
          </section>
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
