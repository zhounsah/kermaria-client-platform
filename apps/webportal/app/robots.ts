import type { MetadataRoute } from "next";
import { headers } from "next/headers";
import { connection } from "next/server";

import {
  getWikiHostKind,
  resolveCanonicalPublicUrl,
  WIKI_PUBLIC_HOST,
} from "@/lib/public-route-config";
import {
  getPortalPublicUrlFromHeaders,
  isVitrinePublicEnabled,
} from "@/lib/public-routes";

/**
 * URL du sitemap sur l'hote canonique : servi depuis un alias (apex sans
 * `www`), `robots.txt` ne doit pas renvoyer vers une URL qui repond 301.
 */
function resolveSitemapUrl(baseUrl: string): string {
  const { host } = new URL(baseUrl);
  return (
    resolveCanonicalPublicUrl(host, "/sitemap.xml") ?? `${baseUrl}/sitemap.xml`
  );
}

export default async function robots(): Promise<MetadataRoute.Robots> {
  await connection();
  const headerList = await headers();
  const host = headerList.get("x-forwarded-host") ?? headerList.get("host");
  const wikiHostKind = getWikiHostKind(host);
  if (wikiHostKind) {
    return {
      rules: {
        userAgent: "*",
        allow: "/",
      },
      sitemap: `https://${WIKI_PUBLIC_HOST}/sitemap.xml`,
    };
  }

  const baseUrl = getPortalPublicUrlFromHeaders(headerList);

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
        "/backups",
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
    // Pas de directive `Host` : non standard, ignoree par les robots et
    // signalee en erreur. La canonicalisation passe par le 301 apex ->
    // `www` (`proxy.ts`).
    sitemap: resolveSitemapUrl(baseUrl),
  };
}
