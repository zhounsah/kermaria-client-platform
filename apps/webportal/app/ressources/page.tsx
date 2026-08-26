import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";

import { ErrorState } from "@/components/ErrorState";
import { ServiceCTA } from "@/components/PublicServiceComponents";
import { getPublicEditorialSitemap } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";

export const dynamic = "force-dynamic";

export const metadata: Metadata = buildPublicMetadata({
  title: "Guides et conseils informatiques",
  description:
    "Guides pratiques Zachary IT sur la messagerie, les sauvegardes, le réseau, la sécurité, l’accès distant et l’hébergement pour petites structures.",
  path: "/ressources",
});

const UNCATEGORIZED_LABEL = "Autres ressources";

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
    .sort(
      (first, second) =>
        first.sortOrder - second.sortOrder
        || first.title.localeCompare(second.title, "fr"),
    );

  type ResourcePage = (typeof pages)[number];
  type ResourceGroup = {
    name: string;
    sortOrder: number;
    items: ResourcePage[];
  };
  const groupedPages = new Map<string, ResourceGroup>();

  for (const page of pages) {
    const categoryName = page.categoryName?.trim() || UNCATEGORIZED_LABEL;
    const categoryKey = page.categoryId ?? UNCATEGORIZED_LABEL;
    const categorySortOrder = page.categorySortOrder ?? Number.MAX_SAFE_INTEGER;
    const group = groupedPages.get(categoryKey) ?? {
      name: categoryName,
      sortOrder: categorySortOrder,
      items: [],
    };
    group.items.push(page);
    groupedPages.set(categoryKey, group);
  }

  const resourceGroups = Array.from(groupedPages.values()).sort(
    (first, second) =>
      first.sortOrder - second.sortOrder
      || first.name.localeCompare(second.name, "fr"),
  );

  return (
    <div className="seo-hub-page">
      <header className="seo-hub-header">
        <p className="eyebrow">Ressources</p>
        <h1>Guides et conseils informatiques</h1>
        <p>
          Des repères pratiques pour comprendre les problèmes informatiques
          courants, vérifier les points importants et choisir une solution
          adaptée à votre situation.
        </p>
      </header>

      {resourceGroups.length > 0 ? (
        <section aria-labelledby="ressources-list-title" className="wiki-section">
          <div>
            <h2 id="ressources-list-title">Guides par thème</h2>
            <p className="empty-copy">
              Messagerie, sauvegarde, réseau, sécurité, accès distant ou
              hébergement : parcourez les ressources selon votre besoin.
            </p>
          </div>

          {resourceGroups.map((group, index) => {
            const headingId = `ressources-theme-${index + 1}`;

            return (
              <section
                aria-labelledby={headingId}
                className="wiki-section"
                key={group.name}
              >
                <h3 id={headingId}>{group.name}</h3>
                <div className="seo-hub-list">
                  {group.items.map((page) => (
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
            );
          })}
        </section>
      ) : (
        <section aria-labelledby="ressources-list-title" className="wiki-section">
          <h2 id="ressources-list-title">Guides en préparation</h2>
          <p className="empty-copy">
            Les premiers guides pratiques seront publiés ici. Ils couvriront
            progressivement la messagerie, les sauvegardes, le réseau, la
            sécurité, l’accès distant et l’hébergement.
          </p>
        </section>
      )}

      <ServiceCTA
        action={{ href: "/contact", label: "Parler de votre besoin" }}
        description="Les guides donnent des repères généraux. Pour un besoin précis, Zachary IT peut examiner votre environnement et proposer une solution adaptée."
        title="Vous ne trouvez pas votre cas ?"
      />
    </div>
  );
}
