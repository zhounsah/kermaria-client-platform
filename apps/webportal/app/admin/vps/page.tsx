import { AdminApproveVpsTechnicalReviewButton } from "@/components/AdminApproveVpsTechnicalReviewButton";
import { AdminVpsManualProvisioningControls } from "@/components/AdminVpsManualProvisioningControls";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminBillingV2VpsTechnicalReviews } from "@/lib/internal-api";

export const metadata = { title: "VPS - Administration" };
export const dynamic = "force-dynamic";

function technicalStatus(technicalStatus: string) {
  return technicalStatus === "approved"
    ? { label: "Prêt à provisionner", tone: "success" as const }
    : { label: "Validation technique en attente", tone: "warning" as const };
}

function provisioningStatus(provisioning: string) {
  switch (provisioning) {
    case "active":
      return { label: "VPS actif", tone: "success" as const };
    case "provisioning":
      return { label: "Mise en service en cours", tone: "warning" as const };
    case "failed":
      return { label: "Mise en service en échec", tone: "danger" as const };
    default:
      return { label: "Mise en service à préparer", tone: "neutral" as const };
  }
}

export default async function AdminVpsTechnicalReviewsPage() {
  await requireAdminSession();
  const result = await getAdminBillingV2VpsTechnicalReviews();
  const pendingCount = result.data.filter((item) =>
    item.technicalStatus === "pending_review").length;

  return (
    <>
      <PageHeader
        action={<StatusBadge label={`${pendingCount} en attente`} tone="warning" />}
        description="Commandes VPS dont le paiement Billing V2 est réellement reçu, avec revue technique puis mise en service manuelle tracée."
        eyebrow="Administration"
        title="VPS payés"
      />

      {result.error ? (
        <ErrorState
          description="Impossible de charger les commandes VPS pour le moment."
          reference={result.correlationId}
          title="Revue VPS indisponible"
        />
      ) : result.data.length === 0 ? (
        <EmptyState
          description="Aucune commande VPS réglée n’attend actuellement de validation technique."
          title="Aucun VPS à examiner"
        />
      ) : (
        <div className="stack-panels">
          {result.data.map((item) => {
            const status = technicalStatus(item.technicalStatus);
            const operational = provisioningStatus(item.provisioningStatus);
            const isActive = item.provisioningStatus === "active";
            return (
              <SectionCard
                ariaLabel={`VPS ${item.serviceCode} ${item.tierCode}`}
                className="stack-panel"
                key={item.technicalRequestId}
              >
                <div className="section-heading">
                  <div>
                    <span className="card-kicker">
                      {item.customerReference} · {item.customerName}
                    </span>
                    <h2>{item.serviceCode} — {item.tierCode}</h2>
                    <p>{item.hostname} · {item.operatingSystem}</p>
                  </div>
                  <div className="badge-stack">
                    <StatusBadge label="Paiement reçu" tone="success" />
                    {!isActive ? (
                      <StatusBadge label={status.label} tone={status.tone} />
                    ) : null}
                    <StatusBadge label={operational.label} tone={operational.tone} />
                  </div>
                </div>
                <dl className="profile-details">
                  <div><dt>Usage</dt><dd>{item.usage}</dd></div>
                  <div><dt>Gestion</dt><dd>{item.managementMode}</dd></div>
                  <div><dt>Exposition Internet</dt><dd>{item.internetExposure}</dd></div>
                  <div><dt>Commentaire</dt><dd>{item.comment || "—"}</dd></div>
                  <div><dt>Payé le</dt><dd>{item.settledAt ? formatDateTime(item.settledAt) : "—"}</dd></div>
                  <div><dt>En revue depuis</dt><dd>{item.technicalReviewPendingAt ? formatDateTime(item.technicalReviewPendingAt) : "—"}</dd></div>
                  <div><dt>Infrastructure cible</dt><dd>{item.infrastructureTarget || "—"}</dd></div>
                  <div><dt>Référence d’instance</dt><dd>{item.instanceReference || "—"}</dd></div>
                  <div><dt>IP publique</dt><dd>{item.publicIpAddress || "—"}</dd></div>
                  <div><dt>Notes opérationnelles</dt><dd>{item.operationalNotes || "—"}</dd></div>
                  <div><dt>Mise en service commencée le</dt><dd>{item.provisioningStartedAt ? formatDateTime(item.provisioningStartedAt) : "—"}</dd></div>
                  <div><dt>Activé le</dt><dd>{item.activatedAt ? formatDateTime(item.activatedAt) : "—"}</dd></div>
                </dl>
                {item.technicalStatus === "pending_review" ? (
                  <AdminApproveVpsTechnicalReviewButton
                    technicalRequestId={item.technicalRequestId}
                  />
                ) : item.technicalStatus === "approved" ? (
                  <AdminVpsManualProvisioningControls
                    provisioningStatus={item.provisioningStatus}
                    technicalRequestId={item.technicalRequestId}
                  />
                ) : null}
              </SectionCard>
            );
          })}
        </div>
      )}
      <MockNotice correlationId={result.correlationId} source={result.source} />
    </>
  );
}
