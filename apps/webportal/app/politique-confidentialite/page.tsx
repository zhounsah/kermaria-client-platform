import type { Metadata } from "next";

import { ErrorState } from "@/components/ErrorState";
import { PublicManagedContentArticle } from "@/components/PublicManagedContentArticle";
import { getPublicManagedContent } from "@/lib/internal-api";

export const metadata: Metadata = {
  title: "Politique de confidentialité",
  description:
    "Politique de confidentialité et utilisation des cookies sur l'espace client.",
  alternates: { canonical: "/politique-confidentialite" },
};

export const revalidate = 300;

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
