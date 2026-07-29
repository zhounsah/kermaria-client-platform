import type { Metadata } from "next";
import type { ReactNode } from "react";
import { headers } from "next/headers";

import { AppShell } from "@/components/AppShell";
import { getCurrentPortalSession } from "@/lib/auth";
import {
  getPortalPublicUrlFromHeaders,
  isSignupEnabled,
} from "@/lib/public-routes";
import "./globals.css";

const SITE_TITLE =
  "Zachary HOUNSA-HOUNKPA EI - Solutions informatiques et espace client";
const SITE_DESCRIPTION =
  "Solutions informatiques claires pour petites structures, associations et professionnels : offres, accompagnement, espace client et demandes d'assistance.";

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
    twitter: {
      card: "summary_large_image",
      title: SITE_TITLE,
      description: SITE_DESCRIPTION,
    },
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
