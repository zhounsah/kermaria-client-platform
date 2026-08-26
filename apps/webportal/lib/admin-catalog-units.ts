/** Conversions d'affichage du catalogue. Aucune de ces fonctions ne calcule un prix. */

function parseTwoDecimalPlaces(value: string | number): number | null {
  const normalized = String(value).trim().replace(",", ".");
  const match = /^(\d+)(?:\.(\d{1,2}))?$/.exec(normalized);
  if (!match) {
    return null;
  }

  const whole = Number(match[1]);
  const decimals = Number((match[2] ?? "").padEnd(2, "0"));
  if (!Number.isSafeInteger(whole) || !Number.isSafeInteger(decimals)) {
    return null;
  }

  const hundredths = whole * 100 + decimals;
  return Number.isSafeInteger(hundredths) ? hundredths : null;
}

export function percentToBasisPoints(value: string | number): number | null {
  const basisPoints = parseTwoDecimalPlaces(value);
  return basisPoints !== null && basisPoints <= 10_000 ? basisPoints : null;
}

export function basisPointsToPercent(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isInteger(value)) {
    return "";
  }

  const whole = Math.trunc(value / 100);
  const decimals = String(Math.abs(value % 100)).padStart(2, "0").replace(/0+$/, "");
  return decimals ? `${whole},${decimals}` : String(whole);
}

export function eurosToCents(value: string | number): number | null {
  const cents = parseTwoDecimalPlaces(value);
  return cents !== null && cents <= 100_000_000 ? cents : null;
}

export function centsToEuros(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isInteger(value)) {
    return "";
  }

  const euros = Math.trunc(value / 100);
  const decimals = String(Math.abs(value % 100)).padStart(2, "0");
  return `${euros},${decimals}`;
}
