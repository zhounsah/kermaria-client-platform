import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { MetricCard } from "@/components/MetricCard";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionHeading } from "@/components/SectionHeading";
import { StatusBadge } from "@/components/StatusBadge";
import {
  formatCurrency,
  formatDate,
  invoiceStatus,
  serviceStatus,
  supportStatus,
} from "@/lib/formatters";
import {
  getClientProfile,
  getInvoices,
  getPortalSummary,
  getServices,
  getSupportRequests,
  resolveDataSource,
} from "@/lib/internal-api";
import { requirePortalSession } from "@/lib/auth";

export const metadata = {
  title: "Tableau de bord",
};

export const dynamic = "force-dynamic";

export default async function DashboardPage() {
  await requirePortalSession();
  const [summaryResult, profileResult, servicesResult, invoicesResult, supportResult] =
    await Promise.all([
      getPortalSummary(),
      getClientProfile(),
      getServices(),
      getInvoices(),
      getSupportRequests(),
    ]);

  const summary = summaryResult.data;
  const profile = profileResult.data;
  const services = servicesResult.data;
  const invoices = invoicesResult.data;
  const supportRequests = supportResult.data;
  const source = resolveDataSource([
    summaryResult.source,
    profileResult.source,
    servicesResult.source,
    invoicesResult.source,
    supportResult.source,
  ]);

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Démonstration" tone="info" />}
        description={
          summary
            ? `Référence ${summary.customerReference} - synthèse fictive de vos services et demandes.`
            : "La synthèse mock est temporairement indisponible."
        }
        eyebrow="Vue d'ensemble"
        title={`Bonjour ${profile?.contactName.split(" ")[0] ?? "Client"}`}
      />

      <section className="metrics-grid" aria-label="Indicateurs du compte">
        <MetricCard
          detail="Selon le périmètre fictif"
          label="Services actifs"
          tone="green"
          value={String(summary?.activeServiceCount ?? 0)}
        />
        <MetricCard
          detail={
            summary?.pendingInvoiceCount
              ? formatCurrency(summary.pendingInvoiceTotal)
              : "Aucune facture en attente"
          }
          label="Factures fictives à régler"
          tone="amber"
          value={String(summary?.pendingInvoiceCount ?? 0)}
        />
        <MetricCard
          detail="Aucune demande réelle transmise"
          label="Demandes ouvertes"
          tone="blue"
          value={String(summary?.openSupportRequestCount ?? 0)}
        />
        <MetricCard
          detail={
            summary
              ? `Mis à jour le ${formatDate(summary.lastUpdatedAt)}`
              : "Source mock indisponible"
          }
          label="État du compte"
          tone="slate"
          value={summary ? "À jour" : "Indisponible"}
        />
      </section>

      <div className="dashboard-layout">
        <section className="content-panel">
          <SectionHeading
            action={<Link href="/services">Voir tous les services</Link>}
            description="Services fictifs actuellement associés au compte."
            title="Vos services"
          />
          {services.length === 0 ? (
            <EmptyState
              description="Aucun service mock n'est disponible actuellement."
              title="Aucun service"
            />
          ) : (
            <div className="stack-list">
              {services.slice(0, 4).map((service) => {
                const status = serviceStatus[service.status];

                return (
                  <article className="stack-row" key={service.id}>
                    <div className="service-symbol" aria-hidden="true">
                      {service.reference.split("-")[1]}
                    </div>
                    <div className="stack-row-main">
                      <strong>{service.name}</strong>
                      <span>
                        {service.commercialTerms} - {service.reference}
                      </span>
                    </div>
                    <StatusBadge label={status.label} tone={status.tone} />
                  </article>
                );
              })}
            </div>
          )}
        </section>

        <aside className="content-panel quick-actions">
          <SectionHeading
            description="Accédez directement aux parcours de démonstration."
            title="Actions rapides"
          />
          <Link className="quick-action" href="/support">
            <span>Créer une demande mock</span>
            <small>Aucun ticket ou e-mail réel</small>
          </Link>
          <Link className="quick-action" href="/request-service">
            <span>Demander un service</span>
            <small>Préparer une demande sans engagement</small>
          </Link>
          <Link className="quick-action" href="/profile">
            <span>Consulter mon profil</span>
            <small>Vérifier les informations fictives</small>
          </Link>
        </aside>
      </div>

      <div className="dashboard-layout">
        <section className="content-panel">
          <SectionHeading
            action={<Link href="/invoices">Toutes les factures</Link>}
            title="Factures fictives récentes"
          />
          {invoices.length === 0 ? (
            <EmptyState
              description="Aucun document fictif n'est disponible."
              title="Aucune facture"
            />
          ) : (
            <div className="stack-list">
              {invoices.slice(0, 3).map((invoice) => {
                const status = invoiceStatus[invoice.status];

                return (
                  <article className="stack-row" key={invoice.id}>
                    <div className="stack-row-main">
                      <strong>{invoice.number}</strong>
                      <span>
                        {invoice.period} - émise le {formatDate(invoice.issuedAt)}
                      </span>
                    </div>
                    <strong>{formatCurrency(invoice.totalAmount)}</strong>
                    <StatusBadge label={status.label} tone={status.tone} />
                  </article>
                );
              })}
            </div>
          )}
        </section>

        <section className="content-panel">
          <SectionHeading
            action={<Link href="/support">Ouvrir le support</Link>}
            title="Support mock récent"
          />
          {supportRequests.length === 0 ? (
            <EmptyState
              description="Aucune demande support fictive n'est disponible."
              title="Aucune demande"
            />
          ) : (
            <div className="stack-list">
              {supportRequests.slice(0, 2).map((request) => {
                const status = supportStatus[request.status];

                return (
                  <article className="stack-row" key={request.id}>
                    <div className="stack-row-main">
                      <strong>{request.subject}</strong>
                      <span>
                        {request.reference} - {request.serviceName}
                      </span>
                    </div>
                    <StatusBadge label={status.label} tone={status.tone} />
                  </article>
                );
              })}
            </div>
          )}
        </section>
      </div>

      <MockNotice
        correlationId={summaryResult.correlationId}
        source={source}
      />
    </>
  );
}
