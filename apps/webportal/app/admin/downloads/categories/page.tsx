import Link from "next/link";

import { AdminDownloadCategoriesManager } from "@/components/AdminDownloadCategoriesManager";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { requireAdminSession } from "@/lib/auth";
import { getAdminDownloadCategories } from "@/lib/internal-api";

export const metadata = {
  title: "Catégories téléchargements - Administration",
};

export const dynamic = "force-dynamic";

export default async function AdminDownloadCategoriesPage() {
  await requireAdminSession();
  const result = await getAdminDownloadCategories();

  return (
    <>
      <PageHeader
        action={
          <Link className="button" href="/admin/downloads/new">
            Nouveau téléchargement
          </Link>
        }
        description="Classez les ressources dans des catégories simples et lisibles pour les clients."
        eyebrow="Téléchargements"
        title="Catégories"
      />

      <div className="stack-row">
        <Link className="text-link" href="/admin/downloads">
          ← Retour aux téléchargements
        </Link>
      </div>

      {result.error ? (
        <ErrorState
          description="Impossible de charger les catégories pour le moment."
          reference={result.correlationId}
          title="Catégories indisponibles"
        />
      ) : (
        <AdminDownloadCategoriesManager categories={result.data} />
      )}

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
