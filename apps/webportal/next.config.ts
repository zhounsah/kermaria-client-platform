import type { NextConfig } from "next";
import path from "node:path";

/**
 * Prefixes servis en `noindex, nofollow` : espaces authentifies, API et
 * pages transactionnelles a jeton. Tout le reste (vitrine publique) doit
 * rester indexable — l'en-tete HTTP prime sur `robots.txt` et sur les
 * metadonnees `robots` des pages.
 *
 * Doit rester aligne avec la liste `disallow` de `app/robots.ts`
 * (garde-fou : `npm run test:seo`).
 */
export const NOINDEX_ROUTE_PREFIXES = [
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
  "/password",
  "/profile",
  "/request-service",
  "/set-password",
  "/signup/verify",
  "/souscrire",
  "/support",
] as const;

const SECURITY_HEADERS = [
  { key: "X-Content-Type-Options", value: "nosniff" },
  { key: "X-Frame-Options", value: "DENY" },
  {
    key: "Content-Security-Policy",
    value: "frame-ancestors 'none'; base-uri 'self'; form-action 'self'",
  },
  {
    key: "Referrer-Policy",
    value: "strict-origin-when-cross-origin",
  },
  {
    key: "Permissions-Policy",
    value: "camera=(), geolocation=(), microphone=()",
  },
  {
    key: "Cross-Origin-Opener-Policy",
    value: "same-origin",
  },
  {
    key: "Cross-Origin-Resource-Policy",
    value: "same-site",
  },
];

const NOINDEX_HEADER = { key: "X-Robots-Tag", value: "noindex, nofollow" };

const nextConfig: NextConfig = {
  reactStrictMode: true,
  output: "standalone",
  transpilePackages: ["@kermaria/shared"],
  /**
   * Hotes autorises a charger les ressources de developpement (`_next/*`,
   * HMR). Sans effet hors `next dev`.
   *
   * Les deux plages LAN couvrent le poste de travail et les VM internes :
   * `192.168.0.0/24` (reseau bureau) et `192.168.100.0/24` (VM applicatives,
   * dont SRV-12). Next.js attend des motifs d'hote, pas des CIDR : `*` y
   * remplace la fin du nom, d'ou la forme `192.168.0.*`.
   */
  allowedDevOrigins: [
    "*.trycloudflare.com",
    "192.168.0.*",
    "192.168.100.*",
  ],
  async headers() {
    return [
      {
        source: "/:path*",
        headers: SECURITY_HEADERS,
      },
      // `/admin/:path*` couvre aussi bien `/admin` que ses sous-routes.
      ...NOINDEX_ROUTE_PREFIXES.map((prefix) => ({
        source: `${prefix}/:path*`,
        headers: [NOINDEX_HEADER],
      })),
    ];
  },
  turbopack: {
    root: path.resolve(process.cwd(), "../.."),
  },
};

export default nextConfig;
