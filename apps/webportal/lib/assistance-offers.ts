import type { BillingV2PublicCatalog, PublicCommercialService, PublicCommercialTier } from "@kermaria/shared";

export const ASSISTANCE_SERVICE_CODE = "ASSISTANCE-MAINTENANCE";

export type AssistanceAudience = "particuliers" | "professionnels";

export type AssistanceOffer = {
  code: string;
  name: string;
  seedName: string;
  audience: AssistanceAudience;
  anchor: string;
  summary: string;
  coverage: string;
  minutes: number;
  cadence: string;
  highlights: readonly string[];
  details: readonly string[];
};

// Présentation de la grille validée. Les prix et l'éligibilité restent dans Billing V2.
export const ASSISTANCE_OFFERS: readonly AssistanceOffer[] = [
  {
    code: "ASSIST-PERS-ESSENTIEL", name: "Essentiel", seedName: "Assistance particuliers Essentiel",
    audience: "particuliers", anchor: "essentiel", summary: "Un interlocuteur pour vos questions courantes.",
    coverage: "1 ordinateur", minutes: 15, cadence: "Diagnostic initial",
    highlights: ["Diagnostic initial à distance", "Conseils et recommandations de sécurité"],
    details: ["10 % de remise sur les interventions hors forfait"],
  },
  {
    code: "ASSIST-PERS-SERENITE", name: "Sérénité", seedName: "Assistance particuliers Sérénité",
    audience: "particuliers", anchor: "serenite", summary: "Un suivi pour les ordinateurs du foyer.",
    coverage: "Jusqu’à 2 ordinateurs", minutes: 45, cadence: "Contrôle trimestriel",
    highlights: ["Vérification trimestrielle de sécurité", "Contrôle des mises à jour et des sauvegardes"],
    details: ["15 % de remise sur les interventions hors forfait"],
  },
  {
    code: "ASSIST-PERS-PREMIUM", name: "Premium", seedName: "Assistance particuliers Premium",
    audience: "particuliers", anchor: "premium", summary: "Un accompagnement régulier pour tout le foyer.",
    coverage: "Jusqu’à 4 ordinateurs", minutes: 90, cadence: "Suivi mensuel",
    highlights: ["Suivi préventif mensuel", "Vérification des sauvegardes et de l’antivirus"],
    details: ["Une visite préventive annuelle à domicile de 60 minutes", "Traitement prioritaire dans les horaires de support convenus"],
  },
  {
    code: "ASSIST-PRO-START", name: "Pro Start", seedName: "Assistance professionnels Start",
    audience: "professionnels", anchor: "pro-start", summary: "Le suivi des postes, avec une aide ponctuelle.",
    coverage: "Par poste", minutes: 15, cadence: "Suivi préventif",
    highlights: ["Inventaire et supervision de l’état des postes", "Alertes de sécurité et maintenance préventive"],
    details: [],
  },
  {
    code: "ASSIST-PRO-BUSINESS", name: "Pro Business", seedName: "Assistance professionnels Business",
    audience: "professionnels", anchor: "pro-business", summary: "Plus d’assistance et un bilan chaque mois.",
    coverage: "Par poste", minutes: 45, cadence: "Rapport mensuel",
    highlights: ["Inventaire, supervision et maintenance renforcée", "Contrôles de la protection et des sauvegardes existantes", "Rapport mensuel synthétique"],
    details: ["Prise en charge prioritaire dans les horaires convenus"],
  },
  {
    code: "ASSIST-PRO-PREMIUM", name: "Pro Premium", seedName: "Assistance professionnels Premium",
    audience: "professionnels", anchor: "pro-premium", summary: "Un suivi renforcé et une revue trimestrielle.",
    coverage: "Par poste", minutes: 90, cadence: "Revue trimestrielle",
    highlights: ["Les prestations Pro Business", "Suivi renforcé de la sécurité et des incidents", "Revue technique trimestrielle"],
    details: ["Accompagnement infrastructure et continuité d’activité", "Priorité renforcée selon contrat"],
  },
];

export const ASSISTANCE_AUDIENCES = {
  particuliers: {
    label: "Particuliers",
    title: "Votre informatique, bien accompagnée.",
    description: "Comparez le nombre d’ordinateurs couverts, le temps d’assistance et le suivi prévu pour votre foyer.",
    coverage: "De 1 à 4 ordinateurs du foyer",
    unit: "TTC / mois",
  },
  professionnels: {
    label: "Professionnels",
    title: "Le suivi adapté à vos postes.",
    description: "Comparez l’assistance et la maintenance prévues pour chaque poste, selon les besoins de votre activité.",
    coverage: "Un abonnement par poste",
    unit: "HT indicatif / poste / mois",
  },
} as const;

export function findAssistanceOffer(value: unknown): AssistanceOffer | null {
  if (typeof value !== "string") return null;
  return ASSISTANCE_OFFERS.find((offer) => offer.code === value.trim().toUpperCase()) ?? null;
}

export function orderAssistanceTiers<T extends { id: string }>(tiers: readonly T[]): T[] {
  const order = new Map(ASSISTANCE_OFFERS.map((offer, index) => [offer.code, index]));
  return [...tiers].sort((left, right) =>
    (order.get(left.id) ?? ASSISTANCE_OFFERS.length) - (order.get(right.id) ?? ASSISTANCE_OFFERS.length));
}

export function assistanceOffersFor(services: readonly PublicCommercialService[], audience: AssistanceAudience) {
  const service = services.find((item) => item.id === ASSISTANCE_SERVICE_CODE);
  if (!service) return [];
  return ASSISTANCE_OFFERS.filter((offer) => offer.audience === audience).flatMap((offer) => {
    const tier = service.tiers.find((item) => item.id === offer.code && item.selectable);
    return tier ? [{ offer, service, tier }] : [];
  });
}

export function assistanceOfferName(offer: AssistanceOffer, catalogName: string): string {
  return catalogName === offer.seedName ? offer.name : catalogName;
}

export function assistanceOfferHref(offer: AssistanceOffer): string {
  return `/assistance-maintenance/${offer.audience}#${offer.anchor}`;
}

export function assistanceContactHref(offer: AssistanceOffer): string {
  return `/contact?assistance=${encodeURIComponent(offer.code)}`;
}

export function assistanceDisplayPrice(offer: AssistanceOffer, tier: PublicCommercialTier) {
  const price = tier.recurringPrice;
  const amountCents = offer.audience === "professionnels" ? price?.frenchNetAmountCents : price?.amountCents;
  if (!price || typeof amountCents !== "number" || !Number.isSafeInteger(amountCents) || amountCents <= 0) return null;
  return { amountCents, currency: price.currency };
}

export function resolveAssistanceContactContext(value: unknown, catalog: BillingV2PublicCatalog | null) {
  const offer = findAssistanceOffer(value);
  if (!offer || !catalog || catalog.source === "unavailable") return null;
  const service = catalog.services.find((entry) => entry.code === ASSISTANCE_SERVICE_CODE && entry.publicVisible);
  const tier = service?.tiers.find((entry) => entry.code === offer.code && entry.publicSelectable);
  if (!tier) return null;
  const name = assistanceOfferName(offer, tier.label);
  return {
    code: offer.code,
    name,
    subject: `Demande de devis — ${name}`,
    message: `Bonjour,\nJe souhaite échanger au sujet de la formule ${name}.\n\n${offer.audience === "professionnels" ? "Nombre de postes concernés" : "Nombre d’ordinateurs concernés"} :\nMon besoin :\n`,
    href: assistanceOfferHref(offer),
  };
}
