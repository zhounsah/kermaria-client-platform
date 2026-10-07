import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { MetricCard } from "@/components/MetricCard";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { SitePageFrame } from "@/components/SitePageFrame";
import { requireAdminSession } from "@/lib/auth";
import {
  getAdminActivity,
  getAdminHomePageLayout,
  getAdminOverview,
} from "@/lib/internal-api";

export const metadata = {
  title: "Administration",
};

export const dynamic = "force-dynamic";

export default async function AdminOverviewPage() {
  await requireAdminSession();
  const [overviewResult, activityResult, layoutResult] = await Promise.all([
    getAdminOverview(),
    getAdminActivity(),
    getAdminHomePageLayout(),
  ]);
  const overview = overviewResult.data;
  const activity = activityResult.data;

  const slots = {
    admin_home_intro: <PageHeader
      action={<StatusBadge label="Vue de suivi" tone="info" />}
      description="Retrouvez les demandes à traiter et l'état du portail. Les actions détaillées sont accessibles depuis le menu."
      eyebrow="Administration interne"
      title="Vue d'ensemble"
    />,
    admin_home_activity: activity ? <div className="metrics-grid admin-metrics">
      <MetricCard detail="Ouvertes, en cours ou avec réponse client" label="Support à traiter"
        tone="amber" value={String(activity.supportToHandleCount)} />
      <MetricCard detail="Reçues, en étude ou avec réponse client" label="Services à traiter"
        value={String(activity.serviceToHandleCount)} />
      <MetricCard detail="Dernier message public envoyé par un client" label="Réponses client"
        tone="amber" value={String(activity.recentClientReplyCount)} />
      <MetricCard detail="Demandes support nécessitant un retour client" label="En attente client"
        tone="slate" value={String(activity.waitingForCustomerCount)} />
      <MetricCard detail="Demandes de support et de service en cours" label="Demandes actives"
        tone="green" value={String(activity.activeRequestCount)} />
    </div> : <EmptyState description="Le centre d'activité est temporairement indisponible."
      title="Activité indisponible" />,
    admin_home_overview: overview ? <div className="metrics-grid metrics-grid-three">
      <MetricCard detail="Clients référencés" label="Clients" value={String(overview.customerCount)} />
      <MetricCard detail="Comptes actifs" label="Utilisateurs" tone="green"
        value={String(overview.activeUserCount)} />
      <MetricCard detail="Non révoquées et non expirées" label="Sessions actives" tone="slate"
        value={String(overview.activeSessionCount)} />
    </div> : <EmptyState description="Les données d'administration sont temporairement indisponibles."
      title="Vue d'ensemble indisponible" />,
    admin_home_integrations: overview ? <section className="content-panel admin-safety-panel">
      <div><span className="card-kicker">État des intégrations</span>
        <h2>Active Directory : {overview.adMode}</h2>
        <p>Les opérations sont disponibles uniquement dans les parcours autorisés et vérifiées par l&apos;API.</p>
      </div>
      <StatusBadge label={`Mode ${overview.adMode}`} tone="info" />
    </section> : null,
    admin_home_shortcuts: <section className="content-panel quick-actions admin-overview-shortcuts">
      <div><span className="card-kicker">Aller plus loin</span><h2>Pages détaillées</h2>
        <p>Ouvrez la page correspondante pour examiner une demande ou suivre une opération.</p></div>
      <div className="admin-overview-shortcut-grid">
        <Link className="quick-action" href="/admin/activity"><span>Flux d&apos;activité</span>
          <small>Derniers messages des clients</small></Link>
        <Link className="quick-action" href="/admin/audit-logs"><span>Journal d&apos;audit</span>
          <small>Derniers événements techniques</small></Link>
        <Link className="quick-action" href="/admin/support-requests"><span>Demandes support</span>
          <small>Demandes clients à traiter</small></Link>
        <Link className="quick-action" href="/admin/payments"><span>Paiements</span>
          <small>Factures à régler ou réglées</small></Link>
      </div>
    </section>,
    admin_home_source: <MockNotice
      correlationId={activityResult.error ? activityResult.correlationId : overviewResult.correlationId}
      source={activityResult.error ? activityResult.source : overviewResult.source}
    />,
  };
  return <SitePageFrame area="admin" pageKey="/admin"
    initialLayout={layoutResult.data} slots={slots}>{null}</SitePageFrame>;
}
