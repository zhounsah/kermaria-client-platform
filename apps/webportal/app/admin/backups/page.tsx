import { AdminBackupIntegrationForm } from "@/components/AdminBackupIntegrationForm";
import { AdminDataTable } from "@/components/AdminDataTable";
import { EmptyState } from "@/components/EmptyState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionHeading } from "@/components/SectionHeading";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminBackupIntegrations } from "@/lib/internal-api";

export const metadata = { title: "Sauvegardes - Administration" };
export const dynamic = "force-dynamic";

export default async function AdminBackupsPage() {
  await requireAdminSession();
  const result = await getAdminBackupIntegrations();

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Collecteur interne" tone="info" />}
        description="Associez les objets Veeam aux services client sans exposer les noms internes aux utilisateurs."
        eyebrow="Administration interne"
        title="Sauvegardes Veeam"
      />

      <section className="content-panel">
        <SectionHeading
          description="Utilisez l'identifiant stable du job Veeam, pas son nom d'affichage."
          title="Nouveau mapping"
        />
        <AdminBackupIntegrationForm />
      </section>

      <section className="request-history-section">
        <SectionHeading title="Mappings et collecte" />
        {result.data.length === 0 ? (
          <EmptyState
            description="Aucun mapping Veeam n'est configure."
            title="Aucun mapping"
          />
        ) : (
          <AdminDataTable
            caption="Mappings Veeam"
            columns={[
              "Client",
              "Service",
              "Job externe",
              "Etat",
              "Collecte",
              "Seuils",
            ]}
            rows={result.data.map((integration) => {
              const stale = integration.lastCollectionStatus === "stale";
              return [
                `${integration.customerName} (${integration.customerReference})`,
                integration.serviceName,
                <code key={`${integration.id}-job`}>
                  {integration.externalJobId}
                </code>,
                <StatusBadge
                  key={`${integration.id}-enabled`}
                  label={integration.enabled ? "Actif" : "Desactive"}
                  tone={integration.enabled ? "success" : "neutral"}
                />,
                <StatusBadge
                  key={`${integration.id}-collection`}
                  label={
                    stale
                      ? "Collecteur silencieux"
                      : integration.lastCollectedAt
                        ? formatDateTime(integration.lastCollectedAt)
                        : "Jamais collecte"
                  }
                  tone={stale ? "warning" : "success"}
                />,
                `${integration.expectedIntervalMinutes} / ${integration.criticalAfterMinutes} / ${integration.staleAfterMinutes} min`,
              ];
            })}
          />
        )}
      </section>

      <MockNotice correlationId={result.correlationId} source={result.source} />
    </>
  );
}
