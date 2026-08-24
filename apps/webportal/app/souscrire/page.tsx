import Link from "next/link";

import { BillingV2DirectSubscribe } from "@/components/BillingV2DirectSubscribe";
import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { requireClientSession } from "@/lib/auth";
import {
  describePresetBenefits,
  resolvePresetBaselineMonthlyCents,
  resolvePresetTagline,
} from "@/lib/billing-v2-formules";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { getBillingV2FormulesCatalog } from "@/lib/internal-api";

export const metadata = {
  title: "Souscrire",
};

export const dynamic = "force-dynamic";

/**
 * Souscription depuis l'espace client.
 *
 * Une seule autorite commerciale : le catalogue Billing V2. La page propose
 * les deux formes de selection que le modele reconnait — une formule, ou une
 * composition directe de services — et rien d'autre. Aucun montant n'est
 * calcule ici : les tarifs affiches viennent d'API-INTERNAL, et le montant
 * reellement facture est recalcule au checkout par le meme moteur.
 */
export default async function SubscribePage() {
  await requireClientSession();
  const catalogResult = await getBillingV2FormulesCatalog();
  const catalog = catalogResult.data;
  const presets = [...catalog.presets].sort(
    (left, right) => left.displayOrder - right.displayOrder,
  );

  return (
    <div className="subscribe-page">
      <PageHeader
        description="Partez d'une formule recommandée, ou composez votre configuration service par service. Le tarif est calculé par nos serveurs à chaque changement."
        eyebrow="Espace client"
        title="Souscrire"
      />

      {catalogResult.error ? (
        <ErrorState
          description="Le catalogue commercial n'est pas joignable pour le moment. Aucun tarif n'est conservé dans le portail : rien ne peut être affiché tant que l'API interne ne répond pas."
          reference={catalogResult.correlationId}
          title="Catalogue indisponible"
        />
      ) : null}

      <section aria-label="Formules" className="subscribe-presets">
        <h2>Formules</h2>
        <p className="subscribe-section-lead">
          Chaque formule part d&apos;une configuration recommandée que vous
          pouvez ajuster avant de souscrire.
        </p>

        {presets.length === 0 ? (
          <p className="subscribe-empty">
            Aucune formule n&apos;est publiée pour le moment.
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
                    {resolvePresetTagline(preset.code)}
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
      </section>

      <section aria-label="Services à la carte" className="subscribe-a-la-carte">
        <h2>Services à la carte</h2>
        <p className="subscribe-section-lead">
          Ajoutez un service isolé, sans formule ni engagement. Le tarif
          s&apos;actualise à chaque changement.
        </p>
        <BillingV2DirectSubscribe catalog={catalog} />
      </section>
    </div>
  );
}
