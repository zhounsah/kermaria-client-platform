import { AdminDemoTemplatesCenter } from "@/components/AdminDemoTemplatesCenter";
import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { requireAdminSession } from "@/lib/auth";
import { getAdminDemoTemplateConfiguration } from "@/lib/internal-api";

export const dynamic = "force-dynamic";
export const metadata = {
  title: "Démonstrations - Administration",
};

export default async function AdminDemoTemplatesPage() {
  await requireAdminSession();
  const result = await getAdminDemoTemplateConfiguration();
  return (
    <>
      <PageHeader
        eyebrow="Centre de configuration"
        title="Modèles de démonstration"
        description="Contenu semé sur un compte de démonstration : services affichés, ordre et périmètre. Les profils, comptes et conversions restent administrés depuis la page Démonstrations."
      />
      {result.error ? (
        <ErrorState
          title="Modèles indisponibles"
          description="Les modèles de démonstration ne peuvent pas être chargés pour le moment."
          reference={result.correlationId}
        />
      ) : (
        <AdminDemoTemplatesCenter initialView={result.data} />
      )}
    </>
  );
}
