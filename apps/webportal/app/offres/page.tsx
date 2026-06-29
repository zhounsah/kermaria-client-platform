import type { Metadata } from "next";
import Link from "next/link";

import {
  commercialOfferBillingCadence,
  formatCurrencyFromCents,
} from "@/lib/formatters";
import { getPublicCommercialCatalog } from "@/lib/internal-api";
import { getPayPalMode } from "@/lib/paypal";
import { isSignupEnabled } from "@/lib/public-routes";
import type { CommercialOfferSummary } from "@kermaria/shared";

export const metadata: Metadata = {
  title: "Offres",
  description:
    "Catalogue des prestations proposées par Zachary HOUNSA-HOUNKPA EI.",
};

export const revalidate = 300;

function isOfferOrderable(
  offer: CommercialOfferSummary,
  paypalMode: "sandbox" | "live",
): boolean {
  if (offer.status !== "active") {
    return false;
  }

  if (offer.billingCadence === "monthly") {
    const planId =
      paypalMode === "live"
        ? offer.paypalPlanIdLive
        : offer.paypalPlanIdSandbox;
    return Boolean(planId);
  }

  return true;
}

export default async function OffresPage() {
  const { data: offers } = await getPublicCommercialCatalog();
  const paypalMode = getPayPalMode();
  const signupEnabled = isSignupEnabled();
  const ctaTarget = (offerId: string) =>
    signupEnabled
      ? "/signup"
      : `/contact?offer=${encodeURIComponent(offerId)}`;

  const orderableOffers = offers
    .filter((offer) => isOfferOrderable(offer, paypalMode))
    .sort((a, b) => a.displayOrder - b.displayOrder);

  return (
    <div className="offres-page">
      <header className="offres-header">
        <p className="eyebrow">Catalogue</p>
        <h1>Offres et prestations</h1>
        <p className="offres-lead">
          Liste des prestations actuellement disponibles. Les tarifs sont
          indicatifs et hors taxes. Pour un devis personnalisé, contactez-nous.
        </p>
      </header>

      {orderableOffers.length === 0 ? (
        <p className="offres-empty">
          Aucune offre n&apos;est actuellement disponible en ligne. Contactez-nous
          pour un devis personnalisé.
        </p>
      ) : (
        <ul className="offres-grid">
          {orderableOffers.map((offer) => {
            const cadence =
              commercialOfferBillingCadence[offer.billingCadence];
            return (
              <li key={offer.id} className="offres-card">
                <header className="offres-card-header">
                  <span
                    className={`offres-badge offres-badge-${cadence.tone}`}
                  >
                    {cadence.label}
                  </span>
                  <span className="offres-category">{offer.category}</span>
                </header>
                <h2>{offer.name}</h2>
                <p className="offres-description">{offer.description}</p>
                <div className="offres-pricing">
                  <strong>
                    {formatCurrencyFromCents(offer.priceAmountCents)}
                  </strong>
                  <span>
                    HT
                    {offer.unitLabel ? ` · ${offer.unitLabel}` : null}
                    {offer.billingCadence === "monthly" ? " · /mois" : null}
                  </span>
                </div>
                <Link className="button" href={ctaTarget(offer.id)}>
                  Demander un devis
                </Link>
              </li>
            );
          })}
        </ul>
      )}

      <aside className="offres-footnote">
        <p>
          Les prix indiqués sont susceptibles d&apos;évoluer. Le tarif applicable
          est celui figurant sur le devis ou la facture émis pour la prestation.
          Consultez les{" "}
          <Link href="/cgv">conditions générales de vente</Link> pour plus de
          détails.
        </p>
      </aside>
    </div>
  );
}
