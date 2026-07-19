"use client";

import type {
  CommercialOfferPaymentMode,
  PublicPackCommitmentMonths,
} from "@kermaria/shared";

import { formatCurrencyFromCents } from "@/lib/formatters";

export type PublicPackSelectionSummaryInput = {
  packLabel: string;
  commitmentMonths: PublicPackCommitmentMonths;
  paymentMode: CommercialOfferPaymentMode;
  monthlyPriceAmountCents: number;
  setupFeeAmountCents: number;
  firstChargeAmountCents: number;
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
  eyebrow = "Pack selectionné",
  title,
  description,
}: PublicPackSelectionSummaryProps) {
  return (
    <section
      aria-label={`Resume du pack ${packLabel}`}
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
          <dd>{formatCurrencyFromCents(monthlyPriceAmountCents)} HT / mois</dd>
        </div>
        <div>
          <dt>Mise en service</dt>
          <dd>{formatCurrencyFromCents(setupFeeAmountCents)} HT</dd>
        </div>
        <div>
          <dt>Première échéance</dt>
          <dd>{formatCurrencyFromCents(firstChargeAmountCents)} HT</dd>
        </div>
      </dl>
    </section>
  );
}
