import { EmptyState } from "@/components/EmptyState";
import { FormSection } from "@/components/FormSection";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { SupportRequestForm } from "@/components/SupportRequestForm";
import { formatDate, supportStatus } from "@/lib/formatters";
import { requirePortalSession } from "@/lib/auth";
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
  await requirePortalSession();
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
        action={<StatusBadge label="V0.7 authentifiée" tone="info" />}
        description="Le formulaire passe par le BFF puis API-INTERNAL. Il écrit dans MariaDB uniquement lorsque la configuration serveur est complète."
        eyebrow="Assistance"
        title="Demandes de support"
      />

      <div className="support-layout">
        <FormSection
          description="Aucun e-mail ni service externe n'est appelé. Le résultat indique clairement si la demande a été persistée."
          title="Nouvelle demande"
        >
          <SupportRequestForm services={servicesResult.data} />
        </FormSection>

        <section>
          <div className="section-heading">
            <div>
              <h2>Demandes récentes</h2>
              <p>Historique rattaché au client connecté.</p>
            </div>
          </div>
          {requestsResult.data.length === 0 ? (
            <EmptyState
              description="Aucune demande support mock n'est disponible."
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
                      <StatusBadge label={status.label} tone={status.tone} />
                    </div>
                    <h3>{request.subject}</h3>
                    <p>{request.serviceName}</p>
                    <div className="ticket-meta">
                      <span>Priorité {priorityLabels[request.priority]}</span>
                      <span>Mis à jour le {formatDate(request.updatedAt)}</span>
                    </div>
                  </article>
                );
              })}
            </div>
          )}
        </section>
      </div>

      <MockNotice
        correlationId={requestsResult.correlationId}
        source={source}
      />
    </>
  );
}
