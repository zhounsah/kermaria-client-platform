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

      <div className="support-stacked">
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

        <section aria-label="Historique des demandes support">
          <div className="section-heading">
            <div>
              <h2>Demandes existantes</h2>
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
            <ul className="support-request-list">
              {requestsResult.data.map((request) => {
                const status = supportStatus[request.status];

                return (
                  <li className="support-request-row" key={request.id}>
                    <div className="support-request-row-main">
                      <div className="support-request-row-head">
                        <span className="card-kicker">{request.reference}</span>
                        <RequestStatusBadge
                          requestType="support"
                          status={request.status}
                        />
                      </div>
                      <h3>{request.subject}</h3>
                      <p className="support-request-row-meta">
                        {request.serviceName} · Priorité{" "}
                        {priorityLabels[request.priority]} · Mis à jour le{" "}
                        {formatDate(request.updatedAt)}
                      </p>
                      <p className="status-description">{status.description}</p>
                    </div>
                    <Link
                      className="button button-ghost button-compact"
                      href={`/support/${encodeURIComponent(request.id)}`}
                    >
                      Consulter le suivi
                    </Link>
                  </li>
                );
              })}
            </ul>
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
