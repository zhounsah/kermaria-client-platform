import Link from "next/link";

import { AdminDataTable } from "@/components/AdminDataTable";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminManagedContentList } from "@/lib/internal-api";

export const metadata = {
  title: "Contenus - Administration",
};

export const dynamic = "force-dynamic";

const contentTypeLabels = {
  legal: { label: "Légal", tone: "warning" as const },
  pack_sheet: { label: "Fiche pack", tone: "info" as const },
  page: { label: "Page", tone: "neutral" as const },
};

export default async function AdminManagedContentListPage() {
  await requireAdminSession();
  const result = await getAdminManagedContentList();
  const entries = result.data;

  return (
    <>
      <PageHeader
        description="CGV, mentions légales et fiches techniques packs éditables depuis le back-office, avec persistance côté API."
        eyebrow="Administration interne"
        title="Contenus administrables"
      />

      <p>
        <Link className="button button-secondary" href="/admin/public-pack-catalog">
          Ouvrir la vitrine packs
        </Link>
      </p>

      {result.error ? (
        <ErrorState
          description="Impossible de charger les contenus administrables pour le moment."
          reference={result.correlationId}
          title="Contenus indisponibles"
        />
      ) : entries.length === 0 ? (
        <EmptyState
          description="Aucun contenu administrable n'est encore disponible."
          title="Aucun contenu"
        />
      ) : (
        <AdminDataTable
          caption="Contenus administrables"
          columns={["Type", "Titre", "URL publique", "Version", "Mise à jour", "Action"]}
          rows={entries.map((entry) => {
            const contentType = contentTypeLabels[entry.contentType];

            return [
              <StatusBadge
                key={`${entry.key}-type`}
                label={contentType.label}
                tone={contentType.tone}
              />,
              <strong key={`${entry.key}-title`}>{entry.title}</strong>,
              <Link
                className="text-link"
                href={entry.publicPath}
                key={`${entry.key}-public`}
                prefetch={false}
                rel="noreferrer"
                target="_blank"
              >
                {entry.publicPath}
              </Link>,
              entry.versionLabel ?? "—",
              entry.updatedAt ? formatDateTime(entry.updatedAt) : "—",
              <Link
                className="table-action"
                href={`/admin/content/${encodeURIComponent(entry.key)}`}
                key={`${entry.key}-edit`}
              >
                Modifier
              </Link>,
            ];
          })}
        />
      )}

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
