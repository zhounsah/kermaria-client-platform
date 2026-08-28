import Link from "next/link";

import { AdminDiagnosticRecommendationForm } from "@/components/AdminDiagnosticRecommendationForm";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { requireAdminSession } from "@/lib/auth";
import {
  DEFAULT_DIAGNOSTIC_RECOMMENDATION_CONFIG,
  DIAGNOSTIC_RECOMMENDATION_CONTENT_KEY,
  parseDiagnosticRecommendationConfig,
} from "@/lib/diagnostic-recommendation-config";
import {
  getAdminManagedContent,
  getBillingV2FormulesCatalog,
} from "@/lib/internal-api";

export const metadata = {
  title: "Diagnostic - Administration",
};

export const dynamic = "force-dynamic";

export default async function AdminDiagnosticPage() {
  await requireAdminSession();

  const [contentResult, catalogResult] = await Promise.all([
    getAdminManagedContent(DIAGNOSTIC_RECOMMENDATION_CONTENT_KEY),
    getBillingV2FormulesCatalog(),
  ]);

  const storedConfig = parseDiagnosticRecommendationConfig(
    contentResult.data?.bodyMarkdown,
  );
  const initialConfig = storedConfig ?? DEFAULT_DIAGNOSTIC_RECOMMENDATION_CONFIG;

  const availablePresets = catalogResult.data.presets
    .slice()
    .sort((left, right) => left.displayOrder - right.displayOrder)
    .map((preset) => ({
      code: preset.code,
      label: preset.name,
      available: true,
    }));

  const knownPresetCodes = new Set(availablePresets.map((preset) => preset.code));
  const missingConfiguredPresets = initialConfig.rules
    .map((rule) => rule.presetCode)
    .filter((presetCode): presetCode is string =>
      presetCode !== null && !knownPresetCodes.has(presetCode)
    )
    .filter((presetCode, index, values) => values.indexOf(presetCode) === index)
    .map((presetCode) => ({
      code: presetCode,
      label: presetCode,
      available: false,
    }));

  const presets = [...availablePresets, ...missingConfiguredPresets];

  return (
    <>
      <PageHeader
        description="Associez les profils issus du diagnostic aux formules commerciales sans modifier le code."
        eyebrow="Administration interne"
        title="Règles du diagnostic"
      />

      <section className="content-panel page-header-split">
        <div>
          <span className="card-kicker">Moteur de recommandation</span>
          <h2>Choisir la formule de base de chaque profil</h2>
          <p>
            Les questions et leur interprétation restent contrôlées par
            l&apos;application. Ici, vous décidez uniquement quelle formule
            commerciale doit être proposée lorsque le diagnostic reconnaît un profil.
          </p>
        </div>
        <div className="stack-row">
          <Link className="button button-secondary" href="/diagnostic">
            Tester le diagnostic
          </Link>
          <Link className="button button-secondary" href="/admin/catalog">
            Ouvrir le catalogue Billing V2
          </Link>
        </div>
      </section>

      {catalogResult.error ? (
        <ErrorState
          compact
          description="Le catalogue Billing V2 est indisponible. Les règles restent lisibles, mais aucune nouvelle formule ne peut être sélectionnée tant que le catalogue n'est pas revenu."
          reference={catalogResult.correlationId}
          title="Catalogue commercial indisponible"
        />
      ) : null}

      {contentResult.error || !contentResult.data ? (
        <ErrorState
          description="Impossible de charger la configuration persistante du diagnostic."
          reference={contentResult.correlationId}
          title="Règles indisponibles"
        />
      ) : (
        <SectionCard ariaLabel="Règles de recommandation du diagnostic">
          <h2>Correspondances profil → formule</h2>
          <p className="field-hint">
            « Aucun parcours standard » force un cadrage/devis pour le profil
            concerné. Une formule absente du catalogue public n&apos;est jamais
            proposée au client.
          </p>
          {!storedConfig ? (
            <p className="field-hint">
              La configuration enregistrée est invalide ou absente : les valeurs
              sûres par défaut sont affichées. Enregistrez pour les persister.
            </p>
          ) : null}
          <AdminDiagnosticRecommendationForm
            initialConfig={initialConfig}
            presets={presets}
          />
        </SectionCard>
      )}

      <MockNotice
        correlationId={contentResult.correlationId}
        source={contentResult.source}
      />
    </>
  );
}
