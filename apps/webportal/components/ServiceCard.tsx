import type { ServiceSummary } from "@kermaria/shared";

import { StatusBadge } from "@/components/StatusBadge";
import { formatDate, serviceStatus } from "@/lib/formatters";
import { getServiceSymbol } from "@/lib/service-display";

type ServiceCardProps = {
  service: ServiceSummary;
};

const statusGuidance: Record<ServiceSummary["status"], string> = {
  active: "Service disponible selon le périmètre actuellement couvert.",
  pending:
    "Le service est en attente de paiement, de validation ou d'activation.",
  suspended:
    "Le service est temporairement indisponible. Contactez le support si besoin.",
};

export function ServiceCard({ service }: ServiceCardProps) {
  const status = serviceStatus[service.status];

  return (
    <article className="service-card">
      <div className="service-card-header">
        <div className="service-symbol" aria-hidden="true">
          {getServiceSymbol(service)}
        </div>
        <StatusBadge label={status.label} tone={status.tone} />
      </div>
      <div>
        <p className="card-kicker">{service.reference}</p>
        <h2>{service.name}</h2>
        <p className="card-description multiline-text">{service.description}</p>
      </div>
      <dl className="compact-details">
        <div>
          <dt>Début</dt>
          <dd>{service.startedAt ? formatDate(service.startedAt) : "À venir"}</dd>
        </div>
        <div>
          <dt>Couverture</dt>
          <dd>{service.commercialTerms}</dd>
        </div>
      </dl>
      <div className="service-scope">
        <strong>Périmètre</strong>
        <span>{service.scope}</span>
      </div>
      <p className={`service-status-note service-status-${service.status}`}>
        {statusGuidance[service.status]}
      </p>
      {service.nextStep ? (
        <p className="service-next-step">{service.nextStep}</p>
      ) : null}
    </article>
  );
}
