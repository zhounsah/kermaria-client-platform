import type { Metadata } from "next";
import Link from "next/link";
import { SitePageFrame } from "@/components/SitePageFrame";

import { PublicPackComparisonTable } from "@/components/PublicPackComparisonTable";
import { PublicPackOverviewGrid } from "@/components/PublicPackOverviewGrid";
import {
  getBillingV2FormulesCatalog,
  getPublicPackCatalogContent,
  getPublicSitePageLayout,
} from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";
import { buildPublicPackViews } from "@/lib/public-packs";
import { isSignupEnabled } from "@/lib/public-routes";

export const metadata: Metadata = buildPublicMetadata({
  title: "Offres de sauvegarde et stockage à Guichen",
  description:
    "Quatre offres conçues pour la sauvegarde distante, le stockage documentaire et la continuité d'activité des particuliers et petites structures.",
  path: "/offres",
});

export const dynamic = "force-dynamic";

export default async function OffresPage() {
  const [catalogResult, contentResult, layoutResult] = await Promise.all([
    getBillingV2FormulesCatalog(),
    getPublicPackCatalogContent(),
    getPublicSitePageLayout("/offres"),
  ]);
  const catalog = catalogResult.data;
  const content = contentResult.data;
  const signupEnabled = isSignupEnabled();
  const packs = buildPublicPackViews(catalog, content);

  const slots = {
    offers_intro: <header className="offres-header">
        {content.pageEyebrow.trim() ? (
          <p className="eyebrow">{content.pageEyebrow}</p>
        ) : null}
        <h1>{content.pageTitle}</h1>
        <p className="offres-lead">{content.pageDescription}</p>
      </header>,

    offers_configure: <section className="offres-demo-access" aria-labelledby="offres-formules-title">
        <div>
          <p className="eyebrow">Nouveau</p>
          <h2 id="offres-formules-title">Configurer une offre et souscrire en ligne</h2>
          <p>
            Quatre offres ajustables — capacité, sauvegarde, accès à
            distance, utilisateurs — avec le prix mis à jour à chaque
            changement et la remise d&apos;engagement affichée.
          </p>
        </div>
        <Link className="button button-primary" href="/formules">
          Configurer une offre
        </Link>
      </section>,

    offers_demo: <section className="offres-demo-access" aria-labelledby="offres-demo-title">
        <div>
          <p className="eyebrow">Espace client</p>
          <h2 id="offres-demo-title">Voir le portail avant de demander une offre</h2>
          <p>
            La démo montre le parcours client avec des données fictives, sans
            accès à un vrai compte ni à des informations de production.
          </p>
        </div>
        <Link className="button button-secondary" href="/decouvrir-espace-client">
          Découvrir l’espace client
        </Link>
      </section>,

    offers_overview: packs.length === 0 ? <p className="offres-empty">
          Les offres ne sont pas encore disponibles en ligne. Contactez-nous
          pour obtenir une proposition adaptée.
        </p> : <section className="offres-overview">
            <div className="offres-section-heading">
              <h2>Commencez par une vue simple des offres</h2>
              <p>
                Chaque offre présente son cadre d&apos;usage, sa structure
                tarifaire et l&apos;action suivante attendue. Le comparatif
                détaillé reste disponible plus bas pour arbitrer ligne par
                ligne.
              </p>
            </div>

            <PublicPackOverviewGrid
              packs={packs}
              signupEnabled={signupEnabled}
            />
          </section>,

    offers_comparison: packs.length === 0 ? null : <section className="offres-comparison">
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
          </section>,
    offers_help: <section className="offres-demo-access">
      <div><h2>Vous hésitez entre deux offres ?</h2>
        <p>Expliquez ce que vous voulez protéger ou retrouver à distance. Nous vous aiderons à choisir.</p></div>
      <Link className="button button-secondary" href="/contact">Parler de mon besoin</Link>
    </section>,
  };
  return <div className="offres-page">
    <SitePageFrame area="public" pageKey="/offres" initialLayout={layoutResult.data} slots={slots}>{null}</SitePageFrame>
  </div>;
}
