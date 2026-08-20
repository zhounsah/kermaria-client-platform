import type { Metadata } from "next";
import type { ReactNode } from "react";
import { Inter, JetBrains_Mono } from "next/font/google";

import { AppShell } from "@/components/AppShell";
import { isSignupEnabled } from "@/lib/public-routes";
import { PUBLIC_BRAND_NAME, PUBLIC_SITE_NAME } from "@/lib/public-metadata";
import { PUBLIC_SITE_URL } from "@/lib/public-route-config";
import "./globals.css";

const inter = Inter({
  display: "swap",
  subsets: ["latin"],
  variable: "--font-inter",
});

const jetbrainsMono = JetBrains_Mono({
  display: "swap",
  subsets: ["latin"],
  variable: "--font-jetbrains-mono",
});

const SITE_TITLE =
  "Sauvegarde distante et continuité d'activité à Guichen (35)";
const SITE_DESCRIPTION =
  "Sauvegarde distante, stockage documentaire et continuité d'activité à Guichen pour particuliers, associations et petites entreprises.";

export async function generateMetadata(): Promise<Metadata> {
  return {
    metadataBase: new URL(PUBLIC_SITE_URL),
    title: {
      default: `${SITE_TITLE} | ${PUBLIC_BRAND_NAME}`,
      template: `%s | ${PUBLIC_BRAND_NAME}`,
    },
    description: SITE_DESCRIPTION,
    openGraph: {
      type: "website",
      locale: "fr_FR",
      siteName: PUBLIC_SITE_NAME,
      title: SITE_TITLE,
      description: SITE_DESCRIPTION,
      url: PUBLIC_SITE_URL,
    },
    // `summary` sans image n'a aucun interet : l'`opengraph-image` de la
    // racine fournit desormais le visuel, dont `twitter:image` herite.
    twitter: { card: "summary_large_image" },
    icons: {
      apple: [
        {
          sizes: "180x180",
          type: "image/png",
          url: "/brand/favicon/apple-touch-icon.png",
        },
      ],
      icon: [
        { url: "/favicon.ico", sizes: "any" },
        {
          sizes: "16x16",
          type: "image/png",
          url: "/brand/favicon/favicon-16.png",
        },
        {
          sizes: "32x32",
          type: "image/png",
          url: "/brand/favicon/favicon-32.png",
        },
        {
          sizes: "48x48",
          type: "image/png",
          url: "/brand/favicon/favicon-48.png",
        },
      ],
    },
    manifest: "/brand/favicon/site.webmanifest",
  };
}

export default async function RootLayout({
  children,
}: Readonly<{ children: ReactNode }>) {
  const signupEnabled = isSignupEnabled();

  return (
    <html lang="fr">
      <body className={`${inter.variable} ${jetbrainsMono.variable}`}>
        <AppShell signupEnabled={signupEnabled}>
          {children}
        </AppShell>
      </body>
    </html>
  );
}
