import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";

import { ErrorState } from "@/components/ErrorState";
import { getPublicEditorialSitemap } from "@/lib/internal-api";

export const dynamic = "force-dynamic";

export const metadata: Metadata = {
  title: "Services",
  description:
    "Pages de ressources publiées par Zachary IT autour des services informatiques proposés.",
  alternates: { canonical: "/ressources" },
};

export default async function RessourcesPage() {
  await connection();
  const result = await getPublicEditorialSitemap();

  if (result.error) {
    return (
      <ErrorState
        description="Impossible de charger les ressources pour le moment."
        reference={result.correlationId}
        title="Ressources indisponibles"
      />
    );
  }

  const pages = result.data
    .filter(
      (entry) =>
        entry.contentType === "seo_page"
        && !entry.noIndex
        && entry.publicPath,
    )
    .sort((first, second) => first.title.localeCompare(second.title, "fr"));

  return (
    <div className="seo-hub-page">
      <header className="seo-hub-header">
        <p className="eyebrow">Services</p>
        <h1>Ressources et services Zachary IT</h1>
        <p>
          Retrouvez ici les pages publiées depuis le back-office éditorial.
        </p>
      </header>

      {pages.length > 0 ? (
        <section className="seo-hub-list" aria-label="Pages publiées">
          {pages.map((page) => (
            <Link
              className="wiki-list-item"
              href={page.publicPath ?? `/${page.slug}`}
              key={page.id}
            >
              <strong>{page.title}</strong>
              {page.summary ? <span>{page.summary}</span> : null}
            </Link>
          ))}
        </section>
      ) : (
        <p className="empty-copy">
          Aucune page de service publiée pour le moment.
        </p>
      )}
    </div>
  );
}
