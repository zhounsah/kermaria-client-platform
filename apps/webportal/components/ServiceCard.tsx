import Link from "next/link";
import type { ServiceSummary } from "@kermaria/shared";

import { StatusBadge } from "@/components/StatusBadge";
import { formatDate, serviceStatus } from "@/lib/formatters";
import {
  getClientServiceDescription,
  getClientServiceName,
  getClientServiceNextStep,
  getClientServiceScope,
  getServiceSymbol,
} from "@/lib/service-display";

type ServiceCardProps = {
  service: ServiceSummary;
  vpsLinks?: Array<{ href: string; label: string }>;
};

const statusGuidance: Record<ServiceSummary["status"], string> = {
  active: "Service disponible selon ce qui est prévu dans votre offre.",
  pending:
    "Le service est en attente de paiement, de validation ou d'activation.",
  suspended:
    "Le service est temporairement indisponible. Contactez-nous si besoin.",
};

export function ServiceCard({ service, vpsLinks = [] }: ServiceCardProps) {
  const status = serviceStatus[service.status];
  const nextStep = getClientServiceNextStep(service);

  return (
    <article className="service-card">
      <div className="service-card-header">
        <div className="service-symbol" aria-hidden="true">
          {getServiceSymbol(service)}
        </div>
        <StatusBadge label={status.label} tone={status.tone} />
      </div>
      <div>
        <h2>{getClientServiceName(service)}</h2>
        <p className="card-description multiline-text">{getClientServiceDescription(service)}</p>
      </div>
      <dl className="compact-details">
        <div>
          <dt>Début</dt>
          <dd>{service.startedAt ? formatDate(service.startedAt) : "À venir"}</dd>
        </div>
      </dl>
      <div className="service-scope">
        <strong>Ce qui est inclus</strong>
        <span>{getClientServiceScope(service)}</span>
      </div>
      <p className={`service-status-note service-status-${service.status}`}>
        {statusGuidance[service.status]}
      </p>
      <details className="service-reference">
        <summary>Référence de suivi</summary>
        <span>{service.reference}</span>
      </details>
      {nextStep ? (
        <p className="service-next-step">{nextStep}</p>
      ) : null}
      {vpsLinks.length > 0 ? (
        <div className="service-card-actions">
          {vpsLinks.map((link) => (
            <Link className="button button-secondary" href={link.href} key={link.href}>
              {link.label}
            </Link>
          ))}
        </div>
      ) : null}
    </article>
  );
}
