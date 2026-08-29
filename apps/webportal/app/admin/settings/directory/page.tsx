import { AdminDirectoryCenter } from "@/components/AdminDirectoryCenter";
import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { AdminSettingsNavigation } from "@/components/AdminSettingsNavigation";
import { requireAdminSession } from "@/lib/auth";
import { getAdminDirectoryOverview } from "@/lib/internal-api";

export const dynamic = "force-dynamic";
export const metadata = {
  title: "Annuaire et KoXo - Administration",
};

export default async function AdminDirectoryPage() {
  await requireAdminSession();
  const result = await getAdminDirectoryOverview();
  return (
    <>
      <PageHeader
        eyebrow="Centre de configuration"
        title="Annuaire et KoXo"
        description="Autorités, périmètres d'écriture, racines autorisées et écritures réellement demandées par l'API. Lecture seule."
      />
      <AdminSettingsNavigation />
      {result.error ? (
        <ErrorState
          title="Vue annuaire indisponible"
          description="L'état de l'annuaire ne peut pas être chargé pour le moment."
          reference={result.correlationId}
        />
      ) : (
        <AdminDirectoryCenter overview={result.data} />
      )}
    </>
  );
}
