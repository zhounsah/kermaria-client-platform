import type { Metadata } from "next";

import { ErrorState } from "@/components/ErrorState";
import { PublicManagedContentArticle } from "@/components/PublicManagedContentArticle";
import { getPublicManagedContent } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";

export const metadata: Metadata = buildPublicMetadata({
  title: "Mentions légales",
  description: "Mentions légales de Zachary HOUNSA-HOUNKPA EI.",
  path: "/mentions-legales",
});

export const dynamic = "force-dynamic";

export default async function MentionsLegalesPage() {
  const result = await getPublicManagedContent("legal:mentions-legales");

  if (result.error || !result.data) {
    return (
      <ErrorState
        description="Impossible de charger les mentions légales pour le moment."
        reference={result.correlationId}
        title="Mentions légales indisponibles"
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
