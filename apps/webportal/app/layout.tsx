import type { Metadata } from "next";
import type { ReactNode } from "react";
import { headers } from "next/headers";

import { AppShell } from "@/components/AppShell";
import {
  getPortalPublicUrlFromHeaders,
  isSignupEnabled,
} from "@/lib/public-routes";
import { getCurrentPortalSession } from "@/lib/auth";
import "./globals.css";

const SITE_TITLE = "Zachary HOUNSA-HOUNKPA EI - Espace client professionnel";
const SITE_DESCRIPTION =
  "Espace client professionnel de Zachary HOUNSA-HOUNKPA EI : catalogue d'offres, facturation, paiements et demandes d'assistance.";

/**
 * TODO (chantier ISR, hors passe SEO du 5 aout 2026) — `await headers()` ici
 * et `getCurrentPortalSession()` dans le composant sont des Dynamic APIs.
 * Appelees dans le layout RACINE, elles basculent l'arbre entier en rendu
 * par requete : les `export const revalidate = 300` de `/offres`,
 * `/a-propos`, `/cgv`, `/mentions-legales`, `/politique-confidentialite` et
 * `/offres/[slug]` n'ont aucun effet, et la production repond
 * `cache-control: private, no-cache, no-store` sur toutes les pages
 * publiques. Corriger suppose de sortir la session d'`AppShell` et de tenir
 * l'hote public autrement que par les en-tetes : ca touche l'architecture,
 * pas seulement les metadonnees.
 */
export async function generateMetadata(): Promise<Metadata> {
  const baseUrl = getPortalPublicUrlFromHeaders(await headers());

  return {
    metadataBase: new URL(baseUrl),
    title: {
      default: SITE_TITLE,
      template: "%s | Zachary HOUNSA-HOUNKPA EI",
    },
    description: SITE_DESCRIPTION,
    openGraph: {
      type: "website",
      locale: "fr_FR",
      siteName: "Zachary HOUNSA-HOUNKPA EI",
      title: SITE_TITLE,
      description: SITE_DESCRIPTION,
      url: baseUrl,
    },
    // `summary` sans image n'a aucun interet : l'`opengraph-image` de la
    // racine fournit desormais le visuel, dont `twitter:image` herite.
    twitter: { card: "summary_large_image" },
  };
}

export default async function RootLayout({
  children,
}: Readonly<{ children: ReactNode }>) {
  const session = await getCurrentPortalSession();
  const signupEnabled = isSignupEnabled();

  return (
    <html lang="fr">
      <body>
        <AppShell session={session} signupEnabled={signupEnabled}>
          {children}
        </AppShell>
      </body>
    </html>
  );
}
