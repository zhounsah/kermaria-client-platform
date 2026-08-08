"use client";

import type {
  CommercialOfferPaymentMode,
  FiscalRegime,
  PublicPackCommitmentMonths,
} from "@kermaria/shared";

import { formatCommercialAmountFromCents } from "@/lib/fiscal-formatters";

export type PublicPackSelectionSummaryInput = {
  packLabel: string;
  commitmentMonths: PublicPackCommitmentMonths;
  paymentMode: CommercialOfferPaymentMode;
  monthlyPriceAmountCents: number;
  setupFeeAmountCents: number;
  firstChargeAmountCents: number;
  fiscalRegime: FiscalRegime;
  fiscalMention: string;
};

type PublicPackSelectionSummaryProps = PublicPackSelectionSummaryInput & {
  eyebrow?: string;
  title?: string;
  description?: string;
};

function formatPaymentModeLabel(paymentMode: CommercialOfferPaymentMode) {
  return paymentMode === "upfront" ? "Comptant" : "Mensuel";
}

export function PublicPackSelectionSummary({
  packLabel,
  commitmentMonths,
  paymentMode,
  monthlyPriceAmountCents,
  setupFeeAmountCents,
  firstChargeAmountCents,
  fiscalRegime,
  fiscalMention,
  eyebrow = "Pack sélectionné",
  title,
  description,
}: PublicPackSelectionSummaryProps) {
  return (
    <section
      aria-label={`Résumé du pack ${packLabel}`}
      className="public-pack-selection-summary"
    >
      <div className="public-pack-selection-summary-header">
        <p className="eyebrow">{eyebrow}</p>
        <h2>{title ?? packLabel}</h2>
        {description ? (
          <p className="public-pack-selection-summary-description">
            {description}
          </p>
        ) : null}
      </div>

      <dl className="public-pack-selection-summary-grid">
        <div>
          <dt>Engagement</dt>
          <dd>{commitmentMonths} mois</dd>
        </div>
        <div>
          <dt>Paiement</dt>
          <dd>{formatPaymentModeLabel(paymentMode)}</dd>
        </div>
        <div>
          <dt>Tarif affiché</dt>
          <dd>
            {formatCommercialAmountFromCents(monthlyPriceAmountCents, {
              fiscalRegime,
              suffix: " / mois",
            })}
          </dd>
        </div>
        <div>
          <dt>Mise en service</dt>
          <dd>
            {formatCommercialAmountFromCents(setupFeeAmountCents, {
              fiscalRegime,
            })}
          </dd>
        </div>
        <div>
          <dt>Total initial estimé</dt>
          <dd>
            {formatCommercialAmountFromCents(firstChargeAmountCents, {
              fiscalRegime,
            })}
          </dd>
        </div>
        <div>
          <dt>Fiscalité</dt>
          <dd>{fiscalMention}</dd>
        </div>
      </dl>
    </section>
  );
}
