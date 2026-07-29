import type { Metadata } from "next";

import { PublicPackComparisonTable } from "@/components/PublicPackComparisonTable";
import { PublicPackOverviewGrid } from "@/components/PublicPackOverviewGrid";
import {
  getPublicCommercialCatalog,
  getPublicPackCatalogContent,
} from "@/lib/internal-api";
import { resolvePackCatalog } from "@/lib/public-packs";
import { isSignupEnabled } from "@/lib/public-routes";

export const metadata: Metadata = {
  title: "Offres",
  description:
    "Comparez des packs lisibles, choisissez un engagement clair et poursuivez vers le bon parcours de contact ou d'inscription.",
};

export const revalidate = 300;

export default async function OffresPage() {
  const [{ data: offers }, { data: content }] = await Promise.all([
    getPublicCommercialCatalog(),
    getPublicPackCatalogContent(),
  ]);
  const signupEnabled = isSignupEnabled();
  const packs = resolvePackCatalog(offers, content);

  return (
    <div className="offres-page">
      <header className="offres-header">
        {content.pageEyebrow.trim() ? (
          <p className="eyebrow">{content.pageEyebrow}</p>
        ) : null}
        <h1>{content.pageTitle}</h1>
        <p className="offres-lead">{content.pageDescription}</p>
      </header>

      {packs.length === 0 ? (
        <p className="offres-empty">
          Les packs ne sont pas encore disponibles en ligne. Contactez-nous pour
          obtenir une proposition adaptée.
        </p>
      ) : (
        <>
          <section className="offres-overview">
            <div className="offres-section-heading">
              <h2>Commencez par une vue simple des packs</h2>
              <p>
                Chaque pack présente son cadre d&apos;usage, sa structure tarifaire
                et l&apos;action suivante attendue. Le comparatif détaillé reste
                disponible plus bas si vous voulez arbitrer ligne par ligne.
              </p>
            </div>

            <PublicPackOverviewGrid
              content={content}
              packs={packs}
              signupEnabled={signupEnabled}
            />
          </section>

          <section className="offres-comparison">
            <div className="offres-section-heading">
              <h2>Comparer les différences utiles</h2>
              <p>
                Utilisez le comparatif détaillé pour arbitrer entre engagement,
                paiement, mise en service et niveau de couverture.
              </p>
            </div>

            <PublicPackComparisonTable
              content={content}
              packs={packs}
              signupEnabled={signupEnabled}
            />
          </section>
        </>
      )}
    </div>
  );
}
