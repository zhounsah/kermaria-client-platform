import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";

import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getClientVpsDetail } from "@/lib/internal-api";

export const dynamic = "force-dynamic";

type Props = {
  params: Promise<{ id: string }>;
};

const provisioningPresentation = {
  preparing: {
    label: "Mise en service en préparation",
    tone: "info" as const,
  },
  in_progress: {
    label: "Mise en service en cours",
    tone: "warning" as const,
  },
  active: {
    label: "Votre VPS est en service",
    tone: "success" as const,
  },
  attention_required: {
    label: "Une intervention est en cours",
    tone: "warning" as const,
  },
};

const exposureLabels = {
  yes: "Accessible depuis Internet",
  no: "Pas d’exposition Internet prévue",
  to_confirm: "À confirmer avec notre équipe",
};

export async function generateMetadata(): Promise<Metadata> {
  return { title: "Mon VPS | Zachary IT" };
}

export default async function ClientVpsPage({ params }: Props) {
  await requireClientSession();
  const { id } = await params;
  const result = await getClientVpsDetail(id);

  if (result.error) {
    return (
      <ErrorState
        action={<Link className="button" href="/services">Retour à mes services</Link>}
        description="Les informations de ce VPS ne sont pas disponibles pour le moment."
        reference={result.correlationId}
        title="VPS indisponible"
      />
    );
  }

  const vps = result.data;
  if (!vps) {
    notFound();
  }

  const status = provisioningPresentation[vps.provisioningStatus as keyof typeof provisioningPresentation]
    ?? provisioningPresentation.preparing;
  const specifications = [
    vps.specifications.vcpuCount === null ? null : ["vCPU", String(vps.specifications.vcpuCount)],
    vps.specifications.ramGib === null ? null : ["RAM", `${vps.specifications.ramGib} Go`],
    vps.specifications.diskGib === null ? null : ["Stockage", `${vps.specifications.diskGib} Go`],
  ].filter((item): item is [string, string] => item !== null);

  return (
    <div className="client-vps-detail-page">
      <PageHeader
        action={
          <div className="button-row client-vps-detail-actions">
            <Link className="button button-secondary" href="/services">
              Mes services
            </Link>
            <Link className="button" href="/support">
              Contacter le support
            </Link>
          </div>
        }
        description={`${vps.serviceName} — ${vps.tierLabel}`}
        eyebrow="Mon VPS"
        title={vps.hostname}
      />

      <section className="detail-card client-vps-detail-card client-vps-status-card" aria-labelledby="vps-status-title">
        <div className="detail-card-heading">
          <div>
            <p className="card-kicker">Statut</p>
            <h2 id="vps-status-title">État de votre VPS</h2>
          </div>
          <StatusBadge label={status.label} tone={status.tone} />
        </div>
        <p>
          Retrouvez ici les informations utiles à l’utilisation de votre VPS.
          Pour toute intervention ou question, contactez le support.
        </p>
      </section>

      <section className="detail-card client-vps-detail-card" aria-labelledby="vps-configuration-title">
        <div className="detail-card-heading">
          <div>
            <p className="card-kicker">Configuration</p>
            <h2 id="vps-configuration-title">Votre offre et votre configuration</h2>
          </div>
        </div>
        <dl className="detail-grid client-vps-detail-grid client-vps-configuration-grid">
          <div><dt>Offre</dt><dd>{vps.serviceName}</dd></div>
          <div><dt>Palier</dt><dd>{vps.tierLabel}</dd></div>
          <div><dt>Hostname</dt><dd>{vps.hostname}</dd></div>
          <div><dt>Système d’exploitation</dt><dd>{vps.operatingSystem}</dd></div>
          {specifications.map(([label, value]) => (
            <div key={label}><dt>{label}</dt><dd>{value}</dd></div>
          ))}
          <div><dt>Mode de gestion</dt><dd>{vps.managementMode}</dd></div>
          <div><dt>Exposition Internet</dt><dd>{exposureLabels[vps.internetExposure]}</dd></div>
          <div><dt>Usage prévu</dt><dd>{vps.usage}</dd></div>
        </dl>
      </section>

      <section className="detail-card client-vps-detail-card" aria-labelledby="vps-access-title">
        <div className="detail-card-heading">
          <div>
            <p className="card-kicker">Références et accès</p>
            <h2 id="vps-access-title">Informations de mise en service</h2>
          </div>
        </div>
        <dl className="detail-grid client-vps-detail-grid client-vps-access-grid">
          <div><dt>Référence VPS</dt><dd>{vps.id}</dd></div>
          <div><dt>Adresse IP publique</dt><dd>{vps.publicIpAddress ?? "En cours d’attribution"}</dd></div>
          <div><dt>Mise en service commencée le</dt><dd>{vps.provisioningStartedAt ? formatDateTime(vps.provisioningStartedAt) : "—"}</dd></div>
          <div><dt>Activé le</dt><dd>{vps.activatedAt ? formatDateTime(vps.activatedAt) : "—"}</dd></div>
        </dl>
      </section>
    </div>
  );
}
