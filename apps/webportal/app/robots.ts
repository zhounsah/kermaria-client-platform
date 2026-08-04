import type { MetadataRoute } from "next";
import { headers } from "next/headers";
import { connection } from "next/server";

import {
  getPortalPublicUrlFromHeaders,
  isVitrinePublicEnabled,
} from "@/lib/public-routes";

export default async function robots(): Promise<MetadataRoute.Robots> {
  await connection();
  const baseUrl = getPortalPublicUrlFromHeaders(await headers());

  if (!isVitrinePublicEnabled()) {
    return {
      rules: {
        userAgent: "*",
        disallow: "/",
      },
    };
  }

  return {
    rules: {
      userAgent: "*",
      allow: "/",
      // Doit rester aligne avec NOINDEX_ROUTE_PREFIXES (next.config.ts).
      disallow: [
        "/access-denied",
        "/admin",
        "/api",
        "/commercial-documents",
        "/dashboard",
        "/downloads",
        "/invoices",
        "/login",
        "/notifications",
        "/panier",
        "/password",
        "/profile",
        "/request-service",
        "/services",
        "/set-password",
        "/signup/verify",
        "/souscrire",
        "/support",
      ],
    },
    host: new URL(baseUrl).host,
    sitemap: `${baseUrl}/sitemap.xml`,
  };
}
