import { AdminIntegrationsCenter } from "@/components/AdminIntegrationsCenter";
import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { requireAdminSession } from "@/lib/auth";
import { describeHCaptchaIntegration } from "@/lib/integrations-hcaptcha";
import { getAdminIntegrations } from "@/lib/internal-api";

export const dynamic = "force-dynamic";
export const metadata = {
  title: "Intégrations - Administration",
};

export default async function AdminIntegrationsPage() {
  await requireAdminSession();
  const result = await getAdminIntegrations();
  // hCaptcha est la seule intégration configurée côté WEBPORTAL : elle est
  // ajoutée ici, sans jamais transporter son secret.
  const hcaptcha = describeHCaptchaIntegration();
  return (
    <>
      <PageHeader
        eyebrow="Centre de configuration"
        title="Intégrations"
        description="Observer sans révéler : modes, configuration et dernières opérations des intégrations. Aucun secret n'est affiché, aucun mode n'est modifiable depuis cette page."
      />
      {result.error ? (
        <ErrorState
          title="Intégrations indisponibles"
          description="L'état des intégrations ne peut pas être chargé pour le moment."
          reference={result.correlationId}
        />
      ) : (
        <AdminIntegrationsCenter
          checkedAt={result.data.checkedAt}
          integrations={[...result.data.integrations, hcaptcha]}
        />
      )}
    </>
  );
}
