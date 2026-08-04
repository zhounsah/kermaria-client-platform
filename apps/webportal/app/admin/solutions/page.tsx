import Link from "next/link";

import { AdminClientSolutionPortalSettingsForm } from "@/components/AdminClientSolutionPortalSettingsForm";
import { AdminDataTable } from "@/components/AdminDataTable";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminClientSolutionPortal } from "@/lib/internal-api";

export const metadata = {
  title: "Portail solutions - Administration",
};

export const dynamic = "force-dynamic";

const statusLabels = {
  published: { label: "Publiée", tone: "success" as const },
  draft: { label: "Brouillon", tone: "neutral" as const },
} as const;

export default async function AdminClientSolutionsPage() {
  await requireAdminSession();
  const result = await getAdminClientSolutionPortal();

  return (
    <>
      <PageHeader
        action={
          <div className="stack-row">
            <Link className="button button-secondary" href="/solutions">
              Voir la page publique
            </Link>
            <Link className="button" href="/admin/solutions/new">
              Nouvelle solution
            </Link>
          </div>
        }
        description="Gérez les tuiles affichées sur la page publique /solutions : nom, logo, lien, ordre et visibilité."
        eyebrow="Administration interne"
        title="Portail solutions"
      />

      {result.error ? (
        <ErrorState
          description="Impossible de charger le portail solutions pour le moment."
          reference={result.correlationId}
          title="Portail solutions indisponible"
        />
      ) : (
        <>
          <AdminClientSolutionPortalSettingsForm
            settings={result.data.settings}
          />

          {result.data.solutions.length === 0 ? (
            <EmptyState
              action={
                <Link className="button" href="/admin/solutions/new">
                  Créer une solution
                </Link>
              }
              description="Aucune tuile n'est encore configurée sur la page publique."
              title="Aucune solution"
            />
          ) : (
            <AdminDataTable
              caption="Solutions publiées sur la vitrine"
              columns={[
                "Ordre",
                "Nom",
                "Lien",
                "Logo",
                "État",
                "Mise à jour",
                "Action",
              ]}
              rows={result.data.solutions.map((solution) => [
                String(solution.displayOrder),
                <div key={`${solution.id}-title`}>
                  <strong>{solution.title}</strong>
                  <div className="field-hint">{solution.slug}</div>
                </div>,
                <span
                  className="multiline-text"
                  key={`${solution.id}-target`}
                >
                  {solution.targetUrl}
                </span>,
                solution.hasLogo ? solution.logoOriginalName : "Initiales",
                <StatusBadge
                  key={`${solution.id}-status`}
                  label={statusLabels[solution.status].label}
                  tone={statusLabels[solution.status].tone}
                />,
                formatDateTime(solution.updatedAt),
                <Link
                  className="table-action"
                  href={`/admin/solutions/${encodeURIComponent(solution.id)}`}
                  key={`${solution.id}-edit`}
                >
                  Modifier
                </Link>,
              ])}
            />
          )}
        </>
      )}

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
