import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MetricCard } from "@/components/MetricCard";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { RequestStatusBadge } from "@/components/RequestStatusBadge";
import { SectionCard } from "@/components/SectionCard";
import { SectionHeading } from "@/components/SectionHeading";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
import {
  commercialDocumentStatus,
  formatCurrency,
  formatCurrencyFromCents,
  formatDate,
  serviceRequestStatus,
  serviceStatus,
} from "@/lib/formatters";
import {
  getClientProfile,
  getCommercialDocuments,
  getNotifications,
  getPortalSummary,
  getServices,
  getServiceRequests,
  getSupportRequests,
  resolveDataSource,
} from "@/lib/internal-api";

export const metadata = {
  title: "Tableau de bord",
};

export const dynamic = "force-dynamic";

export default async function DashboardPage() {
  await requireClientSession();
  const [
    summaryResult,
    profileResult,
    servicesResult,
    documentsResult,
    supportResult,
    serviceRequestsResult,
    notificationsResult,
  ] = await Promise.all([
    getPortalSummary(),
    getClientProfile(),
    getServices(),
    getCommercialDocuments(),
    getSupportRequests(),
    getServiceRequests(),
    getNotifications(),
  ]);

  const summary = summaryResult.data;
  const profile = profileResult.data;
  const services = servicesResult.data;
  const documents = documentsResult.data;
  const supportRequests = supportResult.data;
  const serviceRequests = serviceRequestsResult.data;
  const notifications = notificationsResult.data;
  const unreadNotificationCount = notifications.filter(
    (item) => !item.isRead,
  ).length;
  const source = resolveDataSource([
    summaryResult.source,
    profileResult.source,
    servicesResult.source,
    documentsResult.source,
    supportResult.source,
    serviceRequestsResult.source,
    notificationsResult.source,
  ]);
  const partialError = [
    summaryResult,
    profileResult,
    servicesResult,
    documentsResult,
    supportResult,
    serviceRequestsResult,
    notificationsResult,
  ].find((result) => result.error);

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Espace authentifié" tone="success" />}
        description={
          summary
            ? `Référence ${summary.customerReference} - informations actuellement disponibles pour vos services, demandes et documents commerciaux.`
            : "Votre espace client regroupe les informations actuellement disponibles pour vos services Kermaria."
        }
        eyebrow="Vue d'ensemble"
        title={`Bonjour ${profile?.contactName?.split(" ")[0] ?? "Client"}`}
      />

      {partialError ? (
        <ErrorState
          compact
          description="Une partie du tableau de bord n'a pas pu être chargée. Les autres informations disponibles restent affichées."
          reference={partialError.correlationId}
          title="Chargement partiel"
        />
      ) : null}

      <section className="metrics-grid" aria-label="Indicateurs du compte">
        <MetricCard
          detail="Selon le périmètre convenu"
          label="Services actifs"
          tone="green"
          value={String(summary?.activeServiceCount ?? 0)}
        />
        <MetricCard
          detail={
            summary?.pendingInvoiceCount
              ? `${formatCurrency(summary.pendingInvoiceTotal)} à vérifier`
              : "Aucun document en attente"
          }
          label="Documents en attente"
          tone="amber"
          value={String(summary?.pendingInvoiceCount ?? 0)}
        />
        <MetricCard
          detail={`${summary?.activeServiceRequestCount ?? 0} demande(s) de service en suivi`}
          label="Support ouvert"
          tone="blue"
          value={String(summary?.openSupportRequestCount ?? 0)}
        />
        <MetricCard
          detail={
            summary
              ? `Mis à jour le ${formatDate(summary.lastUpdatedAt)}`
              : "Mise à jour indisponible"
          }
          label="État du compte"
          tone="slate"
          value={summary ? "À jour" : "Indisponible"}
        />
      </section>

      <div className="dashboard-layout">
        <SectionCard ariaLabel="Aperçu des services">
          <SectionHeading
            action={<Link href="/services">Voir tous les services</Link>}
            description="Services fictifs actuellement associés au compte."
            title="Vos services"
          />
          {servicesResult.error ? (
            <ErrorState
              compact
              description="Impossible de charger vos services pour le moment."
              reference={servicesResult.correlationId}
              title="Services indisponibles"
            />
          ) : services.length === 0 ? (
            <EmptyState
              description="Aucun service n'est actuellement associé à ce compte."
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
        </SectionCard>

        <aside className="content-panel quick-actions" aria-label="Actions rapides">
          <SectionHeading
            description="Accédez directement aux principales démarches."
            title="Actions rapides"
          />
          <Link className="quick-action" href="/support">
            <span>Créer une demande support</span>
            <small>Décrire un besoin lié à un service</small>
          </Link>
          <Link className="quick-action" href="/request-service">
            <span>Demander un service</span>
            <small>Demande étudiée avant toute activation</small>
          </Link>
          <Link className="quick-action" href="/invoices">
            <span>Consulter les documents</span>
            <small>Aucun paiement ni facture officielle</small>
          </Link>
          <Link className="quick-action" href="/notifications">
            <span>Consulter les notifications</span>
            <small>
              {unreadNotificationCount > 0
                ? `${unreadNotificationCount} nouvelle(s) activité(s)`
                : "Aucune nouvelle activité"}
            </small>
          </Link>
        </aside>
      </div>

      <div className="dashboard-layout">
        <SectionCard ariaLabel="Aperçu des documents commerciaux">
          <SectionHeading
            action={<Link href="/invoices">Tous les documents</Link>}
            title="Documents commerciaux récents"
          />
          {documentsResult.error ? (
            <ErrorState
              compact
              description="Impossible de charger les documents commerciaux informatifs."
              reference={documentsResult.correlationId}
              title="Documents indisponibles"
            />
          ) : documents.length === 0 ? (
            <EmptyState
              description="Aucun document informatif n'est disponible pour le moment."
              title="Aucun document"
            />
          ) : (
            <div className="stack-list">
              {documents.slice(0, 3).map((document) => {
                const status = commercialDocumentStatus[document.status];

                return (
                  <article className="stack-row" key={document.id}>
                    <div className="stack-row-main">
                      <strong>{document.internalReference}</strong>
                      <span>
                        {document.title} - créé le {formatDate(document.createdAt)}
                      </span>
                    </div>
                    <strong>{formatCurrencyFromCents(document.totalAmountCents)}</strong>
                    <StatusBadge label={status.label} tone={status.tone} />
                  </article>
                );
              })}
            </div>
          )}
        </SectionCard>

        <SectionCard ariaLabel="Demandes récentes">
          <SectionHeading
            action={<Link href="/support">Voir le support</Link>}
            title="Demandes récentes"
          />
          {supportResult.error || serviceRequestsResult.error ? (
            <ErrorState
              compact
              description="Impossible de charger toutes les demandes récentes."
              reference={
                supportResult.error
                  ? supportResult.correlationId
                  : serviceRequestsResult.correlationId
              }
              title="Suivi indisponible"
            />
          ) : supportRequests.length === 0 && serviceRequests.length === 0 ? (
            <EmptyState
              description="Aucune demande n'est disponible pour le moment."
              title="Aucune demande"
            />
          ) : (
            <div className="stack-list">
              {supportRequests.slice(0, 1).map((request) => (
                <Link
                  className="stack-row"
                  href={`/support/${encodeURIComponent(request.id)}`}
                  key={request.id}
                >
                  <div className="stack-row-main">
                    <strong>{request.subject}</strong>
                    <span>{request.reference} · Support</span>
                  </div>
                  <RequestStatusBadge
                    requestType="support"
                    status={request.status}
                  />
                </Link>
              ))}
              {serviceRequests.slice(0, 1).map((request) => {
                const status = serviceRequestStatus[request.status];
                return (
                  <Link
                    className="stack-row"
                    href={`/request-service/${encodeURIComponent(request.id)}`}
                    key={request.id}
                  >
                    <div className="stack-row-main">
                      <strong>{request.subject}</strong>
                      <span>{request.reference} · {status.description}</span>
                    </div>
                    <RequestStatusBadge
                      requestType="service"
                      status={request.status}
                    />
                  </Link>
                );
              })}
            </div>
          )}
        </SectionCard>
      </div>

      <SectionCard ariaLabel="Activité récente du compte">
        <SectionHeading
          action={<Link href="/notifications">Toutes les notifications</Link>}
          description="Changements de statut et messages récemment publiés sur vos demandes."
          title="Activité récente"
        />
        {notificationsResult.error ? (
          <ErrorState
            compact
            description="Impossible de charger l'activité récente pour le moment."
            reference={notificationsResult.correlationId}
            title="Activité indisponible"
          />
        ) : notifications.length === 0 ? (
          <EmptyState
            description="Aucune activité récente n'est disponible."
            title="Aucune notification"
          />
        ) : (
          <div className="stack-list">
            {notifications.slice(0, 3).map((notification) => (
              <Link
                className={
                  notification.isRead
                    ? "stack-row"
                    : "stack-row notification-summary-unread"
                }
                href={safeNotificationLink(notification.linkUrl)}
                key={notification.id}
              >
                <div className="stack-row-main">
                  <strong>{notification.title}</strong>
                  <span>{notification.message}</span>
                </div>
                <StatusBadge
                  label={notification.isRead ? "Lue" : "Nouvelle"}
                  tone={notification.isRead ? "neutral" : "warning"}
                />
              </Link>
            ))}
          </div>
        )}
      </SectionCard>

      {source !== "unavailable" ? (
        <MockNotice
          correlationId={summaryResult.correlationId}
          source={source}
        />
      ) : null}
    </>
  );
}

function safeNotificationLink(value: string | null) {
  if (
    value?.startsWith("/support/")
    || value?.startsWith("/request-service/")
  ) {
    return value;
  }

  return "/notifications";
}
