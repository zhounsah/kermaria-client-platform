import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { connection } from "next/server";

import { ManagedMarkdown } from "@/components/ManagedMarkdown";
import { getPublicWikiArticle } from "@/lib/internal-api";
import { extractMarkdownToc } from "@/lib/markdown-toc";
import { getWikiRobots, wikiCanonical } from "@/lib/wiki-seo";

type WikiArticlePageProps = {
  params: Promise<{ slug: string }>;
};

export const dynamic = "force-dynamic";

export async function generateMetadata({
  params,
}: WikiArticlePageProps): Promise<Metadata> {
  const { slug } = await params;
  const { robots } = await getWikiRobots();
  const result = await getPublicWikiArticle(slug);
  if (!result.data) {
    return {
      title: "Article introuvable",
      robots: { index: false, follow: false },
    };
  }

  return {
    title: result.data.seoTitle ?? result.data.title,
    description: result.data.seoDescription ?? result.data.summary ?? undefined,
    alternates: { canonical: wikiCanonical(`/article/${result.data.slug}`) },
    openGraph: {
      title: result.data.seoTitle ?? result.data.title,
      description: result.data.seoDescription ?? result.data.summary ?? undefined,
      type: "article",
      url: wikiCanonical(`/article/${result.data.slug}`),
    },
    robots,
  };
}

export default async function WikiArticlePage({
  params,
}: WikiArticlePageProps) {
  await connection();
  const { slug } = await params;
  const result = await getPublicWikiArticle(slug);
  if (!result.data) {
    notFound();
  }

  const article = result.data;
  const toc = extractMarkdownToc(article.bodyMarkdown);

  return (
    <article className="wiki-article">
      <header className="wiki-header">
        <Link className="text-link" href="/wiki">Retour au wiki</Link>
        {article.categoryName ? (
          <p className="eyebrow">{article.categoryName}</p>
        ) : null}
        <h1>{article.title}</h1>
        {article.summary ? <p>{article.summary}</p> : null}
        <p className="managed-content-updated">
          Dernière mise à jour :{" "}
          {new Intl.DateTimeFormat("fr-FR", { dateStyle: "long" }).format(
            new Date(article.updatedAt),
          )}
        </p>
      </header>

      <div className="wiki-article-layout">
        {toc.length > 0 ? (
          <nav className="wiki-toc" aria-label="Sommaire">
            <strong>Sommaire</strong>
            {toc.map((heading) => (
              <a
                className={heading.level === 3 ? "wiki-toc-child" : undefined}
                href={`#${heading.id}`}
                key={`${heading.id}-${heading.title}`}
              >
                {heading.title}
              </a>
            ))}
          </nav>
        ) : null}
        <ManagedMarkdown markdown={article.bodyMarkdown} withAnchors />
      </div>
    </article>
  );
}
