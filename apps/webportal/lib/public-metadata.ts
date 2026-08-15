import type { Metadata } from "next";

import { BRAND_NAME } from "@/lib/brand-identity";
import { PUBLIC_SITE_URL } from "@/lib/public-route-config";

// Le suffixe des titres et le `og:site_name` portent le nom commercial, pas
// la denomination juridique : c'est le nom du site que Google doit retenir.
export const PUBLIC_BRAND_NAME = BRAND_NAME;
export const PUBLIC_SITE_NAME = BRAND_NAME;

type PublicMetadataOptions = {
  title: string;
  description?: string;
  path: string;
  robots?: Metadata["robots"];
  type?: "article" | "website";
};

export function buildPublicMetadata({
  title,
  description,
  path,
  robots,
  type = "website",
}: PublicMetadataOptions): Metadata {
  return {
    title,
    ...(description ? { description } : {}),
    alternates: { canonical: path },
    openGraph: {
      title,
      ...(description ? { description } : {}),
      url: path,
      type,
      siteName: PUBLIC_SITE_NAME,
      locale: "fr_FR",
    },
    twitter: {
      card: "summary_large_image",
      title,
      ...(description ? { description } : {}),
    },
    ...(robots ? { robots } : {}),
  };
}

export function absolutePublicUrl(path: string): string {
  return new URL(path, PUBLIC_SITE_URL).toString();
}
