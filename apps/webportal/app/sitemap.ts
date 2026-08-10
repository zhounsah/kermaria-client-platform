import type { MetadataRoute } from "next";
import { headers } from "next/headers";
import { connection } from "next/server";
import {
  buildPackSheetContentKey,
  type ManagedContentKey,
  PUBLIC_PACKS,
} from "@kermaria/shared";

import {
  getPublicEditorialSitemap,
  getPublicManagedContent,
} from "@/lib/internal-api";
import { WIKI_PUBLIC_HOST } from "@/lib/public-route-config";
import {
  getPortalPublicUrlFromHeaders,
  isVitrinePublicEnabled,
} from "@/lib/public-routes";

type PublicRouteEntry = {
  path: string;
  changeFrequency: NonNullable<MetadataRoute.Sitemap[number]["changeFrequency"]>;
  priority: number;
  /**
   * Contenu administrable dont `updatedAt` est la seule date de
   * modification fiable. Une page sans cle ne publie aucun `lastmod` :
   * mieux vaut l'omettre que le recalculer a l'heure de la requete, ce qui
   * annonce a tort tout le site comme modifie a chaque passage du robot.
   */
  contentKey?: ManagedContentKey;
};

const PUBLIC_ROUTE_ENTRIES: PublicRouteEntry[] = [
  { path: "/", changeFrequency: "monthly", priority: 1 },
  { path: "/offres", changeFrequency: "weekly", priority: 0.9 },
  { path: "/diagnostic", changeFrequency: "monthly", priority: 0.8 },
  { path: "/decouvrir-espace-client", changeFrequency: "monthly", priority: 0.75 },
  { path: "/ressources", changeFrequency: "weekly", priority: 0.75 },
  // `/solutions` est volontairement absente : portail d'acces client et non
  // page vitrine, elle est retiree de l'index par ses metadonnees `robots`
  // (`app/solutions/page.tsx`). Un sitemap qui declare une URL non
  // indexable envoie deux signaux contraires.
  {
    path: "/a-propos",
    changeFrequency: "monthly",
    priority: 0.7,
    contentKey: "page:a-propos",
  },
  { path: "/contact", changeFrequency: "monthly", priority: 0.7 },
  {
    path: "/mentions-legales",
    changeFrequency: "yearly",
    priority: 0.3,
    contentKey: "legal:mentions-legales",
  },
  {
    path: "/politique-confidentialite",
    changeFrequency: "yearly",
    priority: 0.3,
    contentKey: "legal:politique-confidentialite",
  },
  {
    path: "/cgv",
    changeFrequency: "yearly",
    priority: 0.3,
    contentKey: "legal:cgv",
  },
];

async function resolveLastModified(
  contentKey: ManagedContentKey | undefined,
): Promise<Date | null> {
  if (!contentKey) {
    return null;
  }

  const result = await getPublicManagedContent(contentKey);
  const updatedAt = result.data?.updatedAt;
  if (!updatedAt) {
    return null;
  }

  const lastModified = new Date(updatedAt);
  return Number.isNaN(lastModified.getTime()) ? null : lastModified;
}

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  await connection();

  if (!isVitrinePublicEnabled()) {
    return [];
  }

  const baseUrl = getPortalPublicUrlFromHeaders(await headers());
  const editorialEntries = await getPublicEditorialSitemap();
  const packEntries: PublicRouteEntry[] = PUBLIC_PACKS.map((pack) => ({
    path: `/offres/${pack.slug}`,
    changeFrequency: "weekly" as const,
    priority: 0.7,
    contentKey: buildPackSheetContentKey(pack.key),
  }));

  const staticEntries = await Promise.all(
    [...PUBLIC_ROUTE_ENTRIES, ...packEntries].map(
      async ({ path, changeFrequency, priority, contentKey }) => {
        const lastModified = await resolveLastModified(contentKey);

        return {
          // `new URL` et non une concatenation : l'accueil sortait
          // `https://www.zacharyhounsa.ovh` sans slash final la ou la
          // canonical de la page vaut `…/`. Deux chaines pour une meme
          // page, donc deux URL du point de vue de Google.
          url: new URL(path, baseUrl).toString(),
          ...(lastModified ? { lastModified } : {}),
          changeFrequency,
          priority,
        };
      },
    ),
  );

  return [
    ...staticEntries,
    ...editorialEntries.data
      .filter((entry) => entry.publicPath)
      .map((entry) => {
        const url = entry.contentType === "wiki_article"
          ? new URL(entry.publicPath!, `https://${WIKI_PUBLIC_HOST}`).toString()
          : new URL(entry.publicPath!, baseUrl).toString();
        const updatedAt = new Date(entry.updatedAt);
        return {
          url,
          ...(Number.isNaN(updatedAt.getTime())
            ? {}
            : { lastModified: updatedAt }),
          changeFrequency: "weekly" as const,
          priority: entry.contentType === "wiki_article" ? 0.6 : 0.7,
        };
      }),
  ];
}
