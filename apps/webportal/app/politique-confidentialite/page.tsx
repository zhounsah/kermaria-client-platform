import type { Metadata } from "next";

import { ErrorState } from "@/components/ErrorState";
import { PublicManagedContentArticle } from "@/components/PublicManagedContentArticle";
import { getPublicManagedContent } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";

export const metadata: Metadata = buildPublicMetadata({
  title: "Politique de confidentialité",
  description:
    "Politique de confidentialité et utilisation des cookies sur l'espace client.",
  path: "/politique-confidentialite",
});

export const dynamic = "force-dynamic";

export default async function PolitiqueConfidentialitePage() {
  const result = await getPublicManagedContent("legal:politique-confidentialite");

  if (result.error || !result.data) {
    return (
      <ErrorState
        description="Impossible de charger la politique de confidentialité pour le moment."
        reference={result.correlationId}
        title="Politique de confidentialité indisponible"
      />
    );
  }

  return (
    <PublicManagedContentArticle
      content={result.data}
      correlationId={result.correlationId}
      eyebrow="Informations légales"
      source={result.source}
    />
  );
}
