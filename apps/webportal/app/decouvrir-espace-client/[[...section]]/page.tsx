import type { Metadata } from "next";
import { headers } from "next/headers";

import { DemoClientSpace } from "@/components/DemoClientSpace";
import { sectionFromDemoRouteSlug } from "@/lib/demo-client-space/data";
import { breadcrumbJsonLd, JsonLd } from "@/lib/seo";
import { getPortalPublicUrlFromHeaders } from "@/lib/public-routes";
import { buildPublicMetadata } from "@/lib/public-metadata";

export const dynamic = "force-static";

export const metadata: Metadata = buildPublicMetadata({
  title: "Découvrez l'espace client",
  description:
    "Découvrez en démonstration l'espace client Zachary IT : sauvegardes, stockage, utilisateurs, facturation, assistance et sécurité.",
  path: "/decouvrir-espace-client",
});

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
