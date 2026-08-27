import type { Metadata } from "next";
import { notFound, permanentRedirect } from "next/navigation";
import { connection } from "next/server";

import { ManagedMarkdown } from "@/components/ManagedMarkdown";
import {
  getEditorialRedirect,
  getPublicSeoPage,
} from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";

type EditorialSeoPageProps = {
  params: Promise<{ slug: string }>;
};

export const dynamic = "force-dynamic";

export async function generateMetadata({
  params,
}: EditorialSeoPageProps): Promise<Metadata> {
  const { slug } = await params;
  const path = `/${slug}`;
  const redirectResult = await getEditorialRedirect(path);
  if (redirectResult.data?.newPath && redirectResult.data.newPath !== path) {
    return {
      title: "Redirection",
      robots: { index: false, follow: true },
    };
  }

  const result = await getPublicSeoPage(slug);
  if (!result.data) {
    notFound();
  }

  const page = result.data;
  return {
    ...buildPublicMetadata({
      title: page.seoTitle ?? page.title,
      description: page.seoDescription ?? page.summary ?? undefined,
      path: page.canonicalUrl ?? `/${page.slug}`,
      type: "article",
    }),
    robots: { index: !page.noIndex, follow: true },
  };
}

export default async function EditorialSeoPage({
  params,
}: EditorialSeoPageProps) {
  const { slug } = await params;
  const path = `/${slug}`;
  const redirectResult = await getEditorialRedirect(path);
  if (redirectResult.data?.newPath && redirectResult.data.newPath !== path) {
    permanentRedirect(redirectResult.data.newPath);
  }

  const result = await getPublicSeoPage(slug);
  if (!result.data) {
    notFound();
  }

  await connection();

  const page = result.data;
  return (
    <article className="seo-editorial-page">
      <header className="seo-editorial-header">
        {page.categoryName ? <p className="eyebrow">{page.categoryName}</p> : null}
        <h1>{page.title}</h1>
        {page.summary ? (
          <p className="seo-editorial-summary">{page.summary}</p>
        ) : null}
      </header>
      <ManagedMarkdown markdown={page.bodyMarkdown} withAnchors />
    </article>
  );
}
