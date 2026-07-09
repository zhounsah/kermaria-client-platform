import type { MetadataRoute } from "next";
import { headers } from "next/headers";
import { connection } from "next/server";
import { PUBLIC_PACKS } from "@kermaria/shared";

import {
  getPortalPublicUrlFromHeaders,
  isVitrinePublicEnabled,
} from "@/lib/public-routes";

type PublicRouteEntry = {
  path: string;
  changeFrequency: NonNullable<MetadataRoute.Sitemap[number]["changeFrequency"]>;
  priority: number;
};

const PUBLIC_ROUTE_ENTRIES: PublicRouteEntry[] = [
  { path: "/", changeFrequency: "monthly", priority: 1 },
  { path: "/offres", changeFrequency: "weekly", priority: 0.9 },
  { path: "/a-propos", changeFrequency: "monthly", priority: 0.7 },
  { path: "/contact", changeFrequency: "monthly", priority: 0.7 },
  { path: "/mentions-legales", changeFrequency: "yearly", priority: 0.3 },
  {
    path: "/politique-confidentialite",
    changeFrequency: "yearly",
    priority: 0.3,
  },
  { path: "/cgv", changeFrequency: "yearly", priority: 0.3 },
];

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  await connection();

  if (!isVitrinePublicEnabled()) {
    return [];
  }

  const baseUrl = getPortalPublicUrlFromHeaders(await headers());
  const now = new Date();
  const packEntries = PUBLIC_PACKS.map((pack) => ({
    path: `/offres/${pack.slug}`,
    changeFrequency: "weekly" as const,
    priority: 0.7,
  }));

  return [...PUBLIC_ROUTE_ENTRIES, ...packEntries].map(
    ({ path, changeFrequency, priority }) => ({
      url: `${baseUrl}${path === "/" ? "" : path}`,
      lastModified: now,
      changeFrequency,
      priority,
    }),
  );
}
