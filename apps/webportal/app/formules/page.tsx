import type { Metadata } from "next";
import Link from "next/link";

import { formatCurrencyFromCents } from "@/lib/formatters";
import { getBillingV2FormulesCatalog } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";
import {
  describePresetComposition,
  resolvePresetBaselineMonthlyCents,
  resolvePresetTagline,
} from "@/lib/billing-v2-formules";

export const metadata: Metadata = buildPublicMetadata({
  title: "Formules de stockage, sauvegarde et accès distant",
  description:
    "Quatre formules claires — dossier sécurisé, accès sécurisé, bureau Windows et Pro — configurables et facturées au mois, avec remise sur engagement.",
  path: "/formules",
});

export const dynamic = "force-dynamic";

export default async function FormulesPage() {
  const { data: catalog } = await getBillingV2FormulesCatalog();
  const presets = [...catalog.presets].sort(
    (left, right) => left.displayOrder - right.displayOrder,
  );
  const bestDiscount = catalog.commitments.reduce(
    (max, commitment) => Math.max(max, commitment.discountBasisPoints),
    0,
  );

  return (
    <div className="formules-page">
      <header className="formules-header">
        <p className="eyebrow">Formules</p>
        <h1>Choisissez une formule, ajustez-la, souscrivez.</h1>
        <p className="formules-lead">
          Chaque formule part d&apos;une configuration recommandée que vous
          pouvez ajuster : capacité de stockage, sauvegarde, accès à distance,
          utilisateurs. Le prix se met à jour à chaque changement.
        </p>
        {bestDiscount > 0 ? (
          <p className="formules-lead-note">
            Sans engagement, ou jusqu&apos;à −{bestDiscount / 100} % en
            s&apos;engageant, toujours payé au mois.
          </p>
        ) : null}
      </header>

      {presets.length === 0 ? (
        <p className="formules-empty">
          Le catalogue des formules n&apos;est pas joignable pour le moment.
          Les prix sont servis par l&apos;API interne : aucune valeur
          tarifaire n&apos;est conservée dans le site public.
        </p>
      ) : (
        <>
          <section className="formules-grid" aria-label="Formules disponibles">
            {presets.map((preset) => {
              const monthlyCents = resolvePresetBaselineMonthlyCents(preset);
              const composition = describePresetComposition(preset, catalog);

              return (
                <article className="formule-card" key={preset.code}>
                  <header className="formule-card-header">
                    <h2>{preset.name}</h2>
                    <p className="formule-card-tagline">
                      {resolvePresetTagline(preset.code)}
                    </p>
                  </header>

                  <p className="formule-card-price">
                    <span className="formule-card-amount">
                      {formatCurrencyFromCents(monthlyCents)}
                    </span>
                    <span className="formule-card-period"> / mois</span>
                  </p>

                  <ul className="formule-card-list">
                    {composition.map((entry) => (
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
            <h2>Ce qui est compris dans toutes les formules</h2>
            <p>
              Le socle de service couvre le compte client, l&apos;exploitation
              de la plateforme, la supervision de l&apos;infrastructure et le
              support lié au fonctionnement normal des services. Il est
              toujours facturé, il n&apos;est pas optionnel.
            </p>
            <p className="formules-note-secondary">
              Les montants affichés sont hors taxes applicables et calculés par
              le moteur de facturation, pas par votre navigateur.
            </p>
          </section>
        </>
      )}
    </div>
  );
}
