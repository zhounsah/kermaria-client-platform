import type { Metadata } from "next";

import { ErrorState } from "@/components/ErrorState";
import { PublicManagedContentArticle } from "@/components/PublicManagedContentArticle";
import { getPublicManagedContent } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";
import { PUBLIC_SITE_URL } from "@/lib/public-route-config";
import { breadcrumbJsonLd, JsonLd } from "@/lib/seo";

export const metadata: Metadata = buildPublicMetadata({
  title: "Infrastructure, hébergement et exploitation",
  description:
    "Découvrez les principes d'exploitation de Zachary IT : hébergement, fournisseurs, sauvegarde, supervision, sécurité, localisation et disponibilité.",
  path: "/infrastructure",
});

export const dynamic = "force-dynamic";

export default async function InfrastructurePage() {
  const result = await getPublicManagedContent("page:infrastructure");

  if (result.error || !result.data) {
    return (
      <ErrorState
        description="Impossible de charger la page infrastructure pour le moment."
        reference={result.correlationId}
        title="Page infrastructure indisponible"
      />
    );
  }

  return (
    <>
      <JsonLd
        data={breadcrumbJsonLd(PUBLIC_SITE_URL, [
          { name: "Infrastructure", path: "/infrastructure" },
        ])}
      />
      <PublicManagedContentArticle
        content={result.data}
        correlationId={result.correlationId}
        eyebrow="Infrastructure"
        source={result.source}
      />
    </>
  );
}
