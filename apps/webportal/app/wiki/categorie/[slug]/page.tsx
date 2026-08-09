import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { connection } from "next/server";

import { getPublicWikiHome } from "@/lib/internal-api";
import { getWikiRobots, wikiCanonical } from "@/lib/wiki-seo";

type WikiCategoryPageProps = {
  params: Promise<{ slug: string }>;
};

export const dynamic = "force-dynamic";

export async function generateMetadata({
  params,
}: WikiCategoryPageProps): Promise<Metadata> {
  const { slug } = await params;
  const { robots } = await getWikiRobots();
  return {
    title: "Catégorie Wiki",
    alternates: { canonical: wikiCanonical(`/categorie/${slug}`) },
    robots,
  };
}

export default async function WikiCategoryPage({
  params,
}: WikiCategoryPageProps) {
  await connection();
  const { slug } = await params;
  const result = await getPublicWikiHome();
  const category = result.data.categories.find((item) => item.slug === slug);
  if (!category) {
    notFound();
  }

  const articles = result.data.items.filter(
    (item) => item.categoryId === category.id,
  );

  return (
    <div className="wiki-page">
      <header className="wiki-header">
        <Link className="text-link" href="/wiki">Retour au wiki</Link>
        <p className="eyebrow">Catégorie</p>
        <h1>{category.name}</h1>
        {category.description ? <p>{category.description}</p> : null}
      </header>

      <section className="wiki-section" aria-labelledby="category-articles">
        <h2 id="category-articles">Articles publiés</h2>
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
            Aucun article publié dans cette catégorie pour le moment.
          </p>
        )}
      </section>
    </div>
  );
}
