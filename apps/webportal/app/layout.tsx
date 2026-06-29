import type { Metadata } from "next";
import type { ReactNode } from "react";

import { AppShell } from "@/components/AppShell";
import { PublicShell } from "@/components/PublicShell";
import {
  getCurrentPathname,
  getPortalPublicUrl,
  isPublicRoute,
} from "@/lib/public-routes";
import "./globals.css";

const SITE_TITLE = "Zachary HOUNSA-HOUNKPA EI - Espace client professionnel";
const SITE_DESCRIPTION =
  "Espace client professionnel de Zachary HOUNSA-HOUNKPA EI : catalogue d'offres, facturation, paiements et demandes d'assistance.";

export const metadata: Metadata = {
  metadataBase: new URL(getPortalPublicUrl()),
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
  },
};

export default async function RootLayout({
  children,
}: Readonly<{ children: ReactNode }>) {
  const pathname = await getCurrentPathname();
  const usePublicShell = isPublicRoute(pathname);

  return (
    <html lang="fr">
      <body>
        {usePublicShell ? (
          <PublicShell>{children}</PublicShell>
        ) : (
          <AppShell>{children}</AppShell>
        )}
      </body>
    </html>
  );
}
