import type { Metadata } from "next";
import Link from "next/link";

import { requireAdminSession } from "@/lib/auth";
import { getAdminEditorialList } from "@/lib/internal-api";

export const metadata: Metadata = {
  title: "Éditorial - Administration",
};

const sections = [
  {
    contentType: "wiki_article",
    href: "/admin/editorial/wiki",
    title: "Wiki",
    text: "Articles du centre d'aide.",
  },
  {
    contentType: "seo_page",
    href: "/admin/editorial/seo",
    title: "Pages SEO",
    text: "Pages publiques dynamiques.",
  },
  {
    contentType: "faq",
    href: "/admin/editorial/faq",
    title: "FAQ",
    text: "Questions réutilisables par contexte.",
  },
] as const;

export default async function AdminEditorialPage() {
  await requireAdminSession();
  const [wiki, seo, faq] = await Promise.all(
    sections.map((section) =>
      getAdminEditorialList(`contentType=${section.contentType}`),
    ),
  );
  const stats = [wiki.data, seo.data, faq.data];

  return (
    <div className="stack-panels">
      <div className="page-heading">
        <div>
          <span className="card-kicker">Back-office</span>
          <h1>Éditorial</h1>
          <p>Gérez les contenus publiables sans modifier le code.</p>
        </div>
      </div>
      <div className="wiki-card-grid">
        {sections.map((section, index) => (
          <Link className="wiki-card" href={section.href} key={section.href}>
            <strong>{section.title}</strong>
            <span>{section.text}</span>
            <small>{dashboardSummary(section.contentType, stats[index])}</small>
          </Link>
        ))}
      </div>
    </div>
  );
}

function dashboardSummary(
  contentType: (typeof sections)[number]["contentType"],
  data: Awaited<ReturnType<typeof getAdminEditorialList>>["data"],
) {
  const published = data.items.filter((item) => item.status === "published").length;
  const drafts = data.items.filter((item) => item.status === "draft").length;

  if (contentType === "seo_page") {
    const indexable = data.items.filter((item) => !item.noIndex).length;
    return `${data.items.length} pages · ${indexable} indexables · ${drafts} brouillons`;
  }

  if (contentType === "faq") {
    const scopes = new Set(data.items.flatMap((item) => item.faqScopes));
    return `${data.items.length} questions · ${published} publiées · ${scopes.size} scopes`;
  }

  return `${data.items.length} articles · ${published} publiés · ${drafts} brouillons`;
}
