import Link from "next/link";

import { AdminDownloadForm } from "@/components/AdminDownloadForm";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { requireAdminSession } from "@/lib/auth";
import {
  getAdminCatalog,
  getAdminDownloadCategories,
} from "@/lib/internal-api";

export const metadata = {
  title: "Nouveau téléchargement - Administration",
};

export const dynamic = "force-dynamic";

export default async function AdminNewDownloadPage() {
  await requireAdminSession();
  const [categoriesResult, catalogResult] = await Promise.all([
    getAdminDownloadCategories(),
    getAdminCatalog(),
  ]);

  if (categoriesResult.error) {
    return (
      <>
        <PageHeader
          description="Le formulaire de création est temporairement indisponible."
          eyebrow="Téléchargements"
          title="Nouveau téléchargement"
        />
        <ErrorState
          description="Impossible de préparer les catégories ou les règles de visibilité."
          reference={categoriesResult.correlationId}
          title="Création indisponible"
        />
      </>
    );
  }

  return (
    <>
      <PageHeader
        description="Ajoutez une ressource claire et rassurante pour l'espace client, avec visibilité contrôlée par packs, offres ou services."
        eyebrow="Téléchargements"
        title="Nouveau téléchargement"
      />

      <div className="stack-row">
        <Link className="text-link" href="/admin/downloads">
          ← Retour à la liste
        </Link>
        <Link className="text-link" href="/admin/downloads/categories">
          Gérer les catégories
        </Link>
      </div>

      <SectionCard ariaLabel="Conseils de création">
        <span className="card-kicker">Conseil UX</span>
        <h2>Parlez usage client avant technique interne</h2>
        <p>
          Préférez un intitulé compréhensible, une description courte et des
          consignes simples. Les détails techniques restent dans le back-office,
          pas dans le bouton de téléchargement.
        </p>
      </SectionCard>

      <AdminDownloadForm
        categories={categoriesResult.data}
        mode="create"
        offerCatalogAvailable={!catalogResult.error}
        offers={catalogResult.error ? [] : catalogResult.data}
      />

      <MockNotice
        correlationId={categoriesResult.correlationId}
        source={categoriesResult.source}
      />
    </>
  );
}
