import type { Metadata } from "next";
import { PublicDiagnosticWizard } from "@/components/PublicDiagnosticWizard";
import { resolveDiagnosticContext } from "@/lib/diagnostic-context";
import {
  getBillingV2FormulesCatalog,
} from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";
import { resolvePublishedPreDiagnosticConfiguration } from "@/lib/diagnostic-configuration";

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
  const requestedContext = resolveDiagnosticContext(rawContext);
  const [catalogResult, preDiagnosticResolution] = await Promise.all([
    getBillingV2FormulesCatalog(),
    resolvePublishedPreDiagnosticConfiguration(),
  ]);
  // Une v2 peut désactiver un contexte sans rendre une ancienne URL invalide.
  // Dans ce cas, retournez vers l'orientation générale : ne continuez jamais
  // à appliquer les règles commerciales d'un contexte dépublié.
  const context = preDiagnosticResolution.configuration
    && !preDiagnosticResolution.configuration.contexts.some((item) => item.id === requestedContext && item.active)
    ? "general"
    : requestedContext;

  return (
    <PublicDiagnosticWizard
      catalog={catalogResult.data}
      context={context}
      preDiagnosticConfiguration={preDiagnosticResolution.configuration}
      diagnosticConfigurationVersion={preDiagnosticResolution.version}
    />
  );
}
