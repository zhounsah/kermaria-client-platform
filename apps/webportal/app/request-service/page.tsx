import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { FormSection } from "@/components/FormSection";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { RequestStatusBadge } from "@/components/RequestStatusBadge";
import { ServiceRequestForm } from "@/components/ServiceRequestForm";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
import {
  formatDate,
  serviceRequestStatus,
} from "@/lib/formatters";
import {
  getServiceCatalog,
  getServiceRequests,
  resolveDataSource,
} from "@/lib/internal-api";

export const metadata = { title: "Demander un service" };
export const dynamic = "force-dynamic";

export default async function RequestServicePage() {
  await requireClientSession();
  const [catalogResult, requestsResult] = await Promise.all([
    getServiceCatalog(),
    getServiceRequests(),
  ]);
  const source = resolveDataSource([
    catalogResult.source,
    requestsResult.source,
  ]);

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Traitement manuel" tone="info" />}
        description="Présentez votre besoin puis suivez son étude. Aucune activation automatique n’est déclenchée."
        eyebrow="Nouveau besoin"
        title="Demandes de service"
      />

      {catalogResult.error ? (
        <ErrorState
          action={<Link className="button" href="/request-service">Réessayer</Link>}
          description="Le catalogue et le formulaire ne peuvent pas être chargés pour le moment."
          reference={catalogResult.correlationId}
          title="Service temporairement indisponible"
        />
      ) : catalogResult.data.length === 0 ? (
        <EmptyState
          description="Aucune prestation n’est actuellement proposée dans cet espace."
          title="Catalogue vide"
        />
      ) : (
        <>
          <section className="catalog-grid" aria-label="Catalogue de services">
            {catalogResult.data.map((service) => (
              <article className="catalog-card" key={service.id}>
                <span className="card-kicker">{service.category}</span>
                <h2>{service.name}</h2>
                <p>{service.description}</p>
                <div className="catalog-scope">
                  <span>{service.scope}</span>
                  <strong>{service.commercialTerms}</strong>
                </div>
              </article>
            ))}
          </section>
          <div className="request-layout">
            <FormSection
              description="Présentez le contexte sans identifiant, mot de passe ni donnée confidentielle."
              title="Parlez-nous de votre besoin"
            >
              <ServiceRequestForm services={catalogResult.data} />
            </FormSection>
            <aside className="process-card">
              <p className="eyebrow">Parcours prévu</p>
              <h2>Une demande étudiée avant toute action</h2>
              <ol className="process-list">
                <li><span>1</span><div><strong>Qualification</strong><p>Le besoin et le contexte sont vérifiés.</p></div></li>
                <li><span>2</span><div><strong>Proposition</strong><p>Une solution peut être préparée séparément.</p></div></li>
                <li><span>3</span><div><strong>Validation</strong><p>Aucune action n’intervient sans accord explicite.</p></div></li>
              </ol>
            </aside>
          </div>
        </>
      )}

      <section className="request-history-section">
        <div className="section-heading">
          <div>
            <h2>Demandes récentes</h2>
            <p>Suivi des demandes de service rattachées à votre compte.</p>
          </div>
        </div>
        {requestsResult.error ? (
          <ErrorState
            compact
            description="Impossible de charger vos demandes de service."
            reference={requestsResult.correlationId}
            title="Suivi indisponible"
          />
        ) : requestsResult.data.length === 0 ? (
          <EmptyState
            description="Aucune demande de service n’est disponible."
            title="Aucune demande"
          />
        ) : (
          <div className="ticket-list">
            {requestsResult.data.map((request) => {
              const status = serviceRequestStatus[request.status];
              return (
                <article className="ticket-card" key={request.id}>
                  <div className="ticket-card-header">
                    <span className="card-kicker">{request.reference}</span>
                    <RequestStatusBadge
                      requestType="service"
                      status={request.status}
                    />
                  </div>
                  <h3>{request.subject}</h3>
                  <p>{request.catalogItemName}</p>
                  <p className="status-description">{status.description}</p>
                  <div className="ticket-meta">
                    <span>Mis à jour le {formatDate(request.updatedAt)}</span>
                  </div>
                  <Link
                    className="text-link"
                    href={`/request-service/${encodeURIComponent(request.id)}`}
                  >
                    Consulter le suivi
                  </Link>
                </article>
              );
            })}
          </div>
        )}
      </section>

      {source !== "unavailable" ? (
        <MockNotice
          correlationId={catalogResult.correlationId}
          source={source}
        />
      ) : null}
    </>
  );
}
