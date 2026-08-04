import Link from "next/link";

import { AdminClientSolutionForm } from "@/components/AdminClientSolutionForm";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminClientSolution } from "@/lib/internal-api";

export const metadata = {
  title: "Édition solution - Administration",
};

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ id: string }>;
};

export default async function AdminClientSolutionDetailPage({
  params,
}: PageProps) {
  await requireAdminSession();
  const { id } = await params;
  const result = await getAdminClientSolution(id);

  if (result.error || !result.data) {
    return (
      <>
        <PageHeader
          description="La solution demandée est temporairement indisponible."
          eyebrow="Portail solutions"
          title="Édition de la solution"
        />
        <ErrorState
          description="Impossible de charger cette solution pour le moment."
          reference={result.correlationId}
          title="Solution indisponible"
        />
      </>
    );
  }

  const solution = result.data;

  return (
    <>
      <PageHeader
        description={
          solution.status === "published"
            ? "Cette tuile est visible sur la page publique."
            : "Cette tuile est en brouillon : elle reste masquée du site public."
        }
        eyebrow="Portail solutions"
        title={solution.title}
      />

      <div className="stack-row">
        <Link className="text-link" href="/admin/solutions">
          ← Retour à la liste
        </Link>
        <Link className="text-link" href="/solutions">
          Voir la page publique
        </Link>
      </div>

      <SectionCard ariaLabel="Métadonnées de la solution">
        <span className="card-kicker">Suivi</span>
        <h2>État courant</h2>
        <dl className="profile-details">
          <div>
            <dt>Identifiant</dt>
            <dd>{solution.id}</dd>
          </div>
          <div>
            <dt>Identifiant d&apos;URL</dt>
            <dd>{solution.slug}</dd>
          </div>
          <div>
            <dt>Logo</dt>
            <dd>{solution.logoOriginalName ?? "Aucun logo stocké"}</dd>
          </div>
          <div>
            <dt>Créée le</dt>
            <dd>{formatDateTime(solution.createdAt)}</dd>
          </div>
          <div>
            <dt>Mise à jour le</dt>
            <dd>{formatDateTime(solution.updatedAt)}</dd>
          </div>
        </dl>
      </SectionCard>

      <AdminClientSolutionForm mode="edit" solution={solution} />

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
