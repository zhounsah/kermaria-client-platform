import type { Metadata } from "next";
import Link from "next/link";

import { formatCurrencyFromCents } from "@/lib/formatters";
import { getBillingV2FormulesCatalog } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";
import {
  describePresetBenefits,
  formatDiscountPercent,
  resolvePresetBaselineMonthlyCents,
  resolvePresetTagline,
} from "@/lib/billing-v2-formules";

export const metadata: Metadata = buildPublicMetadata({
  title: "Offres de stockage, sauvegarde et accès distant",
  description:
    "Quatre offres claires — dossier sécurisé, accès sécurisé, bureau Windows et Pro — configurables et facturées au mois, avec remise sur engagement.",
  path: "/formules",
});

export const dynamic = "force-dynamic";

export default async function FormulesPage() {
  const { data: catalog } = await getBillingV2FormulesCatalog();
  const presets = [...catalog.presets].sort(
    (left, right) => left.displayOrder - right.displayOrder,
  );
  const bestDiscount = catalog.commitments.reduce(
    (max, commitment) =>
      commitment.paymentOptions.reduce(
        (best, option) => Math.max(best, option.discountBasisPoints),
        max,
      ),
    0,
  );

  return (
    <div className="formules-page">
      <header className="formules-header formules-header-2026">
        <p className="eyebrow">Offres</p>
        <h1>Choisissez une offre, ajustez-la, souscrivez.</h1>
        <p className="formules-lead">
          Chaque offre part d&apos;une configuration recommandée que vous
          pouvez ajuster : capacité de stockage, sauvegarde, accès à distance,
          utilisateurs. Le prix se met à jour à chaque changement.
        </p>
        {bestDiscount > 0 ? (
          <p className="formules-lead-note">
            Sans engagement, ou jusqu&apos;à −{formatDiscountPercent(bestDiscount)} % en
            s&apos;engageant — au mois ou en une fois.
          </p>
        ) : null}
      </header>

      {presets.length === 0 ? (
        <p className="formules-empty">
          Le catalogue des offres n&apos;est pas joignable pour le moment.
          Les prix sont servis par l&apos;API interne : aucune valeur
          tarifaire n&apos;est conservée dans le site public.
        </p>
      ) : (
        <>
          <section className="formules-grid" aria-label="Offres disponibles">
            {presets.map((preset) => {
              const monthlyCents = resolvePresetBaselineMonthlyCents(preset);
              const benefits = describePresetBenefits(preset, catalog);

              return (
                <article className="formule-card formule-card-2026" key={preset.code}>
                  <header className="formule-card-header">
                    <h2>{preset.name}</h2>
                    <p className="formule-card-tagline">
                      {resolvePresetTagline(preset)}
                    </p>
                  </header>

                  <p className="formule-card-price">
                    <span className="formule-card-amount">
                      {formatCurrencyFromCents(monthlyCents)}
                    </span>
                    <span className="formule-card-period"> / mois</span>
                  </p>

                  <ul className="formule-card-list">
                    {benefits.map((entry) => (
                      <li key={entry.key}>{entry.label}</li>
                    ))}
                  </ul>

                  <Link
                    className="button button-primary formule-card-action"
                    href={`/formules/${preset.code}`}
                  >
                    Configurer
                  </Link>
                </article>
              );
            })}
          </section>

          <section className="formules-note">
            <h2>Un tarif adapté à vos besoins</h2>
            <p>
              Choisissez uniquement les services et capacités dont vous avez
              besoin. Le prix de votre configuration est recalculé
              automatiquement avant la souscription.
            </p>
            <p>
              Chaque offre comprend la mise en service de votre espace, son
              hébergement, sa supervision et le support lié à son
              fonctionnement.
            </p>
            <p className="formules-note-secondary">
              Les montants affichés sont hors taxes applicables.
            </p>
          </section>
        </>
      )}
    </div>
  );
}
