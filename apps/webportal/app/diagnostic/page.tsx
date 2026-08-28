import type { Metadata } from "next";
import Link from "next/link";

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
import { resolveSystemSnippets } from "@/lib/system-snippets";

export const metadata: Metadata = buildPublicMetadata({
  title: "Diagnostic informatique adapté à votre besoin",
  description:
    "Décrivez votre besoin de sauvegarde, accès distant, réseau, messagerie, domaine, serveur ou hébergement et obtenez une orientation ciblée.",
  path: "/diagnostic",
});

export const dynamic = "force-dynamic";

type DiagnosticPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function DiagnosticPage({ searchParams }: DiagnosticPageProps) {
  const params = await searchParams;
  const rawContext = Array.isArray(params.context) ? params.context[0] : params.context;
  const initialContext = resolveDiagnosticContext(rawContext);
  const [catalogResult, recommendationContentResult, snippets] = await Promise.all([
    getBillingV2FormulesCatalog(),
    getPublicManagedContent(DIAGNOSTIC_RECOMMENDATION_CONTENT_KEY),
    resolveSystemSnippets(),
  ]);
  const recommendationConfig =
    parseDiagnosticRecommendationConfig(recommendationContentResult.data?.bodyMarkdown)
    ?? DEFAULT_DIAGNOSTIC_RECOMMENDATION_CONFIG;

  return (
    <>
      <div className="diagnostic-page diagnostic-page-nav">
        <Link className="back-link" href="/services">
          <span aria-hidden="true">{"<-"}</span> Retour aux services
        </Link>
      </div>
      <PublicDiagnosticWizard
        catalog={catalogResult.data}
        initialContext={initialContext}
        recommendationConfig={recommendationConfig}
        snippets={snippets}
      />
    </>
  );
}
