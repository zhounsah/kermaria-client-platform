import type { Metadata } from "next";

import { ErrorState } from "@/components/ErrorState";
import { PublicManagedContentArticle } from "@/components/PublicManagedContentArticle";
import { getPublicManagedContent } from "@/lib/internal-api";

export const metadata: Metadata = {
  title: "À propos",
  description:
    "Présentation de Zachary HOUNSA-HOUNKPA EI et de ses domaines d'intervention.",
};

export const revalidate = 300;

export default async function AProposPage() {
  const result = await getPublicManagedContent("page:a-propos");

  if (result.error || !result.data) {
    return (
      <ErrorState
        description="Impossible de charger la page à propos pour le moment."
        reference={result.correlationId}
        title="Page à propos indisponible"
      />
    );
  }

  return (
    <PublicManagedContentArticle
      content={result.data}
      correlationId={result.correlationId}
      eyebrow="À propos"
      source={result.source}
    />
  );
}
