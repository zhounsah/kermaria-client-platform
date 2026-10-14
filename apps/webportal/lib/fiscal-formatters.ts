import type { FiscalRegime } from "@kermaria/shared";

import { formatCurrencyFromCents } from "@/lib/formatters";

type CommercialAmountOptions = {
  fiscalRegime: FiscalRegime;
  suffix?: string;
};

export function formatCommercialAmountFromCents(
  amountCents: number,
  { suffix = "" }: CommercialAmountOptions,
) {
  return `${formatCurrencyFromCents(amountCents)} TTC${suffix}`;
}

export function formatFiscalMention(
  fiscalRegime: FiscalRegime,
  fiscalMention?: string | null,
) {
  if (fiscalMention) {
    return fiscalMention;
  }

  switch (fiscalRegime) {
    case "standard":
      return "TVA au taux en vigueur.";
    case "franchise_base":
      return "TVA non applicable, art. 293 B du CGI.";
    case "reverse_charge":
      return "Autoliquidation de la TVA par le preneur.";
    case "exempt":
      return "Exonération de TVA selon le fondement indiqué sur le document.";
    case "outside_scope":
      return "Opération hors du champ de la TVA française.";
    default:
      return "Traitement fiscal à vérifier avant émission.";
  }
}

export function shouldShowVatBreakdown(fiscalRegime: FiscalRegime) {
  return fiscalRegime === "standard";
}
