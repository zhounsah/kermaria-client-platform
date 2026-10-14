import { formatCurrencyFromCents } from "@/lib/formatters";

export type BillingV2PricingSummaryLine = {
  id: string;
  label: string;
  detail?: string | null;
  quantity?: number;
  amountCents: number;
};

type Props = {
  lines: readonly BillingV2PricingSummaryLine[];
  recurringSubtotalCents: number;
  recurringDiscountCents: number;
  recurringTotalCents: number;
  oneTimeDueNowCents: number;
  totalDueNowCents: number;
  subtotalBeforeTaxCents?: number;
  taxAmountCents?: number;
  taxRateBasisPoints?: number | null;
  finalLabel?: string;
  finalAmountCents?: number;
  finalPeriodLabel?: string | null;
  discountLabel?: string;
  showDueNow?: boolean;
  currency: string;
};

/**
 * Présentation pure d'un devis déjà calculé. Ce composant n'additionne ni ne
 * transforme aucune composante : le quote serveur reste l'unique autorité.
 */
export function BillingV2PricingSummary({
  lines,
  recurringSubtotalCents,
  recurringDiscountCents,
  recurringTotalCents,
  oneTimeDueNowCents,
  totalDueNowCents,
  subtotalBeforeTaxCents,
  taxAmountCents,
  taxRateBasisPoints,
  finalLabel = "Prix final",
  finalAmountCents,
  finalPeriodLabel = " / mois",
  discountLabel = "Remise",
  showDueNow = true,
  currency,
}: Props) {
  return <>
    <ul className="formule-summary-lines">
      {lines.map((line) => <li key={line.id}>
        <span className="formule-summary-line-label">
          {line.label}
          {line.detail ? <em> — {line.detail}</em> : null}
          {(line.quantity ?? 1) > 1 ? <em> × {line.quantity}</em> : null}
        </span>
        <span className="formule-summary-line-amount">
          {formatMoney(line.amountCents, currency)} TTC
        </span>
      </li>)}
    </ul>

    <dl className="formule-summary-totals">
      <div>
        <dt>Prix avant remise TTC</dt>
        <dd className={recurringDiscountCents > 0 ? "formule-summary-strike" : undefined}>
          {formatMoney(recurringSubtotalCents, currency)}<span className="formule-summary-period"> / mois</span>
        </dd>
      </div>
      {recurringDiscountCents > 0 ? <div>
        <dt>{discountLabel}</dt>
        <dd className="formule-summary-discount">−{formatMoney(recurringDiscountCents, currency)} TTC<span className="formule-summary-period"> / mois</span></dd>
      </div> : null}
      <div className="formule-summary-final">
        <dt>{finalLabel} TTC</dt>
        <dd>
          {formatMoney(finalAmountCents ?? recurringTotalCents, currency)}
          {finalPeriodLabel ? <span className="formule-summary-period">{finalPeriodLabel}</span> : null}
        </dd>
      </div>
      {oneTimeDueNowCents > 0 ? <div>
        <dt>Frais ponctuels TTC</dt>
        <dd>{formatMoney(oneTimeDueNowCents, currency)}</dd>
      </div> : null}
      {showDueNow ? <div className="formule-summary-final">
        <dt>Dû maintenant TTC</dt>
        <dd>{formatMoney(totalDueNowCents, currency)}</dd>
      </div> : null}
      {subtotalBeforeTaxCents !== undefined && taxAmountCents !== undefined ? <>
        <div><dt>Montant HT</dt><dd>{formatMoney(subtotalBeforeTaxCents, currency)}</dd></div>
        <div><dt>TVA{taxRateBasisPoints !== null && taxRateBasisPoints !== undefined ? ` (${taxRateBasisPoints / 100} %)` : ""}</dt><dd>{formatMoney(taxAmountCents, currency)}</dd></div>
        <div className="formule-summary-final"><dt>Total TTC</dt><dd>{formatMoney(totalDueNowCents, currency)}</dd></div>
      </> : null}
    </dl>
  </>;
}

function formatMoney(amountCents: number, currency: string) {
  // Le formateur historique EUR reste la présentation préférée des formules;
  // la forme standard couvre les devis Cart dans leur devise unique.
  return currency === "EUR"
    ? formatCurrencyFromCents(amountCents)
    : new Intl.NumberFormat("fr-FR", { style: "currency", currency }).format(amountCents / 100);
}
