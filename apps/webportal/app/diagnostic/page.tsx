import type { Metadata } from "next";
import { PublicDiagnosticWizard } from "@/components/PublicDiagnosticWizard";
import { resolveDiagnosticContext } from "@/lib/diagnostic-context";
import {
  DEFAULT_DIAGNOSTIC_RECOMMENDATION_CONFIG,
  DIAGNOSTIC_RECOMMENDATION_CONTENT_KEY,
  parseDiagnosticRecommendationConfig,
} from "@/lib/diagnostic-recommendation-config";
import {
  getBillingV2FormulesCatalog,
  getPublicManagedContent,
} from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";

export const metadata: Metadata = buildPublicMetadata({
  title: "Pré-diagnostic informatique",
  description:
    "Faites le point sur vos équipements, sauvegardes, réseau et sécurité, puis identifiez les priorités à examiner.",
  path: "/diagnostic",
});

// La vitrine garde le meme mode de rendu que les autres pages publiques : les
// en-tetes, canonical et indicateurs de disponibilite restent ainsi evalues
// a la requete, sans reintroduire de dependance au catalogue commercial.
export const dynamic = "force-dynamic";

type DiagnosticPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function DiagnosticPage({ searchParams }: DiagnosticPageProps) {
  const params = await searchParams;
  const rawContext = Array.isArray(params.context) ? params.context[0] : params.context;
  const context = resolveDiagnosticContext(rawContext);
  const [catalogResult, recommendationContentResult] = await Promise.all([
    getBillingV2FormulesCatalog(),
    getPublicManagedContent(DIAGNOSTIC_RECOMMENDATION_CONTENT_KEY),
  ]);
  const recommendationConfig =
    parseDiagnosticRecommendationConfig(recommendationContentResult.data?.bodyMarkdown)
    ?? DEFAULT_DIAGNOSTIC_RECOMMENDATION_CONFIG;

  return (
    <PublicDiagnosticWizard
      catalog={catalogResult.data}
      context={context}
      recommendationConfig={recommendationConfig}
    />
  );
}
