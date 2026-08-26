import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";

import { AdminEditorialCategoryForm } from "@/components/AdminEditorialCategoryForm";
import { requireAdminSession } from "@/lib/auth";
import {
  contentTypeFromSegment,
  editorialSectionTitle,
} from "@/lib/editorial-admin";
import { getAdminEditorialList } from "@/lib/internal-api";

type AdminEditorialListPageProps = {
  params: Promise<{ contentType: string }>;
  searchParams: Promise<{ status?: string; query?: string }>;
};

export const metadata: Metadata = {
  title: "Contenus éditoriaux - Administration",
};

export default async function AdminEditorialListPage({
  params,
  searchParams,
}: AdminEditorialListPageProps) {
  await requireAdminSession();
  const { contentType: segment } = await params;
  const contentType = contentTypeFromSegment(segment);
  if (!contentType) {
    notFound();
  }

  const filters = await searchParams;
  const query = new URLSearchParams({ contentType });
  if (filters.status) query.set("status", filters.status);
  if (filters.query) query.set("query", filters.query);
  const result = await getAdminEditorialList(query.toString());

  return (
    <div className="stack-panels">
      <div className="page-heading">
        <div>
          <span className="card-kicker">Éditorial</span>
          <h1>{editorialSectionTitle(contentType)}</h1>
        </div>
        <Link className="button" href={`/admin/editorial/${segment}/new`}>
          {createLabel(contentType)}
        </Link>
      </div>

      <form className="filter-bar" action={`/admin/editorial/${segment}`}>
        <input
          defaultValue={filters.query ?? ""}
          name="query"
          placeholder="Rechercher"
          type="search"
        />
        <select defaultValue={filters.status ?? ""} name="status">
          <option value="">Tous les états</option>
          <option value="draft">Brouillon</option>
          <option value="published">Publié</option>
          <option value="archived">Archivé</option>
          <option value="scheduled">Planifié</option>
        </select>
        <button className="button button-secondary" type="submit">Filtrer</button>
      </form>

      <div className="admin-table-wrapper">
        <table className="admin-table">
          <thead>
            {contentType === "faq" ? (
              <tr>
                <th>Question</th>
                <th>Catégorie / scope</th>
                <th>État</th>
                <th>Ordre</th>
                <th>Modification</th>
                <th>Actions</th>
              </tr>
            ) : null}
            {contentType === "wiki_article" ? (
              <tr>
                <th>Titre</th>
                <th>Catégorie</th>
                <th>État</th>
                <th>Slug</th>
                <th>Modification</th>
                <th>Publication</th>
                <th>Actions</th>
              </tr>
            ) : null}
            {contentType === "seo_page" ? (
              <tr>
                <th>Titre</th>
                <th>Catégorie</th>
                <th>État</th>
                <th>Slug</th>
                <th>Indexation</th>
                <th>Modification</th>
                <th>Publication</th>
                <th>Actions</th>
              </tr>
            ) : null}
          </thead>
          <tbody>
            {result.data.items.map((item) => (
              <tr key={item.id}>
                {contentType === "faq" ? (
                  <>
                    <td>{item.title}</td>
                    <td>
                      <span>{item.categoryName ?? "Sans catégorie"}</span>
                      {item.faqScopes.length > 0 ? (
                        <small>{item.faqScopes.join(", ")}</small>
                      ) : (
                        <small>Aucun scope</small>
                      )}
                    </td>
                    <td>{statusLabel(item.status)}</td>
                    <td>{item.sortOrder}</td>
                    <td>{formatDate(item.updatedAt)}</td>
                  </>
                ) : null}
                {contentType === "wiki_article" ? (
                  <>
                    <td>{item.title}</td>
                    <td>{item.categoryName ?? "Sans catégorie"}</td>
                    <td>{statusLabel(item.status)}</td>
                    <td>{item.slug}</td>
                    <td>{formatDate(item.updatedAt)}</td>
                    <td>{item.publishedAt ? formatDate(item.publishedAt) : "—"}</td>
                  </>
                ) : null}
                {contentType === "seo_page" ? (
                  <>
                    <td>{item.title}</td>
                    <td>{item.categoryName ?? "Sans catégorie"}</td>
                    <td>{statusLabel(item.status)}</td>
                    <td>{item.slug}</td>
                    <td>{item.noIndex ? "Noindex" : "Indexable"}</td>
                    <td>{formatDate(item.updatedAt)}</td>
                    <td>{item.publishedAt ? formatDate(item.publishedAt) : "—"}</td>
                  </>
                ) : null}
                <td>
                  <Link
                    className="text-link"
                    href={`/admin/editorial/${segment}/${item.id}`}
                  >
                    Modifier
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {result.data.items.length === 0 ? (
        <div className="editorial-empty-state">
          <strong>{emptyTitle(contentType)}</strong>
          <p>{emptyText(contentType)}</p>
          <div className="stack-row">
            <Link className="button" href={`/admin/editorial/${segment}/new`}>
              {createLabel(contentType)}
            </Link>
            <Link
              className="button button-secondary"
              href={`/admin/editorial/${segment}/new`}
            >
              Importer un Markdown
            </Link>
          </div>
        </div>
      ) : null}

      <AdminEditorialCategoryForm
        categories={result.data.categories.filter(
          (category) => category.contentType === contentType,
        )}
        contentType={contentType}
      />
    </div>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("fr-FR", {
    dateStyle: "short",
    timeStyle: "short",
  }).format(new Date(value));
}

function statusLabel(status: string) {
  if (status === "draft") return "Brouillon";
  if (status === "published") return "Publié";
  if (status === "archived") return "Archivé";
  if (status === "scheduled") return "Planifié";
  return status;
}

function createLabel(contentType: string) {
  if (contentType === "wiki_article") return "Créer un article";
  if (contentType === "seo_page") return "Créer une page";
  return "Créer une question";
}

function emptyTitle(contentType: string) {
  if (contentType === "wiki_article") return "Aucun article pour le moment.";
  if (contentType === "seo_page") return "Aucune page SEO pour le moment.";
  return "Aucune question FAQ pour le moment.";
}

function emptyText(contentType: string) {
  if (contentType === "wiki_article") {
    return "Créez votre premier article ou importez un fichier Markdown.";
  }
  if (contentType === "seo_page") {
    return "Créez votre première page SEO ou importez un fichier Markdown.";
  }
  return "Créez votre première question ou importez une réponse Markdown.";
}
