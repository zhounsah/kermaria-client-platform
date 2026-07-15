import Link from "next/link";

import { AdminDataTable } from "@/components/AdminDataTable";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminDownloads } from "@/lib/internal-api";

export const metadata = {
  title: "Téléchargements - Administration",
};

export const dynamic = "force-dynamic";

const resourceTypeLabels = {
  software: "Logiciel",
  script: "Script",
  rdp: "Fichier RDP",
  document: "Documentation",
  tool: "Outil",
  other: "Autre",
} as const;

const sourceKindLabels = {
  internal_file: "Fichier interne",
  external_url: "Lien externe",
} as const;

const visibilityModeLabels = {
  all_clients: { label: "Tous les clients", tone: "neutral" as const },
  targeted: { label: "Cible", tone: "warning" as const },
} as const;

const statusLabels = {
  active: { label: "Active", tone: "success" as const },
  inactive: { label: "Inactive", tone: "neutral" as const },
} as const;

export default async function AdminDownloadsPage() {
  await requireAdminSession();
  const result = await getAdminDownloads();
  const errorDescription = result.error
    ? process.env.NODE_ENV === "production"
      ? "Impossible de charger le centre de téléchargements pour le moment."
      : `${result.error.code} - ${result.error.message} (source: ${result.source})`
    : null;

  return (
    <>
      <PageHeader
        action={
          <div className="stack-row">
            <Link
              className="button button-secondary"
              href="/admin/downloads/categories"
            >
              Catégories
            </Link>
            <Link className="button" href="/admin/downloads/new">
              Nouveau téléchargement
            </Link>
          </div>
        }
        description="Gérez les logiciels, scripts, fichiers RDP et documentations visibles depuis l'espace client."
        eyebrow="Administration interne"
        title="Téléchargements"
      />

      {result.error ? (
        <ErrorState
          description={
            errorDescription
            ?? "Impossible de charger le centre de téléchargements pour le moment."
          }
          reference={result.correlationId}
          title="Téléchargements indisponibles"
        />
      ) : result.data.length === 0 ? (
        <EmptyState
          action={
            <Link className="button" href="/admin/downloads/new">
              Créer une ressource
            </Link>
          }
          description="Aucun téléchargement n'est encore configuré."
          title="Aucune ressource"
        />
      ) : (
        <AdminDataTable
          caption="Téléchargements"
          columns={[
            "Catégorie",
            "Titre",
            "Type",
            "Source",
            "Visibilité",
            "État",
            "Mise à jour",
            "Action",
          ]}
          rows={result.data.map((item) => [
            item.categoryTitle,
            <div key={`${item.id}-title`}>
              <strong>{item.title}</strong>
              <div className="field-hint">{item.shortDescription}</div>
            </div>,
            resourceTypeLabels[item.resourceType],
            <div key={`${item.id}-source`}>
              <div>{sourceKindLabels[item.sourceKind]}</div>
              <div className="field-hint">
                {item.sourceKind === "internal_file"
                  ? item.fileOriginalName ?? "Aucun fichier"
                  : "Redirection contrôlée"}
              </div>
            </div>,
            <StatusBadge
              key={`${item.id}-visibility`}
              label={visibilityModeLabels[item.visibilityMode].label}
              tone={visibilityModeLabels[item.visibilityMode].tone}
            />,
            <StatusBadge
              key={`${item.id}-status`}
              label={statusLabels[item.status].label}
              tone={statusLabels[item.status].tone}
            />,
            formatDateTime(item.updatedAt),
            <Link
              className="table-action"
              href={`/admin/downloads/${encodeURIComponent(item.id)}`}
              key={`${item.id}-edit`}
            >
              Modifier
            </Link>,
          ])}
        />
      )}

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
