import type { Metadata } from "next";
import type { ReactNode } from "react";

import { AppShell } from "@/components/AppShell";
import "./globals.css";

export const metadata: Metadata = {
  title: {
    default: "Espace client - Zachary HOUNSA-HOUNKPA EI",
    template: "%s | Zachary HOUNSA-HOUNKPA EI",
  },
  description:
    "Espace client sécurisé de Zachary HOUNSA-HOUNKPA EI.",
};

export default function RootLayout({
  children,
}: Readonly<{ children: ReactNode }>) {
  return (
    <html lang="fr">
      <body>
        <AppShell>{children}</AppShell>
      </body>
    </html>
  );
}
