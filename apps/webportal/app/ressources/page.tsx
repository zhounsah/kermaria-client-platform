import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";

import { ErrorState } from "@/components/ErrorState";
import { getPublicEditorialSitemap } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";

export const dynamic = "force-dynamic";

export const metadata: Metadata = buildPublicMetadata({
  title: "Ressources",
  description:
    "Pages de ressources publiées par Zachary IT autour de la sauvegarde, du stockage documentaire et de l'accès distant.",
  path: "/ressources",
});

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
        <p className="eyebrow">Ressources</p>
        <h1>Ressources Zachary IT</h1>
        <p>
          Retrouvez ici les pages publiées depuis le back-office éditorial.
        </p>
      </header>

      {pages.length > 0 ? (
        <section aria-labelledby="ressources-list-title">
          <h2 id="ressources-list-title">Pages publiées</h2>
          <div className="seo-hub-list">
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
          </div>
        </section>
      ) : (
        <p className="empty-copy">
          Aucune ressource publiée pour le moment.
        </p>
      )}
    </div>
  );
}
