import { AdminKoxoValidationButton } from "@/components/AdminKoxoValidationButton";
import { EmptyState } from "@/components/EmptyState";
import { MetricCard } from "@/components/MetricCard";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { SectionHeading } from "@/components/SectionHeading";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminKoxoDashboard } from "@/lib/internal-api";

export const metadata = {
  title: "KoXo - Administration",
};

export const dynamic = "force-dynamic";

export default async function AdminKoxoPage() {
  await requireAdminSession();
  const result = await getAdminKoxoDashboard();
  const dashboard = result.data;

  if (!dashboard) {
    return (
      <EmptyState
        description="Les données KoXo sont temporairement indisponibles."
        title="KoXo indisponible"
      />
    );
  }

  return (
    <>
      <PageHeader
        action={<StatusBadge label={`Schéma v${dashboard.schemaVersion}`} tone="info" />}
        description="Validation privée et non destructive des utilisateurs exportables vers KoXo."
        eyebrow="Pilotage"
        title="KoXo"
      />

      <div className="metrics-grid metrics-grid-three">
        <MetricCard
          detail="Utilisateurs actuellement exportables"
          label="Exportables"
          tone="green"
          value={String(dashboard.exportableUserCount)}
        />
        <MetricCard
          detail="Utilisateurs bloquant l'export global"
          label="Invalides"
          tone={dashboard.invalidUserCount > 0 ? "amber" : "slate"}
          value={String(dashboard.invalidUserCount)}
        />
        <MetricCard
          detail={
            dashboard.lastApiCallAt
              ? formatDateTime(dashboard.lastApiCallAt)
              : "Aucun appel API enregistré"
          }
          label="Dernier appel API"
          value={dashboard.lastRequestedStatus ?? "-"}
        />
      </div>

      <SectionCard ariaLabel="Dernière exécution KoXo">
        <SectionHeading
          description="Historique persistant des validations et exports KoXo demandés depuis le site."
          title="Dernière exécution"
        />
        {dashboard.lastRun ? (
          <dl className="detail-grid">
            <div>
              <dt>Source</dt>
              <dd>{dashboard.lastRun.source}</dd>
            </div>
            <div>
              <dt>Statut</dt>
              <dd>{dashboard.lastRun.status}</dd>
            </div>
            <div>
              <dt>Exécutée le</dt>
              <dd>{formatDateTime(dashboard.lastRun.createdAt)}</dd>
            </div>
            <div>
              <dt>Générée le</dt>
              <dd>
                {dashboard.lastRun.generatedAt
                  ? formatDateTime(dashboard.lastRun.generatedAt)
                  : "-"}
              </dd>
            </div>
            <div>
              <dt>Utilisateurs</dt>
              <dd>{dashboard.lastRun.userCount}</dd>
            </div>
            <div>
              <dt>Invalides</dt>
              <dd>{dashboard.lastRun.invalidUserCount}</dd>
            </div>
            <div>
              <dt>Adresse source</dt>
              <dd>{dashboard.lastRun.sourceAddress ?? "-"}</dd>
            </div>
            <div>
              <dt>Corrélation</dt>
              <dd>
                <code>{dashboard.lastRun.correlationId}</code>
              </dd>
            </div>
            <div>
              <dt>Message</dt>
              <dd>{dashboard.lastRun.summaryMessage}</dd>
            </div>
          </dl>
        ) : (
          <p className="field-hint">
            Aucune validation KoXo n&apos;a encore été enregistrée.
          </p>
        )}
        <AdminKoxoValidationButton />
      </SectionCard>

      <SectionCard ariaLabel="Aperçu JSON KoXo">
        <SectionHeading
          description="Aperçu limité aux premiers utilisateurs exportables, sans donnée technique sensible supplémentaire."
          title="Aperçu JSON"
        />
        <pre className="content-panel">
          {dashboard.preview
            ? JSON.stringify(dashboard.preview, null, 2)
            : JSON.stringify(
                {
                  error: "KOXO_EXPORT_VALIDATION_FAILED",
                  invalidUsers: dashboard.validationErrors,
                },
                null,
                2,
              )}
        </pre>
      </SectionCard>

      <SectionCard ariaLabel="Erreurs de validation KoXo">
        <SectionHeading
          description="L'export KoXo est refusé globalement tant que ces erreurs persistent."
          title="Erreurs de validation"
        />
        {dashboard.validationErrors.length === 0 ? (
          <p className="field-hint">Aucune erreur bloquante détectée.</p>
        ) : (
          <div className="content-panel">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Identifiant</th>
                  <th>Utilisateur portail</th>
                  <th>Champs</th>
                </tr>
              </thead>
              <tbody>
                {dashboard.validationErrors.map((error) => (
                  <tr key={`${error.portalUserId}:${error.identifiantUnique ?? "missing"}`}>
                    <td>{error.identifiantUnique ?? "-"}</td>
                    <td>{error.portalUserId}</td>
                    <td>{error.fields.join(", ")}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </SectionCard>

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
