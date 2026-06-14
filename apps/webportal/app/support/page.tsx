import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { FormSection } from "@/components/FormSection";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { SupportRequestForm } from "@/components/SupportRequestForm";
import { RequestStatusBadge } from "@/components/RequestStatusBadge";
import { formatDate, supportStatus } from "@/lib/formatters";
import { requireClientSession } from "@/lib/auth";
import {
  getServices,
  getSupportRequests,
  resolveDataSource,
} from "@/lib/internal-api";

export const metadata = {
  title: "Support",
};

export const dynamic = "force-dynamic";

const priorityLabels = {
  low: "Faible",
  normal: "Normale",
  high: "Haute",
};

export default async function SupportPage() {
  await requireClientSession();
  const [requestsResult, servicesResult] = await Promise.all([
    getSupportRequests(),
    getServices(),
  ]);
  const source = resolveDataSource([
    requestsResult.source,
    servicesResult.source,
  ]);

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Espace authentifié" tone="success" />}
        description="Décrivez votre besoin et sélectionnez le service concerné. La demande sera examinée avant toute intervention."
        eyebrow="Assistance"
        title="Demandes de support"
      />

      <div className="support-layout">
        <FormSection
          description="Ne saisissez aucun mot de passe, identifiant ou contenu confidentiel."
          title="Nouvelle demande"
        >
          {servicesResult.error ? (
            <ErrorState
              compact
              description="Le formulaire ne peut pas vérifier les services de votre compte pour le moment."
              reference={servicesResult.correlationId}
              title="Formulaire temporairement indisponible"
            />
          ) : (
            <SupportRequestForm services={servicesResult.data} />
          )}
        </FormSection>

        <section>
          <div className="section-heading">
            <div>
              <h2>Demandes récentes</h2>
              <p>Historique rattaché au client connecté.</p>
            </div>
          </div>
          {requestsResult.error ? (
            <ErrorState
              compact
              description="Impossible de charger l’historique des demandes support."
              reference={requestsResult.correlationId}
              title="Historique indisponible"
            />
          ) : requestsResult.data.length === 0 ? (
            <EmptyState
              description="Aucune demande support n’est disponible pour le moment."
              title="Aucune demande"
            />
          ) : (
            <div className="ticket-list">
              {requestsResult.data.map((request) => {
                const status = supportStatus[request.status];

                return (
                  <article className="ticket-card" key={request.id}>
                    <div className="ticket-card-header">
                      <span className="card-kicker">{request.reference}</span>
                      <RequestStatusBadge
                        requestType="support"
                        status={request.status}
                      />
                    </div>
                    <h3>{request.subject}</h3>
                    <p>{request.serviceName}</p>
                    <p className="status-description">{status.description}</p>
                    <div className="ticket-meta">
                      <span>Priorité {priorityLabels[request.priority]}</span>
                      <span>Mis à jour le {formatDate(request.updatedAt)}</span>
                    </div>
                    <Link
                      className="text-link"
                      href={`/support/${encodeURIComponent(request.id)}`}
                    >
                      Consulter le suivi
                    </Link>
                  </article>
                );
              })}
            </div>
          )}
        </section>
      </div>

      {source !== "unavailable" ? (
        <MockNotice
          correlationId={requestsResult.correlationId}
          source={source}
        />
      ) : null}
    </>
  );
}
