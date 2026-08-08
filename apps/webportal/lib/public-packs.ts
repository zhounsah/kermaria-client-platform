import type {
  CommercialOfferPaymentMode,
  CommercialOfferSummary,
  PendingPackSelectionSummary,
  PublicPackCatalogContent,
  PublicPackCode,
  PublicPackCommitmentMonths,
  PublicPackPresentation,
  ResolvedPublicPackManifest,
  ResolvedPublicPackVariant,
} from "@kermaria/shared";
import {
  createDefaultPublicPackCatalogContent,
  getPublicPackManifest,
  resolvePublicPackCatalog,
  resolvePublicPackVariantFromCatalog,
} from "@kermaria/shared";

export type PublicPackSelectionInput = {
  packKey: PublicPackCode;
  commitmentMonths: PublicPackCommitmentMonths;
  paymentMode: CommercialOfferPaymentMode;
};

export type PublicPackSelectionOverride = PublicPackSelectionInput & {
  baseFingerprint: string;
};

export function isPackSelectionUnavailable(
  pack: ResolvedPublicPackManifest,
  selection: PublicPackSelectionInput | null,
) {
  return Boolean(
    selection?.packKey === pack.key
      && selection.paymentMode === "upfront"
      && !pack.variantsByCommitment[selection.commitmentMonths].upfront,
  );
}

export function buildPublicPackSelectionBaseFingerprint(
  pack: ResolvedPublicPackManifest,
  initialSelection: PublicPackSelectionInput | null,
) {
  const baseSelection =
    initialSelection?.packKey === pack.key
      ? {
          packKey: initialSelection.packKey,
          commitmentMonths: initialSelection.commitmentMonths,
          paymentMode: initialSelection.paymentMode,
        }
      : {
          packKey: pack.key,
          commitmentMonths: 1 as const,
          paymentMode: "monthly" as const,
        };

  return JSON.stringify({
    baseSelection,
    variants: ([1, 6, 12] as const).map((commitmentMonths) => {
      const variants = pack.variantsByCommitment[commitmentMonths];
      return {
        commitmentMonths,
        monthlyOfferId: variants.monthly.offer.id,
        upfrontOfferId: variants.upfront?.offer.id ?? null,
      };
    }),
  });
}

export function resolvePublicPackCardSelection(
  pack: ResolvedPublicPackManifest,
  initialSelection: PublicPackSelectionInput | null,
  selectionOverride: PublicPackSelectionOverride | null,
) {
  const baseFingerprint = buildPublicPackSelectionBaseFingerprint(
    pack,
    initialSelection,
  );
  const baseSelection: PublicPackSelectionInput =
    initialSelection?.packKey === pack.key
      ? {
          packKey: initialSelection.packKey,
          commitmentMonths: initialSelection.commitmentMonths,
          paymentMode: initialSelection.paymentMode,
        }
      : {
          packKey: pack.key,
          commitmentMonths: 1,
          paymentMode: "monthly",
        };
  const activeOverride =
    selectionOverride?.baseFingerprint === baseFingerprint
    && selectionOverride.packKey === pack.key
      ? selectionOverride
      : null;

  return {
    baseFingerprint,
    hasActiveOverride: activeOverride !== null,
    selection: activeOverride
      ? {
          packKey: activeOverride.packKey,
          commitmentMonths: activeOverride.commitmentMonths,
          paymentMode: activeOverride.paymentMode,
        }
      : baseSelection,
  };
}

export function normalizePublicPackKey(value: unknown): PublicPackCode | null {
  if (typeof value !== "string") {
    return null;
  }

  const normalized = value.trim();
  return getPublicPackManifest(normalized as PublicPackCode)
    ? (normalized as PublicPackCode)
    : null;
}

export function normalizeCommitmentMonths(
  value: unknown,
): PublicPackCommitmentMonths | null {
  if (value === 1 || value === 6 || value === 12) {
    return value;
  }

  if (typeof value === "string") {
    const normalized = value.trim();
    if (normalized === "1" || normalized === "6" || normalized === "12") {
      return Number(normalized) as PublicPackCommitmentMonths;
    }
  }

  return null;
}

export function normalizePaymentMode(
  value: unknown,
  commitmentMonths: PublicPackCommitmentMonths | null,
): CommercialOfferPaymentMode | null {
  if (commitmentMonths === null) {
    return null;
  }

  if (commitmentMonths === 1) {
    return value === "monthly" ? value : null;
  }

  if (value === "monthly" || value === "upfront") {
    return value;
  }

  return null;
}

export function resolvePackSelectionInput(
  value: Record<string, unknown>,
): PublicPackSelectionInput | null {
  const packKey = normalizePublicPackKey(value.packKey);
  const commitmentMonths = normalizeCommitmentMonths(value.commitmentMonths);
  const paymentMode = normalizePaymentMode(value.paymentMode, commitmentMonths);
  if (!packKey || !commitmentMonths || !paymentMode) {
    return null;
  }

  return {
    packKey,
    commitmentMonths,
    paymentMode,
  };
}

export function resolvePackCatalog(
  catalog: readonly CommercialOfferSummary[],
  content: PublicPackCatalogContent | null = null,
): ResolvedPublicPackManifest[] {
  const presentationByCode = buildPackPresentationMap(content);
  const activeCatalog = catalog.filter((offer) => offer.status === "active");

  return resolvePublicPackCatalog(activeCatalog)
    .filter((pack) =>
      Object.values(pack.variantsByCommitment).every(
        (variants) => variants.monthly.offer.status === "active",
      ),
    )
    .map((pack) => {
      const presentation = presentationByCode.get(pack.key);
      return presentation
        ? {
            ...pack,
            label: presentation.label,
            shortLabel: presentation.shortLabel,
            headline: presentation.headline,
            audience: presentation.audience,
            description: presentation.description,
            highlights: presentation.highlights,
            included: presentation.included,
            order: presentation.displayOrder,
          }
        : pack;
    })
    .sort((left, right) => left.order - right.order);
}

export function resolvePackSelection(
  catalog: readonly CommercialOfferSummary[],
  selection: PublicPackSelectionInput,
): ResolvedPublicPackVariant | null {
  return resolvePublicPackVariantFromCatalog(
    catalog.filter((offer) => offer.status === "active"),
    selection.packKey,
    selection.commitmentMonths,
    selection.paymentMode,
  );
}

export function buildSignupPackSnapshot(
  catalog: readonly CommercialOfferSummary[],
  selection: PublicPackSelectionInput,
  content: PublicPackCatalogContent | null = null,
) {
  const variant = resolvePackSelection(catalog, selection);
  if (!variant) {
    return null;
  }

  const presentation = buildPackPresentationMap(content).get(selection.packKey);
  const billingIntervalMonths =
    variant.offer.billingIntervalMonths
    ?? (selection.paymentMode === "upfront" ? selection.commitmentMonths : 1);

  return {
    packKey: selection.packKey,
    packLabel: presentation?.label ?? variant.offer.name,
    offerId: variant.offer.id,
    offerExternalReference: variant.externalReference,
    commitmentMonths: selection.commitmentMonths,
    paymentMode: selection.paymentMode,
    billingIntervalMonths,
    discountPercent: variant.discountPercent,
    monthlyPriceAmountCents: variant.monthlyPriceAmountCents,
    billingPriceAmountCents: variant.billingPriceAmountCents,
    setupFeeAmountCents: variant.setupFeeAmountCents,
    firstChargeAmountCents: variant.firstChargeAmountCents,
    fiscalRegime: variant.offer.fiscalRegime,
    fiscalMention: variant.offer.fiscalMention,
    currency: variant.currency,
  };
}

export function selectionToQueryString(selection: PublicPackSelectionInput) {
  const params = new URLSearchParams({
    pack: selection.packKey,
    commitment: String(selection.commitmentMonths),
    payment: selection.paymentMode,
  });
  return params.toString();
}

export function selectionToContactQueryString(
  selection: PublicPackSelectionInput,
  offerId: string,
) {
  const params = new URLSearchParams(selectionToQueryString(selection));
  params.set("offer", offerId);
  return params.toString();
}

export function selectionFromSearchParams(
  params: URLSearchParams | Record<string, string | string[] | undefined>,
): PublicPackSelectionInput | null {
  const read = (key: string) => {
    if (params instanceof URLSearchParams) {
      const values = params.getAll(key);
      return values.length === 1 ? values[0] : null;
    }

    const value = params[key];
    return Array.isArray(value) ? null : value ?? null;
  };

  const packKey = normalizePublicPackKey(read("pack"));
  const commitmentMonths = normalizeCommitmentMonths(read("commitment"));
  const paymentMode = normalizePaymentMode(read("payment"), commitmentMonths);

  if (!packKey || !commitmentMonths || !paymentMode) {
    return null;
  }

  return {
    packKey,
    commitmentMonths,
    paymentMode,
  };
}

export function findPendingPackSelectionForPack(
  pendingSelection: PendingPackSelectionSummary | null,
  packKey: PublicPackCode,
): PublicPackSelectionInput | null {
  if (!pendingSelection || pendingSelection.snapshot.packKey !== packKey) {
    return null;
  }

  return {
    packKey,
    commitmentMonths:
      pendingSelection.snapshot.commitmentMonths as PublicPackCommitmentMonths,
    paymentMode: pendingSelection.snapshot.paymentMode,
  };
}

export function buildPackPresentationMap(
  content: PublicPackCatalogContent | null = null,
) {
  const source = content ?? createDefaultPublicPackCatalogContent();
  return new Map<PublicPackCode, PublicPackPresentation>(
    source.packs.map((pack) => [pack.packCode, pack]),
  );
}

export function findPackPresentation(
  packKey: PublicPackCode,
  content: PublicPackCatalogContent | null = null,
) {
  return buildPackPresentationMap(content).get(packKey) ?? null;
}
