import type { Metadata } from "next";
import { notFound, redirect } from "next/navigation";
import { connection } from "next/server";

import { ManagedMarkdown } from "@/components/ManagedMarkdown";
import {
  getEditorialRedirect,
  getPublicSeoPage,
} from "@/lib/internal-api";

type EditorialSeoPageProps = {
  params: Promise<{ slug: string }>;
};

export const dynamic = "force-dynamic";

export async function generateMetadata({
  params,
}: EditorialSeoPageProps): Promise<Metadata> {
  const { slug } = await params;
  const result = await getPublicSeoPage(slug);
  if (!result.data) {
    return {
      title: "Page introuvable",
      robots: { index: false, follow: false },
    };
  }

  const page = result.data;
  return {
    title: page.seoTitle ?? page.title,
    description: page.seoDescription ?? page.summary ?? undefined,
    alternates: { canonical: page.canonicalUrl ?? `/${page.slug}` },
    openGraph: {
      title: page.seoTitle ?? page.title,
      description: page.seoDescription ?? page.summary ?? undefined,
      type: "article",
      url: page.canonicalUrl ?? `/${page.slug}`,
    },
    robots: { index: !page.noIndex, follow: true },
  };
}

export default async function EditorialSeoPage({
  params,
}: EditorialSeoPageProps) {
  await connection();
  const { slug } = await params;
  const path = `/${slug}`;
  const redirectResult = await getEditorialRedirect(path);
  if (redirectResult.data?.newPath && redirectResult.data.newPath !== path) {
    redirect(redirectResult.data.newPath);
  }

  const result = await getPublicSeoPage(slug);
  if (!result.data) {
    notFound();
  }

  const page = result.data;
  return (
    <article className="seo-editorial-page">
      <header className="seo-editorial-header">
        {page.summary ? <p className="eyebrow">{page.summary}</p> : null}
        <h1>{page.title}</h1>
      </header>
      <ManagedMarkdown markdown={page.bodyMarkdown} withAnchors />
    </article>
  );
}
