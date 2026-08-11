import type { Metadata } from "next";

import { PUBLIC_SITE_URL } from "@/lib/public-route-config";

export const PUBLIC_BRAND_NAME = "Zachary IT";
export const PUBLIC_SITE_NAME = "Zachary IT";

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
    ...(robots ? { robots } : {}),
  };
}

export function absolutePublicUrl(path: string): string {
  return new URL(path, PUBLIC_SITE_URL).toString();
}
