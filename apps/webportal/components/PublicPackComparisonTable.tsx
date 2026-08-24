import Link from "next/link";

import type { PublicPackCatalogContent } from "@kermaria/shared";

import { formatCurrencyFromCents } from "@/lib/formatters";
import type { PublicPackView } from "@/lib/public-packs";

/**
 * Comparatif public des formules.
 *
 * Le tableau compare ce qui se compare sans calcul : la couverture
 * fonctionnelle, ligne par ligne, telle que l'editorial la decrit. Les
 * arbitrages tarifaires — duree d'engagement, reglement comptant ou mensuel,
 * remise associee — ne sont volontairement plus simules ici : leur resultat est
 * un montant, et le seul endroit qui a autorite pour le produire est le moteur
 * tarifaire derriere `/formules/{code}`. La colonne affiche donc le point de
 * depart mensuel deja calcule par le serveur, et renvoie vers la configuration.
 */
type PublicPackComparisonTableProps = {
  content: PublicPackCatalogContent;
  packs: readonly PublicPackView[];
  signupEnabled: boolean;
};

function IncludedIcon() {
  return (
    <svg aria-hidden="true" viewBox="0 0 20 20">
      <path
        d="M4.5 10.5 8 14l7.5-8"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="2"
      />
    </svg>
  );
}

function ExcludedIcon() {
  return (
    <svg aria-hidden="true" viewBox="0 0 20 20">
      <path
        d="m5 5 10 10M15 5 5 15"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="2"
      />
    </svg>
  );
}

export function PublicPackComparisonTable({
  content,
  packs,
  signupEnabled,
}: PublicPackComparisonTableProps) {
  const rows = content.comparisonRows
    .slice()
    .sort((left, right) => left.sortOrder - right.sortOrder);
  const orderedPacks = packs
    .slice()
    .sort((left, right) => left.order - right.order);

  return (
    <section className="public-pack-compare-section">
      <div className="public-pack-compare-wrap">
        <div
          aria-colcount={orderedPacks.length + 1}
          aria-label="Comparatif des formules publiques"
          aria-rowcount={rows.length + 1}
          className="public-pack-compare-table"
          role="table"
          style={{
            gridTemplateColumns: `minmax(270px, 0.95fr) repeat(${orderedPacks.length}, minmax(310px, 1fr))`,
          }}
        >
          <div className="public-pack-compare-row" role="row">
            <div
              className="public-pack-compare-feature-head"
              role="columnheader"
            >
              {content.pageEyebrow.trim() ? (
                <span className="public-pack-compare-overline">
                  {content.pageEyebrow}
                </span>
              ) : null}
              <h2>{content.comparisonColumnLabel}</h2>
              <p>
                Comparez les différences utiles avant de choisir votre formule.
                Le tarif définitif dépend de la configuration retenue et de la
                durée d&apos;engagement : il est calculé à l&apos;étape
                suivante.
              </p>
              <div className="public-pack-compare-legend">
                <span>
                  <IncludedIcon />
                  Inclus
                </span>
                <span>
                  <ExcludedIcon />
                  Non inclus
                </span>
              </div>
            </div>

            {orderedPacks.map((pack) => (
              <article
                className={`public-pack-compare-column${pack.highlightLabel ? " is-featured" : ""}`}
                key={pack.key}
                role="columnheader"
              >
                <div className="public-pack-compare-column-head">
                  <div className="public-pack-compare-badge-slot">
                    {pack.highlightLabel ? (
                      <span className="public-pack-compare-badge">
                        {pack.highlightLabel}
                      </span>
                    ) : (
                      <span
                        aria-hidden="true"
                        className="public-pack-compare-badge-spacer"
                      />
                    )}
                  </div>
                  <h3>{pack.label}</h3>
                  <p className="public-pack-compare-audience">
                    {pack.audience}
                  </p>
                  <p className="public-pack-compare-headline">
                    {pack.headline}
                  </p>
                </div>

                <div className="public-pack-compare-price">
                  <div className="public-pack-compare-strike">
                    <span className="public-pack-compare-price-kicker">
                      À partir de
                    </span>
                  </div>
                  <strong>
                    {formatCurrencyFromCents(pack.baselineMonthlyAmountCents)}
                    {" / mois"}
                  </strong>
                  <span className="public-pack-compare-price-caption">
                    Configuration recommandée, sans engagement
                  </span>
                </div>

                <ul className="public-pack-compare-highlights">
                  {pack.highlights.slice(0, 4).map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>

                {signupEnabled ? (
                  <Link
                    className="button"
                    href={`/formules/${encodeURIComponent(pack.presetCode)}`}
                  >
                    Configurer cette formule
                  </Link>
                ) : (
                  <Link
                    className="button"
                    href={`/contact?formule=${encodeURIComponent(pack.presetCode)}`}
                  >
                    Demander cette formule
                  </Link>
                )}
                <Link className="text-link" href={`/offres/${pack.slug}`}>
                  Voir la fiche technique
                </Link>
              </article>
            ))}
          </div>

          {rows.map((row, rowIndex) => (
            <div className="public-pack-compare-row" key={row.id} role="row">
              <div
                className={`public-pack-compare-feature-cell ${rowIndex % 2 === 0 ? "is-even" : "is-odd"}`}
                role="rowheader"
              >
                <span>{row.label}</span>
              </div>
              {orderedPacks.map((pack) => {
                const value = row.values[pack.key];
                return (
                  <div
                    className={`public-pack-compare-value-cell ${rowIndex % 2 === 0 ? "is-even" : "is-odd"}`}
                    key={`${row.id}-${pack.key}`}
                    role="cell"
                  >
                    {value.kind === "included" ? (
                      <span
                        aria-label={`${row.label} inclus`}
                        className="public-pack-value public-pack-value-included"
                        title="Inclus"
                      >
                        <IncludedIcon />
                      </span>
                    ) : value.kind === "excluded" ? (
                      <span
                        aria-label={`${row.label} non inclus`}
                        className="public-pack-value public-pack-value-excluded"
                        title="Non inclus"
                      >
                        <ExcludedIcon />
                      </span>
                    ) : (
                      <span className="public-pack-value-text">
                        {value.text}
                      </span>
                    )}
                  </div>
                );
              })}
            </div>
          ))}
        </div>
      </div>

      <aside className="offres-footnote">
        <p>{content.footnotePrimary}</p>
        {content.footnoteSecondary.trim() ? (
          <p>{content.footnoteSecondary}</p>
        ) : null}
      </aside>
    </section>
  );
}
