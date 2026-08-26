import type { Metadata } from "next";

import { ErrorState } from "@/components/ErrorState";
import { PublicManagedContentArticle } from "@/components/PublicManagedContentArticle";
import { getPublicManagedContent } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";

export const metadata: Metadata = buildPublicMetadata({
  title: "À propos de Zachary IT",
  description:
    "Découvrez Zachary IT, entreprise de services informatiques à Guichen : approche, accompagnement, transparence et personne derrière l’entreprise.",
  path: "/a-propos",
});

export const dynamic = "force-dynamic";

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
