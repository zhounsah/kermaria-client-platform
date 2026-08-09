import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";

import { ErrorState } from "@/components/ErrorState";
import { getPublicWikiHome, searchPublicWiki } from "@/lib/internal-api";
import { getWikiRobots, wikiCanonical } from "@/lib/wiki-seo";

export const dynamic = "force-dynamic";

type WikiHomePageProps = {
  searchParams: Promise<{ q?: string }>;
};

export async function generateMetadata(): Promise<Metadata> {
  const { robots } = await getWikiRobots();
  return {
    title: "Wiki",
    description: "Centre d'aide public Zachary IT.",
    alternates: { canonical: wikiCanonical("/") },
    robots,
  };
}

export default async function WikiHomePage({
  searchParams,
}: WikiHomePageProps) {
  await connection();
  const { q } = await searchParams;
  const query = q?.trim() ?? "";
  const result = query.length >= 2
    ? await searchPublicWiki(query)
    : await getPublicWikiHome();

  if (result.error) {
    return (
      <ErrorState
        description="Impossible de charger le wiki pour le moment."
        reference={result.correlationId}
        title="Wiki indisponible"
      />
    );
  }

  const categories = "categories" in result.data ? result.data.categories : [];
  const articles = "items" in result.data ? result.data.items : result.data;

  return (
    <div className="wiki-page">
      <header className="wiki-header">
        <p className="eyebrow">Centre d&apos;aide</p>
        <h1>Wiki Zachary IT</h1>
        <form className="wiki-search" action="/wiki">
          <label htmlFor="wiki-q">Rechercher</label>
          <div className="wiki-search-row">
            <input
              defaultValue={query}
              id="wiki-q"
              name="q"
              placeholder="Titre, résumé ou contenu"
              type="search"
            />
            <button className="button" type="submit">Rechercher</button>
          </div>
        </form>
      </header>

      {categories.length > 0 ? (
        <section className="wiki-section" aria-labelledby="wiki-categories">
          <h2 id="wiki-categories">Catégories</h2>
          <div className="wiki-card-grid">
            {categories.map((category) => (
              <Link
                className="wiki-card"
                href={`/wiki/categorie/${category.slug}`}
                key={category.id}
              >
                <strong>{category.name}</strong>
                {category.description ? <span>{category.description}</span> : null}
              </Link>
            ))}
          </div>
        </section>
      ) : null}

      <section className="wiki-section" aria-labelledby="wiki-recent">
        <h2 id="wiki-recent">
          {query.length >= 2 ? "Résultats" : "Articles récemment mis à jour"}
        </h2>
        {articles.length > 0 ? (
          <div className="wiki-list">
            {articles.map((article) => (
              <Link
                className="wiki-list-item"
                href={`/wiki/article/${article.slug}`}
                key={article.id}
              >
                <strong>{article.title}</strong>
                {article.summary ? <span>{article.summary}</span> : null}
              </Link>
            ))}
          </div>
        ) : (
          <p className="empty-copy">
            Aucun article publié n&apos;est disponible pour le moment.
          </p>
        )}
      </section>
    </div>
  );
}
