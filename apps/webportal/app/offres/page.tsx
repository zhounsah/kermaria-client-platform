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
import { buildPublicPackViews, presentPublicPackCatalogContent } from "@/lib/public-packs";
import { isSignupEnabled } from "@/lib/public-routes";

export const metadata: Metadata = buildPublicMetadata({
  title: "Offres de sauvegarde et de services à Guichen",
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
  const content = presentPublicPackCatalogContent(contentResult.data);
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
          <h2 id="offres-formules-title">Configurer une offre</h2>
          <p>
            Ajustez les options proposées et consultez le prix calculé à chaque
            étape avant de poursuivre le parcours de souscription.
          </p>
        </div>
        <Link className="button button-primary" href="/formules">
          Configurer une offre
        </Link>
      </section>,

    offers_demo: <section className="offres-demo-access" aria-labelledby="offres-demo-title">
        <div>
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
              <h2>Les offres publiées</h2>
              <p>
                Consultez les services inclus et le point de départ tarifaire.
                Les options et le montant final sont précisés dans le configurateur.
              </p>
            </div>

            <PublicPackOverviewGrid
              packs={packs}
              signupEnabled={signupEnabled}
            />
          </section>,

    offers_comparison: packs.length === 0 ? null : <section className="offres-comparison">
            <div className="offres-section-heading">
              <h2>Comparer les offres en détail</h2>
              <p>
                Retrouvez les différences de couverture, de mise en service et
                de facturation dans un même tableau.
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
