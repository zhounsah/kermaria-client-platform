import Link from "next/link";

import { AdminDiagnosticCenter } from "@/components/AdminDiagnosticCenter";
import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { requireAdminSession } from "@/lib/auth";
import {
  DEFAULT_DIAGNOSTIC_RECOMMENDATION_CONFIG,
  DIAGNOSTIC_RECOMMENDATION_CONTENT_KEY,
  parseDiagnosticRecommendationConfig,
} from "@/lib/diagnostic-recommendation-config";
import {
  getAdminDiagnosticConfiguration,
  getBillingV2FormulesCatalog,
  getPublicManagedContent,
} from "@/lib/internal-api";

export const dynamic = "force-dynamic";
export const metadata = { title: "Diagnostic - Administration" };

export default async function AdminDiagnosticPage() {
  await requireAdminSession();
  // Le simulateur charge exactement les memes donnees que la page publique :
  // catalogue Billing V2 et regles de recommandation administrees.
  const [view, catalogResult, recommendationContentResult] = await Promise.all([
    getAdminDiagnosticConfiguration(),
    getBillingV2FormulesCatalog(),
    getPublicManagedContent(DIAGNOSTIC_RECOMMENDATION_CONTENT_KEY),
  ]);
  const recommendationConfig =
    parseDiagnosticRecommendationConfig(recommendationContentResult.data?.bodyMarkdown)
    ?? DEFAULT_DIAGNOSTIC_RECOMMENDATION_CONFIG;

  return (
    <>
      <PageHeader
        eyebrow="Centre de configuration"
        title="Diagnostic"
        description="Contextes, questions, conditions, textes de résultat et correspondance Billing V2. Le brouillon n'est jamais visible du public : seule une publication bascule le parcours, en une seule fois."
      />
      <p>
        <Link className="back-link" href="/admin/settings">
          <span aria-hidden="true">←</span> Retour au centre de configuration
        </Link>
      </p>
      {view.error ? (
        <ErrorState
          title="Configuration indisponible"
          description="La configuration du diagnostic ne peut pas être chargée pour le moment."
          reference={view.correlationId}
        />
      ) : (
        <AdminDiagnosticCenter
          catalog={catalogResult.data}
          initialView={view.data}
          recommendationConfig={recommendationConfig}
        />
      )}
    </>
  );
}
