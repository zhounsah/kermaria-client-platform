import type { Metadata } from "next";

import { ErrorState } from "@/components/ErrorState";
import { PublicManagedContentArticle } from "@/components/PublicManagedContentArticle";
import { getPublicManagedContent } from "@/lib/internal-api";

export const metadata: Metadata = {
  title: "Conditions générales de vente",
  description:
    "Conditions générales de vente applicables aux prestations de Zachary HOUNSA-HOUNKPA EI.",
};

export const revalidate = 300;

export default async function CgvPage() {
  const result = await getPublicManagedContent("legal:cgv");

  if (result.error || !result.data) {
    return (
      <ErrorState
        description="Impossible de charger les conditions générales de vente pour le moment."
        reference={result.correlationId}
        title="CGV indisponibles"
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
