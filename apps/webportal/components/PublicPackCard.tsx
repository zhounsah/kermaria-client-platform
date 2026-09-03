import Link from "next/link";

import { getPublicPackBackupPolicySummary } from "@kermaria/shared";

import { formatCurrencyFromCents } from "@/lib/formatters";
import type { PublicPackView } from "@/lib/public-packs";

/**
 * Carte publique d'une formule.
 *
 * Elle ne choisit ni engagement ni mode de reglement : ces arbitrages ont un
 * prix, et un prix ne se calcule pas dans le navigateur. La carte annonce le
 * point de depart mensuel calcule par le serveur puis renvoie vers
 * `/formules/{code}`, ou chaque changement de configuration est reevalue par le
 * moteur tarifaire.
 */
type PublicPackCardProps = {
  pack: PublicPackView;
  signupEnabled?: boolean;
};

export function PublicPackCard({
  pack,
  signupEnabled = true,
}: PublicPackCardProps) {
  const backupPolicy = getPublicPackBackupPolicySummary(pack);

  return (
    <article className="public-pack-card">
      <header className="public-pack-header">
        <div className="public-pack-header-copy">
          <h2>{pack.label}</h2>
          <p className="public-pack-audience">{pack.audience}</p>
        </div>
        {pack.highlightLabel ? (
          <span className="status-badge status-badge-info">
            {pack.highlightLabel}
          </span>
        ) : null}
      </header>

      <p className="public-pack-headline">{pack.headline}</p>
      <p className="public-pack-description">{pack.description}</p>

      <div className="public-pack-pricing">
        <div className="public-pack-price-main">
          <strong>
            {formatCurrencyFromCents(pack.baselineMonthlyAmountCents)} / mois
          </strong>
          <span>
            Configuration recommandée, sans engagement. Le tarif définitif —
            options, capacité, remise d&apos;engagement — est calculé à
            l&apos;étape suivante.
          </span>
        </div>
      </div>

      <div className="public-pack-columns">
        <div>
          <h3>Inclus</h3>
          <ul>
            {pack.included.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </div>
        <div>
          <h3>Différences clés</h3>
          <ul>
            {pack.highlights.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </div>
      </div>

      <div className="public-pack-policy">
        <p className="public-pack-policy-kicker">
          {backupPolicy.included ? "Sauvegarde incluse" : "Sauvegarde en option"}
        </p>
        <p className="public-pack-policy-text">{backupPolicy.summary}</p>
        <Link className="text-link" href={backupPolicy.detailsHref}>
          {backupPolicy.detailsLabel}
        </Link>
      </div>

      <div className="public-pack-cta">
        {signupEnabled ? (
          <Link
            className="button"
            href={`/formules/${encodeURIComponent(pack.presetCode)}`}
          >
            Configurer cette offre
          </Link>
        ) : (
          <Link
            className="button"
            href={`/contact?formule=${encodeURIComponent(pack.presetCode)}`}
          >
            Demander cette offre
          </Link>
        )}
        <Link className="text-link" href={`/offres/${pack.slug}`}>
          Voir la fiche technique
        </Link>
      </div>
    </article>
  );
}
