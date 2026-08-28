import Link from "next/link";

import { AdminCommunicationsCenter } from "@/components/AdminCommunicationsCenter";
import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { requireAdminSession } from "@/lib/auth";
import { getAdminCommunicationTemplates } from "@/lib/internal-api";

export const dynamic = "force-dynamic";
export const metadata = { title: "Messages & communications - Administration" };

export default async function AdminCommunicationsPage() {
  await requireAdminSession();
  const result = await getAdminCommunicationTemplates();
  return (
    <>
      <PageHeader
        eyebrow="Centre de configuration"
        title="Messages & communications"
        description="E-mails transactionnels, notifications du portail et textes système. Chaque modèle possède une liste fermée de variables ; un modèle absent ou désactivé retombe sur le texte intégré au code."
      />
      <p>
        <Link className="back-link" href="/admin/settings">
          <span aria-hidden="true">←</span> Retour au centre de configuration
        </Link>
      </p>
      {result.error ? (
        <ErrorState
          title="Modèles indisponibles"
          description="Les modèles de communication ne peuvent pas être chargés pour le moment."
          reference={result.correlationId}
        />
      ) : (
        <AdminCommunicationsCenter initialCollection={result.data} />
      )}
    </>
  );
}
