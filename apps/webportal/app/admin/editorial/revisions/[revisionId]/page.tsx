import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";

import { AdminEditorialRestoreButton } from "@/components/AdminEditorialRestoreButton";
import { ManagedMarkdown } from "@/components/ManagedMarkdown";
import { requireAdminSession } from "@/lib/auth";
import { contentTypeSegment } from "@/lib/editorial-admin";
import { getAdminEditorialRevision } from "@/lib/internal-api";

type AdminEditorialRevisionPageProps = {
  params: Promise<{ revisionId: string }>;
};

export const metadata: Metadata = {
  title: "Version éditoriale - Administration",
};

export default async function AdminEditorialRevisionPage({
  params,
}: AdminEditorialRevisionPageProps) {
  await requireAdminSession();
  const { revisionId } = await params;
  const result = await getAdminEditorialRevision(revisionId);
  if (!result.data) {
    notFound();
  }

  const revision = result.data;
  const snapshot = revision.snapshot;
  const segment = contentTypeSegment(snapshot.contentType);

  return (
    <div className="stack-panels">
      <div className="page-heading">
        <div>
          <Link
            className="text-link"
            href={`/admin/editorial/${segment}/${snapshot.id}`}
          >
            Retour au contenu
          </Link>
          <span className="card-kicker">Historique</span>
          <h1>Version {revision.versionNumber}</h1>
          <p>
            {revision.action} · {formatDate(revision.createdAt)}
          </p>
        </div>
        <AdminEditorialRestoreButton
          contentType={snapshot.contentType}
          revisionId={revision.id}
        />
      </div>

      <section className="content-panel">
        <dl className="details-grid">
          <div>
            <dt>Titre</dt>
            <dd>{snapshot.title}</dd>
          </div>
          <div>
            <dt>Slug</dt>
            <dd>{snapshot.slug}</dd>
          </div>
          <div>
            <dt>État</dt>
            <dd>{snapshot.status}</dd>
          </div>
        </dl>
      </section>

      <section className="managed-content-preview-card">
        <div className="managed-content-preview-header">
          <span className="card-kicker">Aperçu</span>
          <h2>{snapshot.title}</h2>
        </div>
        <ManagedMarkdown markdown={snapshot.bodyMarkdown} withAnchors />
      </section>
    </div>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("fr-FR", {
    dateStyle: "long",
    timeStyle: "short",
  }).format(new Date(value));
}
