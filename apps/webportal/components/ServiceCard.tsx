import type { ServiceSummary } from "@kermaria/shared";

import { StatusBadge } from "@/components/StatusBadge";
import { formatDate, serviceStatus } from "@/lib/formatters";

type ServiceCardProps = {
  service: ServiceSummary;
};

const serviceSymbols: Record<ServiceSummary["type"], string> = {
  personal_hosting: "HDP",
  backup: "SAV",
  vpn: "VPN",
  rds: "RDS",
  support: "SUP",
};

export function ServiceCard({ service }: ServiceCardProps) {
  const status = serviceStatus[service.status];

  return (
    <article className="service-card">
      <div className="service-card-header">
        <div className="service-symbol" aria-hidden="true">
          {serviceSymbols[service.type]}
        </div>
        <StatusBadge label={status.label} tone={status.tone} />
      </div>
      <div>
        <p className="card-kicker">{service.reference}</p>
        <h2>{service.name}</h2>
        <p className="card-description">{service.description}</p>
      </div>
      <dl className="compact-details">
        <div>
          <dt>Début</dt>
          <dd>{service.startedAt ? formatDate(service.startedAt) : "À venir"}</dd>
        </div>
        <div>
          <dt>Conditions</dt>
          <dd>{service.commercialTerms}</dd>
        </div>
      </dl>
      <div className="service-scope">
        <strong>Périmètre</strong>
        <span>{service.scope}</span>
      </div>
      {service.nextStep ? (
        <p className="service-next-step">{service.nextStep}</p>
      ) : null}
    </article>
  );
}
