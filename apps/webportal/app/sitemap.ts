import type { MetadataRoute } from "next";

import {
  getPortalPublicUrl,
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

export default function sitemap(): MetadataRoute.Sitemap {
  if (!isVitrinePublicEnabled()) {
    return [];
  }

  const baseUrl = getPortalPublicUrl();
  const now = new Date();

  return PUBLIC_ROUTE_ENTRIES.map(({ path, changeFrequency, priority }) => ({
    url: `${baseUrl}${path === "/" ? "" : path}`,
    lastModified: now,
    changeFrequency,
    priority,
  }));
}
