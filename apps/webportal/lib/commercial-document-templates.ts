import type {
  BillingV2AdminCatalogSnapshot,
  BillingV2AdminPrice,
  BillingV2AdminService,
  BillingV2AdminTier,
} from "@/lib/internal-api";

/**
 * Modele de ligne propose a la saisie d'un document commercial.
 *
 * Ce n'est pas un lien vers le catalogue : c'est une valeur recopiee au moment
 * ou l'exploitant la choisit. Un devis reste donc lisible et coherent apres une
 * revision de prix, sans que le catalogue courant ne reecrive retroactivement
 * ce qui a ete propose ou facture.
 */
export type CatalogLineTemplate = {
  key: string;
  label: string;
  description: string | null;
  unitLabel: string;
  unitPriceCents: number;
  taxRateBasisPoints: number | null;
  priceCode: string;
};

/**
 * Modeles issus des prix <b>actuellement en vigueur</b>.
 *
 * Une version future n'est pas proposee : un document emis aujourd'hui ne peut
 * pas s'appuyer sur un tarif qui n'a pas encore pris effet. Une version passee
 * ne l'est pas non plus : elle appartient aux documents qu'elle a produits.
 */
export function buildCatalogLineTemplates(
  snapshot: BillingV2AdminCatalogSnapshot,
  now: Date = new Date(),
): CatalogLineTemplate[] {
  const templates: CatalogLineTemplate[] = [];

  for (const service of snapshot.services) {
    if (service.status !== "active") {
      continue;
    }

    for (const price of service.flatPrices) {
      const template = toTemplate(service, null, price, now);
      if (template) {
        templates.push(template);
      }
    }

    for (const tier of service.tiers) {
      if (tier.status !== "active") {
        continue;
      }

      for (const price of tier.prices) {
        const template = toTemplate(service, tier, price, now);
        if (template) {
          templates.push(template);
        }
      }
    }
  }

  return templates.sort((left, right) =>
    left.label.localeCompare(right.label, "fr-FR"),
  );
}

function toTemplate(
  service: BillingV2AdminService,
  tier: BillingV2AdminTier | null,
  price: BillingV2AdminPrice,
  now: Date,
): CatalogLineTemplate | null {
  if (price.status !== "active" || !isCurrent(price, now)) {
    return null;
  }

  const tierLabel = tier ? tier.publicLabel ?? tier.name : null;

  return {
    key: price.id,
    label: tierLabel ? `${service.name} — ${tierLabel}` : service.name,
    description: service.description,
    unitLabel: price.billingCadence === "monthly" ? "mois" : "forfait",
    unitPriceCents: price.amountCents,
    taxRateBasisPoints: price.taxRateBasisPoints,
    priceCode: price.priceCode,
  };
}

function isCurrent(price: BillingV2AdminPrice, now: Date) {
  const from = Date.parse(price.validFrom);
  if (Number.isNaN(from) || from > now.getTime()) {
    return false;
  }

  if (!price.validUntil) {
    return true;
  }

  const until = Date.parse(price.validUntil);
  return Number.isNaN(until) || until > now.getTime();
}
