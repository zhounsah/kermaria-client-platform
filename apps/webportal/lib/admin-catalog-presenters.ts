import type {
  BillingV2AdminPrice,
  BillingV2AdminService,
} from "@/lib/internal-api";

export type AdminPriceWindow = "current" | "scheduled" | "historical";

export function classifyAdminPrice(
  price: BillingV2AdminPrice,
  asOf: Date | string,
): AdminPriceWindow {
  const now = typeof asOf === "string" ? new Date(asOf) : asOf;
  const from = new Date(price.validFrom);
  const until = price.validUntil ? new Date(price.validUntil) : null;

  if (price.status === "active" && from.getTime() > now.getTime()) {
    return "scheduled";
  }
  if (
    price.status === "active"
    && from.getTime() <= now.getTime()
    && (!until || until.getTime() > now.getTime())
  ) {
    return "current";
  }
  return "historical";
}

export function currentAdminPrices(
  service: BillingV2AdminService,
  asOf: Date | string,
): BillingV2AdminPrice[] {
  return [
    ...service.flatPrices,
    ...service.tiers.flatMap((tier) => tier.prices),
  ].filter((price) => classifyAdminPrice(price, asOf) === "current");
}

/** Présentation uniquement : minimum des composantes mensuelles en vigueur. */
export function startingMonthlyPriceCents(
  service: BillingV2AdminService,
  asOf: Date | string,
): number | null {
  const amounts = currentAdminPrices(service, asOf)
    .filter(
      (price) => price.billingCadence === "monthly"
        && price.chargeTrigger === "initial_subscription",
    )
    .map((price) => price.amountCents);

  return amounts.length > 0 ? Math.min(...amounts) : null;
}

export function formatAdminDateTime(value: string | null): string {
  if (!value) {
    return "—";
  }
  return new Intl.DateTimeFormat("fr-FR", {
    dateStyle: "short",
    timeStyle: "short",
    timeZone: "Europe/Paris",
  }).format(new Date(value));
}
