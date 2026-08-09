import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";

import { AdminEditorialForm } from "@/components/AdminEditorialForm";
import { requireAdminSession } from "@/lib/auth";
import {
  contentTypeFromSegment,
  editorialSectionTitle,
} from "@/lib/editorial-admin";
import {
  getAdminEditorialContent,
  getAdminEditorialList,
  getAdminEditorialRevisions,
} from "@/lib/internal-api";

type AdminEditorialDetailPageProps = {
  params: Promise<{ contentType: string; id: string }>;
};

export const metadata: Metadata = {
  title: "Contenu éditorial - Administration",
};

export default async function AdminEditorialDetailPage({
  params,
}: AdminEditorialDetailPageProps) {
  await requireAdminSession();
  const { contentType: segment, id } = await params;
  const contentType = contentTypeFromSegment(segment);
  if (!contentType) {
    notFound();
  }

  const [contentResult, listResult, revisionsResult] = await Promise.all([
    getAdminEditorialContent(id),
    getAdminEditorialList(`contentType=${contentType}`),
    getAdminEditorialRevisions(id),
  ]);
  if (!contentResult.data || contentResult.data.contentType !== contentType) {
    notFound();
  }

  return (
    <div className="stack-panels">
      <div className="page-heading">
        <div>
          <Link className="text-link" href={`/admin/editorial/${segment}`}>
            Retour
          </Link>
          <span className="card-kicker">{editorialSectionTitle(contentType)}</span>
          <h1>{contentResult.data.title}</h1>
        </div>
      </div>

      <AdminEditorialForm
        categories={listResult.data.categories}
        content={contentResult.data}
        contentType={contentType}
        mode="edit"
      />

      <section className="content-panel">
        <div className="section-heading">
          <div>
            <span className="card-kicker">Historique</span>
            <h2>Versions enregistrées</h2>
          </div>
        </div>
        {revisionsResult.data.length > 0 ? (
          <div className="admin-table-wrapper">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Version</th>
                  <th>Action</th>
                  <th>Date</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {revisionsResult.data.map((revision) => (
                  <tr key={revision.id}>
                    <td>{revision.versionNumber}</td>
                    <td>{revision.action}</td>
                    <td>{formatDate(revision.createdAt)}</td>
                    <td>
                      <Link
                        className="text-link"
                        href={`/admin/editorial/revisions/${revision.id}`}
                      >
                        Consulter
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="empty-copy">Aucune version précédente pour le moment.</p>
        )}
      </section>
    </div>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("fr-FR", {
    dateStyle: "short",
    timeStyle: "short",
  }).format(new Date(value));
}
