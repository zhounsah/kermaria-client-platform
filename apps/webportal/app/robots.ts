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
      disallow: [
        "/admin",
        "/api",
        "/dashboard",
        "/invoices",
        "/notifications",
        "/password",
        "/profile",
        "/services",
        "/support",
        "/commercial-documents",
        "/request-service",
        "/login",
        "/signup/verify",
        "/access-denied",
      ],
    },
    host: new URL(baseUrl).host,
    sitemap: `${baseUrl}/sitemap.xml`,
  };
}
