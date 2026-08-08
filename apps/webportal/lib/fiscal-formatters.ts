import type { FiscalRegime } from "@kermaria/shared";

import { formatCurrencyFromCents } from "@/lib/formatters";

type CommercialAmountOptions = {
  fiscalRegime: FiscalRegime;
  suffix?: string;
};

export function formatCommercialAmountFromCents(
  amountCents: number,
  { fiscalRegime, suffix = "" }: CommercialAmountOptions,
) {
  const taxLabel = fiscalRegime === "standard" ? " HT" : "";
  return `${formatCurrencyFromCents(amountCents)}${taxLabel}${suffix}`;
}

export function formatFiscalMention(
  fiscalRegime: FiscalRegime,
  fiscalMention?: string | null,
) {
  if (fiscalMention) {
    return fiscalMention;
  }

  return fiscalRegime === "standard"
    ? "TVA au taux en vigueur."
    : "TVA non applicable, art. 293 B du CGI.";
}

export function shouldShowVatBreakdown(fiscalRegime: FiscalRegime) {
  return fiscalRegime === "standard";
}
