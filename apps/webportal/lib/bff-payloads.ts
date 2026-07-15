import type {
  AdGroupCreatePayload,
  AdGroupMemberPayload,
  AdUserCreatePayload,
  AdUserMovePayload,
  AdUserRenamePayload,
  CommercialDocumentLinePayload,
  CommercialDocumentPayload,
  CommercialOfferPaymentMode,
  CommercialOfferPayload,
  DownloadCategoryPayload,
  DownloadResourcePayload,
  DownloadVisibilityRulePayload,
  ManagedContentPayload,
  PublicPackCode,
  PublicPackComparisonValueKind,
  PublicPackCatalogContentPayload,
  CustomerAdLinkPayload,
  ServiceRequestPayload,
  SupportRequestPayload,
} from "@kermaria/shared";
import {
  DOWNLOAD_RESOURCE_TYPES,
  DOWNLOAD_SOURCE_KINDS,
  DOWNLOAD_VISIBILITY_MODES,
  DOWNLOAD_VISIBILITY_TARGET_TYPES,
  createDefaultPublicPackCatalogContentPayload,
  getPublicPackManifest,
} from "@kermaria/shared";

const adUserPrincipalNamePattern = /^[^\s@]+@[^\s@]+$/;

export function parseSupportRequestPayload(
  value: unknown,
): SupportRequestPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<SupportRequestPayload>;
  if (
    typeof candidate.serviceId !== "string"
    || typeof candidate.priority !== "string"
    || typeof candidate.subject !== "string"
    || typeof candidate.description !== "string"
  ) {
    return null;
  }

  const payload: SupportRequestPayload = {
    serviceId: candidate.serviceId.trim(),
    priority: candidate.priority as SupportRequestPayload["priority"],
    subject: candidate.subject.trim(),
    description: candidate.description.trim(),
  };

  return payload.serviceId
    && ["low", "normal", "high"].includes(payload.priority)
    && payload.subject.length >= 3
    && payload.subject.length <= 160
    && payload.description.length >= 10
    && payload.description.length <= 4000
    ? payload
    : null;
}

export function parseServiceRequestPayload(
  value: unknown,
): ServiceRequestPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<ServiceRequestPayload>;
  if (
    typeof candidate.catalogItemId !== "string"
    || typeof candidate.subject !== "string"
    || typeof candidate.description !== "string"
  ) {
    return null;
  }

  const payload: ServiceRequestPayload = {
    catalogItemId: candidate.catalogItemId.trim(),
    subject: candidate.subject.trim(),
    description: candidate.description.trim(),
  };

  return payload.catalogItemId
    && payload.subject.length >= 3
    && payload.subject.length <= 160
    && payload.description.length >= 10
    && payload.description.length <= 4000
    ? payload
    : null;
}

export function parseCommercialOfferPayload(
  value: unknown,
): CommercialOfferPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<CommercialOfferPayload>;
  if (
    typeof candidate.name !== "string"
    || typeof candidate.description !== "string"
    || typeof candidate.category !== "string"
    || typeof candidate.unitLabel !== "string"
    || typeof candidate.priceAmountCents !== "number"
    || !(
      typeof candidate.externalReference === "string"
      || candidate.externalReference === null
      || candidate.externalReference === undefined
    )
    || !Array.isArray(candidate.technicalServiceReferences)
    || !candidate.technicalServiceReferences.every((entry) => typeof entry === "string")
    || !Array.isArray(candidate.provisioningGroupSamAccountNames)
    || !candidate.provisioningGroupSamAccountNames.every((entry) => typeof entry === "string")
    || typeof candidate.status !== "string"
    || typeof candidate.displayOrder !== "number"
    || typeof candidate.billingCadence !== "string"
    || !(
      typeof candidate.setupFeeAmountCents === "number"
      || candidate.setupFeeAmountCents === null
      || candidate.setupFeeAmountCents === undefined
    )
    || !(
      typeof candidate.billingIntervalMonths === "number"
      || candidate.billingIntervalMonths === null
      || candidate.billingIntervalMonths === undefined
    )
    || !(
      typeof candidate.commitmentMonths === "number"
      || candidate.commitmentMonths === null
      || candidate.commitmentMonths === undefined
    )
    || !(
      typeof candidate.paymentMode === "string"
      || candidate.paymentMode === null
      || candidate.paymentMode === undefined
    )
    || !(
      typeof candidate.publicPackCode === "string"
      || candidate.publicPackCode === null
      || candidate.publicPackCode === undefined
    )
    || !(
      typeof candidate.paypalPlanIdSandbox === "string"
      || candidate.paypalPlanIdSandbox === null
      || candidate.paypalPlanIdSandbox === undefined
    )
    || !(
      typeof candidate.paypalPlanIdLive === "string"
      || candidate.paypalPlanIdLive === null
      || candidate.paypalPlanIdLive === undefined
    )
    || !(
      typeof candidate.stripePriceIdTest === "string"
      || candidate.stripePriceIdTest === null
      || candidate.stripePriceIdTest === undefined
    )
    || !(
      typeof candidate.stripePriceIdLive === "string"
      || candidate.stripePriceIdLive === null
      || candidate.stripePriceIdLive === undefined
    )
  ) {
    return null;
  }

  const paypalPlanIdSandbox =
    typeof candidate.paypalPlanIdSandbox === "string"
      ? candidate.paypalPlanIdSandbox.trim() || null
      : null;
  const paypalPlanIdLive =
    typeof candidate.paypalPlanIdLive === "string"
      ? candidate.paypalPlanIdLive.trim() || null
      : null;
  const stripePriceIdTest =
    typeof candidate.stripePriceIdTest === "string"
      ? candidate.stripePriceIdTest.trim() || null
      : null;
  const stripePriceIdLive =
    typeof candidate.stripePriceIdLive === "string"
      ? candidate.stripePriceIdLive.trim() || null
      : null;
  const setupFeeAmountCents =
    typeof candidate.setupFeeAmountCents === "number"
      ? Math.trunc(candidate.setupFeeAmountCents)
      : null;
  const billingIntervalMonths =
    typeof candidate.billingIntervalMonths === "number"
      ? Math.trunc(candidate.billingIntervalMonths)
      : null;
  const commitmentMonths =
    typeof candidate.commitmentMonths === "number"
      ? Math.trunc(candidate.commitmentMonths)
      : null;
  const paymentMode =
    typeof candidate.paymentMode === "string"
      ? candidate.paymentMode.trim() || null
      : null;
  const publicPackCode =
    typeof candidate.publicPackCode === "string"
      ? candidate.publicPackCode.trim() || null
      : null;
  const externalReference =
    typeof candidate.externalReference === "string"
      ? candidate.externalReference.trim() || null
      : null;
  const technicalServiceReferences = Array.from(new Set(
    candidate.technicalServiceReferences
      .map((entry) => entry.trim())
      .filter((entry) => entry.length > 0),
  ));
  const provisioningGroupSamAccountNames = Array.from(new Set(
    candidate.provisioningGroupSamAccountNames
      .map((entry) => entry.trim())
      .filter((entry) => entry.length > 0),
  ));
  const payload: CommercialOfferPayload = {
    name: candidate.name.trim(),
    description: candidate.description.trim(),
    category: candidate.category.trim(),
    unitLabel: candidate.unitLabel.trim(),
    priceAmountCents: Math.trunc(candidate.priceAmountCents),
    externalReference,
    technicalServiceReferences,
    provisioningGroupSamAccountNames,
    status: candidate.status as CommercialOfferPayload["status"],
    displayOrder: Math.trunc(candidate.displayOrder),
    billingCadence:
      candidate.billingCadence as CommercialOfferPayload["billingCadence"],
    setupFeeAmountCents,
    billingIntervalMonths,
    commitmentMonths,
    paymentMode: paymentMode as CommercialOfferPaymentMode | null,
    publicPackCode: publicPackCode as PublicPackCode | null,
    paypalPlanIdSandbox,
    paypalPlanIdLive,
    stripePriceIdTest,
    stripePriceIdLive,
  };

  const planIdPattern = /^[A-Za-z0-9_-]{1,64}$/;
  const isValidSetupFee =
    payload.setupFeeAmountCents === null
    || (Number.isInteger(payload.setupFeeAmountCents)
      && payload.setupFeeAmountCents >= 0
      && payload.setupFeeAmountCents <= 100000000);
  const isValidBillingInterval =
    payload.billingIntervalMonths === null
    || (Number.isInteger(payload.billingIntervalMonths)
      && [1, 6, 12].includes(payload.billingIntervalMonths));
  const isValidCommitment =
    payload.commitmentMonths === null
    || (Number.isInteger(payload.commitmentMonths)
      && [1, 6, 12].includes(payload.commitmentMonths));
  const isValidExternalReference =
    payload.externalReference === null
    || /^[A-Za-z0-9._-]{1,100}$/.test(payload.externalReference);
  const isValidTechnicalServiceReferences =
    payload.technicalServiceReferences.length <= 50
    && payload.technicalServiceReferences.every((entry) =>
      /^[A-Za-z0-9._-]{1,100}$/.test(entry)
    );
  const isValidProvisioningGroups =
    payload.provisioningGroupSamAccountNames.length <= 50
    && payload.provisioningGroupSamAccountNames.every((entry) =>
      /^[A-Za-z0-9._-]{1,100}$/.test(entry)
    );
  const isValidPaymentMode =
    payload.paymentMode === null
    || payload.paymentMode === "monthly"
    || payload.paymentMode === "upfront";
  const isValidPublicPackCode =
    payload.publicPackCode === null
    || getPublicPackManifest(payload.publicPackCode) !== null;
  const hasPackMetadata =
    payload.setupFeeAmountCents !== null
    || payload.billingIntervalMonths !== null
    || payload.commitmentMonths !== null
    || payload.paymentMode !== null
    || payload.publicPackCode !== null;

  return payload.name.length >= 3
    && payload.name.length <= 200
    && payload.description.length >= 3
    && payload.description.length <= 1000
    && payload.category.length >= 2
    && payload.category.length <= 100
    && payload.unitLabel.length >= 1
    && payload.unitLabel.length <= 40
    && Number.isInteger(payload.priceAmountCents)
    && payload.priceAmountCents >= 0
    && payload.priceAmountCents <= 100000000
    && ["active", "inactive"].includes(payload.status)
    && Number.isInteger(payload.displayOrder)
    && payload.displayOrder >= 0
    && payload.displayOrder <= 100000
    && ["one_time", "monthly"].includes(payload.billingCadence)
    && isValidSetupFee
    && isValidBillingInterval
    && isValidCommitment
    && isValidExternalReference
    && isValidTechnicalServiceReferences
    && isValidProvisioningGroups
    && isValidPaymentMode
    && isValidPublicPackCode
    && (payload.paypalPlanIdSandbox === null
      || planIdPattern.test(payload.paypalPlanIdSandbox))
    && (payload.paypalPlanIdLive === null
      || planIdPattern.test(payload.paypalPlanIdLive))
    && (payload.stripePriceIdTest === null
      || planIdPattern.test(payload.stripePriceIdTest))
    && (payload.stripePriceIdLive === null
      || planIdPattern.test(payload.stripePriceIdLive))
    && !(
      (payload.publicPackCode !== null
        || payload.technicalServiceReferences.length > 0
        || payload.provisioningGroupSamAccountNames.length > 0)
      && payload.externalReference === null
    )
    && !(
      payload.billingCadence === "one_time"
      && (hasPackMetadata
        || payload.paypalPlanIdSandbox !== null
        || payload.paypalPlanIdLive !== null)
    )
    ? payload
    : null;
}

export function parsePublicPackCatalogContentPayload(
  value: unknown,
): PublicPackCatalogContentPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<PublicPackCatalogContentPayload>;
  if (
    typeof candidate.pageEyebrow !== "string"
    || typeof candidate.pageTitle !== "string"
    || typeof candidate.pageDescription !== "string"
    || typeof candidate.comparisonColumnLabel !== "string"
    || typeof candidate.footnotePrimary !== "string"
    || typeof candidate.footnoteSecondary !== "string"
    || !Array.isArray(candidate.packs)
    || !Array.isArray(candidate.comparisonRows)
  ) {
    return null;
  }

  const defaultPayload = createDefaultPublicPackCatalogContentPayload();
  const packCodes = new Set(defaultPayload.packs.map((pack) => pack.packCode));
  const valueKinds = new Set<string>(["included", "excluded", "text"]);

  const packs = candidate.packs.map((pack) => {
    if (!pack || typeof pack !== "object") {
      return null;
    }

    const item = pack as PublicPackCatalogContentPayload["packs"][number];
    return typeof item.packCode === "string"
      && packCodes.has(item.packCode as PublicPackCode)
      && typeof item.label === "string"
      && typeof item.shortLabel === "string"
      && typeof item.headline === "string"
      && typeof item.audience === "string"
      && typeof item.description === "string"
      && Array.isArray(item.highlights)
      && item.highlights.every((entry) => typeof entry === "string")
      && Array.isArray(item.included)
      && item.included.every((entry) => typeof entry === "string")
      && (typeof item.highlightLabel === "string"
        || item.highlightLabel === null
        || item.highlightLabel === undefined)
      && typeof item.displayOrder === "number"
      ? {
          packCode: item.packCode as PublicPackCode,
          label: item.label.trim(),
          shortLabel: item.shortLabel.trim(),
          headline: item.headline.trim(),
          audience: item.audience.trim(),
          description: item.description.trim(),
          highlights: item.highlights
            .map((entry) => entry.trim())
            .filter((entry) => entry.length > 0),
          included: item.included
            .map((entry) => entry.trim())
            .filter((entry) => entry.length > 0),
          highlightLabel:
            typeof item.highlightLabel === "string"
              ? item.highlightLabel.trim() || null
              : null,
          displayOrder: Math.trunc(item.displayOrder),
        }
      : null;
  });

  const comparisonRows = candidate.comparisonRows.map((row) => {
    if (!row || typeof row !== "object") {
      return null;
    }

    const item = row as PublicPackCatalogContentPayload["comparisonRows"][number];
    if (
      typeof item.id !== "string"
      || typeof item.label !== "string"
      || typeof item.sortOrder !== "number"
      || !item.values
      || typeof item.values !== "object"
    ) {
      return null;
    }

    const values = {} as PublicPackCatalogContentPayload["comparisonRows"][number]["values"];
    for (const packCode of packCodes) {
      const rawValue = item.values[packCode as PublicPackCode];
      if (!rawValue || typeof rawValue !== "object") {
        return null;
      }

      const typedValue = rawValue as { kind?: unknown; text?: unknown };
      if (
        typeof typedValue.kind !== "string"
        || !valueKinds.has(typedValue.kind)
        || !(
          typeof typedValue.text === "string"
          || typedValue.text === null
          || typedValue.text === undefined
        )
      ) {
        return null;
      }

      values[packCode as PublicPackCode] = {
        kind: typedValue.kind as PublicPackComparisonValueKind,
        text:
          typeof typedValue.text === "string"
            ? typedValue.text.trim() || null
            : null,
      };
    }

    return {
      id: item.id.trim(),
      label: item.label.trim(),
      sortOrder: Math.trunc(item.sortOrder),
      values,
    };
  });

  if (packs.some((pack) => pack === null) || comparisonRows.some((row) => row === null)) {
    return null;
  }

  return {
    pageEyebrow: candidate.pageEyebrow.trim(),
    pageTitle: candidate.pageTitle.trim(),
    pageDescription: candidate.pageDescription.trim(),
    comparisonColumnLabel: candidate.comparisonColumnLabel.trim(),
    footnotePrimary: candidate.footnotePrimary.trim(),
    footnoteSecondary: candidate.footnoteSecondary.trim(),
    packs: packs as PublicPackCatalogContentPayload["packs"],
    comparisonRows: comparisonRows as PublicPackCatalogContentPayload["comparisonRows"],
  };
}

export function parseManagedContentPayload(
  value: unknown,
): ManagedContentPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<ManagedContentPayload>;
  if (
    typeof candidate.bodyMarkdown !== "string"
    || !(
      typeof candidate.versionLabel === "string"
      || candidate.versionLabel === null
      || candidate.versionLabel === undefined
    )
  ) {
    return null;
  }

  const bodyMarkdown = candidate.bodyMarkdown.trim();
  const versionLabel =
    typeof candidate.versionLabel === "string"
      ? candidate.versionLabel.trim() || null
      : null;

  return bodyMarkdown.length >= 10
    && bodyMarkdown.length <= 120000
    && (versionLabel === null || versionLabel.length <= 160)
    ? {
        bodyMarkdown,
        versionLabel,
      }
    : null;
}

export function parseDownloadCategoryPayload(
  value: unknown,
): DownloadCategoryPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<DownloadCategoryPayload>;
  if (
    typeof candidate.slug !== "string"
    || typeof candidate.title !== "string"
    || !(
      typeof candidate.description === "string"
      || candidate.description === null
      || candidate.description === undefined
    )
    || typeof candidate.status !== "string"
    || typeof candidate.displayOrder !== "number"
  ) {
    return null;
  }

  const payload: DownloadCategoryPayload = {
    slug: candidate.slug.trim().toLowerCase(),
    title: candidate.title.trim(),
    description:
      typeof candidate.description === "string"
        ? candidate.description.trim() || null
        : null,
    status: candidate.status.trim() as DownloadCategoryPayload["status"],
    displayOrder: Math.trunc(candidate.displayOrder),
  };

  return /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(payload.slug)
    && payload.slug.length <= 80
    && payload.title.length >= 2
    && payload.title.length <= 120
    && (payload.description === null || payload.description.length <= 280)
    && ["active", "inactive"].includes(payload.status)
    && Number.isInteger(payload.displayOrder)
    && payload.displayOrder >= 0
    && payload.displayOrder <= 9999
    ? payload
    : null;
}

export function parseDownloadResourcePayload(
  value: unknown,
): DownloadResourcePayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<DownloadResourcePayload>;
  if (
    typeof candidate.categoryId !== "string"
    || typeof candidate.title !== "string"
    || typeof candidate.shortDescription !== "string"
    || typeof candidate.resourceType !== "string"
    || typeof candidate.sourceKind !== "string"
    || typeof candidate.visibilityMode !== "string"
    || typeof candidate.status !== "string"
    || !(
      typeof candidate.externalUrl === "string"
      || candidate.externalUrl === null
      || candidate.externalUrl === undefined
    )
    || !(
      typeof candidate.versionLabel === "string"
      || candidate.versionLabel === null
      || candidate.versionLabel === undefined
    )
    || !(
      typeof candidate.installationInstructions === "string"
      || candidate.installationInstructions === null
      || candidate.installationInstructions === undefined
    )
    || typeof candidate.displayOrder !== "number"
    || !Array.isArray(candidate.visibilityRules)
  ) {
    return null;
  }

  const visibilityRules = candidate.visibilityRules
    .map((rule) => parseDownloadVisibilityRulePayload(rule))
    .filter((rule): rule is DownloadVisibilityRulePayload => rule !== null);
  if (visibilityRules.length !== candidate.visibilityRules.length) {
    return null;
  }

  const externalUrl =
    typeof candidate.externalUrl === "string"
      ? candidate.externalUrl.trim() || null
      : null;
  const payload: DownloadResourcePayload = {
    categoryId: candidate.categoryId.trim(),
    title: candidate.title.trim(),
    shortDescription: candidate.shortDescription.trim(),
    resourceType:
      candidate.resourceType.trim() as DownloadResourcePayload["resourceType"],
    sourceKind:
      candidate.sourceKind.trim() as DownloadResourcePayload["sourceKind"],
    visibilityMode:
      candidate.visibilityMode.trim() as DownloadResourcePayload["visibilityMode"],
    status: candidate.status.trim() as DownloadResourcePayload["status"],
    externalUrl,
    versionLabel:
      typeof candidate.versionLabel === "string"
        ? candidate.versionLabel.trim() || null
        : null,
    installationInstructions:
      typeof candidate.installationInstructions === "string"
        ? candidate.installationInstructions.trim() || null
        : null,
    displayOrder: Math.trunc(candidate.displayOrder),
    visibilityRules,
  };

  const hasValidExternalUrl =
    payload.externalUrl === null
    || /^https?:\/\/\S+$/i.test(payload.externalUrl);

  return /^[A-Za-z0-9-]{1,100}$/.test(payload.categoryId)
    && payload.title.length >= 2
    && payload.title.length <= 140
    && payload.shortDescription.length >= 2
    && payload.shortDescription.length <= 320
    && DOWNLOAD_RESOURCE_TYPES.includes(payload.resourceType)
    && DOWNLOAD_SOURCE_KINDS.includes(payload.sourceKind)
    && DOWNLOAD_VISIBILITY_MODES.includes(payload.visibilityMode)
    && ["active", "inactive"].includes(payload.status)
    && hasValidExternalUrl
    && (payload.versionLabel === null || payload.versionLabel.length <= 80)
    && (
      payload.installationInstructions === null
      || payload.installationInstructions.length <= 4000
    )
    && Number.isInteger(payload.displayOrder)
    && payload.displayOrder >= 0
    && payload.displayOrder <= 9999
    ? payload
    : null;
}

function parseDownloadVisibilityRulePayload(
  value: unknown,
): DownloadVisibilityRulePayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<DownloadVisibilityRulePayload>;
  if (
    typeof candidate.targetType !== "string"
    || typeof candidate.targetValue !== "string"
  ) {
    return null;
  }

  const payload: DownloadVisibilityRulePayload = {
    targetType:
      candidate.targetType.trim() as DownloadVisibilityRulePayload["targetType"],
    targetValue: candidate.targetValue.trim(),
  };

  return DOWNLOAD_VISIBILITY_TARGET_TYPES.includes(payload.targetType)
    && payload.targetValue.length >= 1
    && payload.targetValue.length <= 160
    ? payload
    : null;
}

export function parseCommercialDocumentPayload(
  value: unknown,
): CommercialDocumentPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<CommercialDocumentPayload>;
  if (
    typeof candidate.customerReference !== "string"
    || typeof candidate.documentType !== "string"
    || typeof candidate.title !== "string"
    || typeof candidate.currency !== "string"
    || typeof candidate.disclaimer !== "string"
    || !(
      typeof candidate.serviceRequestId === "string"
      || candidate.serviceRequestId === null
      || candidate.serviceRequestId === undefined
    )
    || !(
      typeof candidate.status === "string"
      || candidate.status === undefined
    )
  ) {
    return null;
  }

  const payload: CommercialDocumentPayload = {
    customerReference: candidate.customerReference.trim(),
    documentType:
      candidate.documentType as CommercialDocumentPayload["documentType"],
    title: candidate.title.trim(),
    currency: candidate.currency.trim().toUpperCase() as "EUR",
    serviceRequestId:
      typeof candidate.serviceRequestId === "string"
        ? candidate.serviceRequestId.trim() || null
        : null,
    disclaimer: candidate.disclaimer.trim(),
    ...(typeof candidate.status === "string"
      ? { status: candidate.status.trim() as CommercialDocumentPayload["status"] }
      : {}),
  };

  return /^[A-Za-z0-9-]{1,100}$/.test(payload.customerReference)
    && ["quote_draft", "billing_draft", "informational_invoice"].includes(
      payload.documentType,
    )
    && payload.title.length >= 3
    && payload.title.length <= 200
    && payload.currency === "EUR"
    && payload.disclaimer.length >= 10
    && payload.disclaimer.length <= 500
    && (payload.serviceRequestId === null
      || /^[A-Za-z0-9-]{1,100}$/.test(payload.serviceRequestId))
    && (!payload.status
      || ["draft", "pending_review"].includes(payload.status))
    ? payload
    : null;
}

export function parseCommercialDocumentLinePayload(
  value: unknown,
): CommercialDocumentLinePayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<CommercialDocumentLinePayload>;
  if (
    !(
      typeof candidate.offerId === "string"
      || candidate.offerId === null
      || candidate.offerId === undefined
    )
    || typeof candidate.label !== "string"
    || typeof candidate.description !== "string"
    || typeof candidate.quantity !== "number"
    || typeof candidate.unitLabel !== "string"
    || typeof candidate.unitPriceCents !== "number"
    || !(
      typeof candidate.taxRateBasisPoints === "number"
      || candidate.taxRateBasisPoints === null
      || candidate.taxRateBasisPoints === undefined
    )
    || typeof candidate.sortOrder !== "number"
  ) {
    return null;
  }

  const quantity = Number(candidate.quantity);
  const payload: CommercialDocumentLinePayload = {
    offerId:
      typeof candidate.offerId === "string"
        ? candidate.offerId.trim() || null
        : null,
    label: candidate.label.trim(),
    description: candidate.description.trim(),
    quantity,
    unitLabel: candidate.unitLabel.trim(),
    unitPriceCents: Math.trunc(candidate.unitPriceCents),
    taxRateBasisPoints:
      typeof candidate.taxRateBasisPoints === "number"
        ? Math.trunc(candidate.taxRateBasisPoints)
        : null,
    sortOrder: Math.trunc(candidate.sortOrder),
  };

  return Number.isFinite(quantity)
    && quantity > 0
    && Math.round(quantity * 100) === quantity * 100
    && quantity <= 1000000
    && (payload.offerId === null
      || /^[A-Za-z0-9-]{1,100}$/.test(payload.offerId))
    && (payload.label.length === 0
      || (payload.label.length >= 2 && payload.label.length <= 200))
    && payload.description.length <= 1000
    && payload.unitLabel.length <= 40
    && Number.isInteger(payload.unitPriceCents)
    && payload.unitPriceCents >= 0
    && payload.unitPriceCents <= 100000000
    && (payload.taxRateBasisPoints === null
      || (Number.isInteger(payload.taxRateBasisPoints)
        && payload.taxRateBasisPoints >= 0
        && payload.taxRateBasisPoints <= 10000))
    && Number.isInteger(payload.sortOrder)
    && payload.sortOrder >= 0
    && payload.sortOrder <= 100000
    ? payload
    : null;
}

export function parseCustomerAdLinkPayload(
  value: unknown,
): CustomerAdLinkPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<CustomerAdLinkPayload>;
  if (typeof candidate.distinguishedName !== "string") {
    return null;
  }

  const payload: CustomerAdLinkPayload = {
    distinguishedName: candidate.distinguishedName.trim(),
  };

  return payload.distinguishedName.length >= 10
    && payload.distinguishedName.length <= 1000
    ? payload
    : null;
}

export function parseAdUserCreatePayload(
  value: unknown,
): AdUserCreatePayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<AdUserCreatePayload>;
  if (
    typeof candidate.samAccountName !== "string"
    || typeof candidate.displayName !== "string"
  ) {
    return null;
  }

  const payload: AdUserCreatePayload = {
    samAccountName: candidate.samAccountName.trim(),
    displayName: candidate.displayName.trim(),
    givenName:
      typeof candidate.givenName === "string"
        ? candidate.givenName.trim() || null
        : null,
    surname:
      typeof candidate.surname === "string"
        ? candidate.surname.trim() || null
        : null,
    userPrincipalName:
      typeof candidate.userPrincipalName === "string"
        ? candidate.userPrincipalName.trim() || null
        : null,
    description:
      typeof candidate.description === "string"
        ? candidate.description.trim() || null
        : null,
  };

  return /^[A-Za-z0-9._-]{1,64}$/.test(payload.samAccountName)
    && payload.displayName.length >= 3
    && payload.displayName.length <= 200
    && (payload.givenName === null || payload.givenName.length <= 120)
    && (payload.surname === null || payload.surname.length <= 120)
    && (payload.userPrincipalName === null
      || isValidAdUserPrincipalName(payload.userPrincipalName))
    && (payload.description === null || payload.description.length <= 255)
    ? payload
    : null;
}

export function parseAdGroupCreatePayload(
  value: unknown,
): AdGroupCreatePayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<AdGroupCreatePayload>;
  if (
    typeof candidate.samAccountName !== "string"
    || typeof candidate.displayName !== "string"
  ) {
    return null;
  }

  const payload: AdGroupCreatePayload = {
    samAccountName: candidate.samAccountName.trim(),
    displayName: candidate.displayName.trim(),
    description:
      typeof candidate.description === "string"
        ? candidate.description.trim() || null
        : null,
  };

  return /^[A-Za-z0-9._-]{1,64}$/.test(payload.samAccountName)
    && payload.displayName.length >= 3
    && payload.displayName.length <= 200
    && (payload.description === null || payload.description.length <= 255)
    ? payload
    : null;
}

export function parseAdUserRenamePayload(
  value: unknown,
): AdUserRenamePayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<AdUserRenamePayload>;
  if (
    typeof candidate.newSamAccountName !== "string"
    || typeof candidate.newDisplayName !== "string"
  ) {
    return null;
  }

  const payload: AdUserRenamePayload = {
    newSamAccountName: candidate.newSamAccountName.trim(),
    newDisplayName: candidate.newDisplayName.trim(),
    newUserPrincipalName:
      typeof candidate.newUserPrincipalName === "string"
        ? candidate.newUserPrincipalName.trim() || null
        : null,
  };

  return /^[A-Za-z0-9._-]{1,64}$/.test(payload.newSamAccountName)
    && payload.newDisplayName.length >= 3
    && payload.newDisplayName.length <= 200
    && (payload.newUserPrincipalName === null
      || isValidAdUserPrincipalName(payload.newUserPrincipalName))
    ? payload
    : null;
}

export function parseAdUserMovePayload(
  value: unknown,
): AdUserMovePayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<AdUserMovePayload>;
  if (
    typeof candidate.targetCustomerReference !== "string"
    || typeof candidate.targetContainer !== "string"
  ) {
    return null;
  }

  const payload: AdUserMovePayload = {
    targetCustomerReference: candidate.targetCustomerReference.trim(),
    targetContainer:
      candidate.targetContainer as AdUserMovePayload["targetContainer"],
  };

  return /^[A-Za-z0-9-]{1,100}$/.test(payload.targetCustomerReference)
    && (payload.targetContainer === "Users"
      || payload.targetContainer === "Disabled")
    ? payload
    : null;
}

export function parseAdGroupMemberPayload(
  value: unknown,
): AdGroupMemberPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<AdGroupMemberPayload>;
  if (typeof candidate.userSamAccountName !== "string") {
    return null;
  }

  const payload: AdGroupMemberPayload = {
    userSamAccountName: candidate.userSamAccountName.trim(),
  };

  return /^[A-Za-z0-9._-]{1,64}$/.test(payload.userSamAccountName)
    ? payload
    : null;
}

function isValidAdUserPrincipalName(value: string) {
  if (value.length > 255) {
    return false;
  }

  if (/[\p{Cc}]/u.test(value) || /\s/u.test(value)) {
    return false;
  }

  if (!adUserPrincipalNamePattern.test(value)) {
    return false;
  }

  const [, domainPart = ""] = value.split("@", 2);
  return getAllowedAdUserPrincipalNameDomains().includes(
    domainPart.toLowerCase(),
  );
}

function getAllowedAdUserPrincipalNameDomains() {
  const configuredDomains = process.env.AD_ALLOWED_UPN_DOMAINS
    ?.split(",")
    .map((item) => item.trim().toLowerCase())
    .filter(Boolean);

  if (configuredDomains && configuredDomains.length > 0) {
    return configuredDomains;
  }

  const configuredDomain = process.env.AD_DOMAIN?.trim().toLowerCase();
  return [configuredDomain || "home.bzh"];
}
