import Link from "next/link";

import { AdminDownloadForm } from "@/components/AdminDownloadForm";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import {
  getAdminCatalog,
  getAdminDownload,
  getAdminDownloadCategories,
} from "@/lib/internal-api";

export const metadata = {
  title: "Édition téléchargement - Administration",
};

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ id: string }>;
};

export default async function AdminDownloadDetailPage({ params }: PageProps) {
  await requireAdminSession();
  const { id } = await params;
  const [categoriesResult, catalogResult, downloadResult] = await Promise.all([
    getAdminDownloadCategories(),
    getAdminCatalog(),
    getAdminDownload(id),
  ]);

  if (
    categoriesResult.error
    || downloadResult.error
    || !downloadResult.data
  ) {
    return (
      <>
        <PageHeader
          description="La ressource demandée est temporairement indisponible."
          eyebrow="Téléchargements"
          title="Édition du téléchargement"
        />
        <ErrorState
          description="Impossible de charger ce téléchargement pour le moment."
          reference={downloadResult.correlationId}
          title="Téléchargement indisponible"
        />
      </>
    );
  }

  const download = downloadResult.data;

  return (
    <>
      <PageHeader
        description={`Catégorie : ${download.categoryTitle}`}
        eyebrow="Téléchargements"
        title={download.title}
      />

      <div className="stack-row">
        <Link className="text-link" href="/admin/downloads">
          ← Retour à la liste
        </Link>
        <Link className="text-link" href="/admin/downloads/categories">
          Voir les catégories
        </Link>
      </div>

      <SectionCard ariaLabel="Métadonnées de la ressource">
        <span className="card-kicker">Suivi</span>
        <h2>Historique et état courant</h2>
        <dl className="profile-details">
          <div>
            <dt>Identifiant</dt>
            <dd>{download.id}</dd>
          </div>
          <div>
            <dt>Source</dt>
            <dd>{download.sourceKind === "internal_file" ? "Interne" : "Externe"}</dd>
          </div>
          <div>
            <dt>Fichier interne</dt>
            <dd>{download.fileOriginalName ?? "Aucun fichier stocké"}</dd>
          </div>
          <div>
            <dt>Règles actives</dt>
            <dd>{String(download.rules.length)}</dd>
          </div>
          <div>
            <dt>Créé le</dt>
            <dd>{formatDateTime(download.createdAt)}</dd>
          </div>
          <div>
            <dt>Mis à jour le</dt>
            <dd>{formatDateTime(download.updatedAt)}</dd>
          </div>
        </dl>
      </SectionCard>

      <AdminDownloadForm
        categories={categoriesResult.data}
        download={download}
        mode="edit"
        offerCatalogAvailable={!catalogResult.error}
        offers={catalogResult.error ? [] : catalogResult.data}
      />

      <MockNotice
        correlationId={downloadResult.correlationId}
        source={downloadResult.source}
      />
    </>
  );
}
