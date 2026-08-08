import type { Metadata } from "next";
import { headers } from "next/headers";

import { DemoClientSpace } from "@/components/DemoClientSpace";
import { sectionFromDemoRouteSlug } from "@/lib/demo-client-space/data";
import { breadcrumbJsonLd, JsonLd } from "@/lib/seo";
import { getPortalPublicUrlFromHeaders } from "@/lib/public-routes";

export const dynamic = "force-static";

export const metadata: Metadata = {
  title: "Découvrez l'espace client Zachary IT",
  description:
    "Découvrez en démonstration l'espace client Zachary IT : sauvegardes, stockage, utilisateurs, facturation, assistance et sécurité.",
  alternates: {
    canonical: "/decouvrir-espace-client",
  },
  openGraph: {
    title: "Découvrez l'espace client Zachary IT",
    description:
      "Parcourez un compte client fictif Pack Pro / Association, sans authentification ni données réelles.",
    url: "/decouvrir-espace-client",
    type: "website",
  },
};

type PageProps = {
  params: Promise<{ section?: string[] }>;
};

export default async function DemoClientSpacePage({ params }: PageProps) {
  const { section } = await params;
  const currentSection = sectionFromDemoRouteSlug(section?.[0]);
  const baseUrl = getPortalPublicUrlFromHeaders(await headers());

  return (
    <>
      <JsonLd
        data={breadcrumbJsonLd(baseUrl, [
          {
            name: "Découvrir l'espace client",
            path: "/decouvrir-espace-client",
          },
        ])}
      />
      <DemoClientSpace section={currentSection} />
    </>
  );
}
