import Link from "next/link";

import { BillingV2DirectSubscribe } from "@/components/BillingV2DirectSubscribe";
import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { SitePageFrame } from "@/components/SitePageFrame";
import { requireClientSession } from "@/lib/auth";
import {
  describePresetBenefits,
  resolvePresetBaselineMonthlyCents,
  resolvePresetTagline,
} from "@/lib/billing-v2-formules";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { getBillingV2FormulesCatalog, getClientSubscribePageLayout } from "@/lib/internal-api";

export const metadata = {
  title: "Souscrire",
};

export const dynamic = "force-dynamic";

/**
 * Souscription depuis l'espace client.
 *
 * Une seule autorite commerciale : le catalogue Billing V2. La page propose
 * les deux formes de selection que le modele reconnait — une offre, ou une
 * composition directe de services — et rien d'autre. Aucun montant n'est
 * calcule ici : les tarifs affiches viennent d'API-INTERNAL, et le montant
 * reellement facture est recalcule au checkout par le meme moteur.
 */
export default async function SubscribePage() {
  await requireClientSession();
  const [catalogResult, layoutResult] = await Promise.all([
    getBillingV2FormulesCatalog(), getClientSubscribePageLayout(),
  ]);
  const catalog = catalogResult.data;
  const presets = [...catalog.presets].sort(
    (left, right) => left.displayOrder - right.displayOrder,
  );

  const slots = {
    subscribe_intro: <><PageHeader
        description="Choisissez une offre adaptée à votre besoin et ajustez-la avant de confirmer. Le prix affiché se met à jour à chaque choix."
        eyebrow="Espace client"
        title="Souscrire"
      />

      {catalogResult.error ? (
        <ErrorState
          description="Les offres et leurs prix ne peuvent pas être affichés pour le moment. Réessayez dans quelques instants."
          reference={catalogResult.correlationId}
          title="Catalogue indisponible"
        />
      ) : null}</>,

    subscribe_offers: <section aria-label="Offres" className="subscribe-presets">
        <h2>Offres</h2>
        <p className="subscribe-section-lead">
          Chaque offre part d&apos;une configuration recommandée que vous
          pouvez ajuster avant de souscrire.
        </p>

        {presets.length === 0 ? (
          <p className="subscribe-empty">
            Aucune offre n&apos;est publiée pour le moment.
          </p>
        ) : (
          <ul className="subscribe-preset-grid">
            {presets.map((preset) => {
              const monthlyCents = resolvePresetBaselineMonthlyCents(preset);
              const benefits = describePresetBenefits(preset, catalog);

              return (
                <li className="subscribe-preset-card" key={preset.code}>
                  <h3>{preset.name}</h3>
                  <p className="subscribe-preset-tagline">
                    {resolvePresetTagline(preset)}
                  </p>
                  <p className="subscribe-preset-price">
                    <strong>{formatCurrencyFromCents(monthlyCents)}</strong>
                    {" / mois"}
                  </p>
                  <ul className="subscribe-preset-benefits">
                    {benefits.map((benefit) => (
                      <li key={benefit.key}>{benefit.label}</li>
                    ))}
                  </ul>
                  <Link
                    className="button"
                    href={`/formules/${encodeURIComponent(preset.code)}`}
                  >
                    Configurer et souscrire
                  </Link>
                </li>
              );
            })}
          </ul>
        )}
      </section>,

    subscribe_direct: <section aria-label="Services à la carte" className="subscribe-a-la-carte">
        <h2>Services à la carte</h2>
        <p className="subscribe-section-lead">
          Ajoutez un service isolé, sans offre ni engagement. Le tarif
          s&apos;actualise à chaque changement.
        </p>
        <BillingV2DirectSubscribe catalog={catalog} />
      </section>,
    subscribe_help: <section className="content-panel subscribe-help">
      <h2>Besoin d’aide pour choisir ?</h2>
      <p>Décrivez votre situation avant de commander ; nous vous aiderons à préciser ce dont vous avez besoin.</p>
      <Link href="/contact">Nous contacter</Link>
    </section>,
  };
  return <div className="subscribe-page">
    <SitePageFrame area="client" pageKey="/souscrire" initialLayout={layoutResult.data} slots={slots}>{null}</SitePageFrame>
  </div>;
}
