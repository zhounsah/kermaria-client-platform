import { AdminRuntimeCenter } from "@/components/AdminRuntimeCenter";
import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { requireAdminSession } from "@/lib/auth";
import { getAdminRuntimeOverview } from "@/lib/internal-api";

export const dynamic = "force-dynamic";
export const metadata = {
  title: "Infrastructure et runtime - Administration",
};

export default async function AdminRuntimePage() {
  await requireAdminSession();
  const result = await getAdminRuntimeOverview();
  return (
    <>
      <PageHeader
        eyebrow="Centre de configuration"
        title="Infrastructure et runtime"
        description="API-INTERNAL, MariaDB, stockage et journalisation, avec la source réelle de chaque valeur appliquée. Lecture seule : ces réglages sont résolus au démarrage du service."
      />
      {result.error ? (
        <ErrorState
          title="Vue runtime indisponible"
          description="L'état d'exécution ne peut pas être chargé pour le moment."
          reference={result.correlationId}
        />
      ) : (
        <AdminRuntimeCenter overview={result.data} />
      )}
    </>
  );
}
