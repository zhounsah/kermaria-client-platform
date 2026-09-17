import type {
  PublicCommercialCta,
  PublicCommercialOrderingMode,
} from "@kermaria/shared";

type PublicCommercialOrderingInput = {
  requestedMode: PublicCommercialOrderingMode | null;
  legacyMode: Exclude<PublicCommercialOrderingMode, "direct">;
  offerCount: number;
  preferredOfferCta: PublicCommercialCta | null;
  directCta: PublicCommercialCta | null;
};

export type PublicCommercialOrderingResolution = {
  orderingMode: PublicCommercialOrderingMode;
  offerCount: number;
  primaryCta: PublicCommercialCta;
};

const QUOTE_CTA: PublicCommercialCta = {
  label: "Demander un devis",
  href: "/contact",
};

/**
 * Résout le parcours affiché à partir de la décision explicitement portée par
 * Billing V2. Les CTA sont déjà des destinations existantes vérifiées par la
 * vitrine : cette fonction ne construit aucun tunnel ni aucun prix.
 *
 * Les catalogues antérieurs à `public_ordering_mode` restent compatibles via
 * `legacyMode`. Ce repli est fermé : il n'émet jamais `direct`.
 */
export function resolvePublicCommercialOrdering(
  input: PublicCommercialOrderingInput,
): PublicCommercialOrderingResolution {
  const requestedMode = normalizeOrderingMode(input.requestedMode);
  const mode = requestedMode ?? input.legacyMode;

  if (mode === "quote") {
    return quoteResolution();
  }

  if (mode === "offer_component" && input.offerCount > 0) {
    return {
      orderingMode: "offer_component",
      offerCount: input.offerCount,
      primaryCta: input.preferredOfferCta ?? {
        label: input.offerCount > 1 ? "Comparer les offres" : "Voir les offres",
        href: "/offres",
      },
    };
  }

  if (mode === "direct" && input.directCta) {
    return {
      orderingMode: "direct",
      offerCount: 0,
      primaryCta: input.directCta,
    };
  }

  // Donnée historique incomplète ou configuration devenue invalide : la
  // vitrine revient vers le devis plutôt que de simuler une commande.
  return quoteResolution();
}

function quoteResolution(): PublicCommercialOrderingResolution {
  return {
    orderingMode: "quote",
    offerCount: 0,
    primaryCta: QUOTE_CTA,
  };
}

function normalizeOrderingMode(
  value: PublicCommercialOrderingMode | null,
): PublicCommercialOrderingMode | null {
  return value === "quote" || value === "offer_component" || value === "direct"
    ? value
    : null;
}
