import type {
  AdGroupCreatePayload,
  AdGroupMemberPayload,
  AdUserCreatePayload,
  AdUserMovePayload,
  AdUserRenamePayload,
  CommercialDocumentLinePayload,
  CommercialDocumentPayload,
  ClientSolutionPayload,
  ClientSolutionPortalSettingsPayload,
  DemoAccountCreateRequest,
  DemoProfilePayload,
  DownloadCategoryPayload,
  DownloadResourcePayload,
  DownloadVisibilityRulePayload,
  EditorialCategoryPayload,
  EditorialContentPayload,
  ManagedContentPayload,
  PortalProfileUpdatePayload,
  PublicPackCode,
  PublicPackComparisonValueKind,
  PublicPackCatalogContentPayload,
  CustomerAdLinkPayload,
  BillingV2VpsConfigurationPayload,
  BillingV2VpsManualProvisioningPayload,
  ServiceRequestPayload,
  SupportRequestPayload,
} from "@kermaria/shared";
import {
  CLIENT_SOLUTION_STATUSES,
  DOWNLOAD_RESOURCE_TYPES,
  DOWNLOAD_SOURCE_KINDS,
  DOWNLOAD_VISIBILITY_MODES,
  DOWNLOAD_VISIBILITY_TARGET_TYPES,
  createDefaultPublicPackCatalogContentPayload,
} from "@kermaria/shared";

const adUserPrincipalNamePattern = /^[^\s@]+@[^\s@]+$/;
const editorialSlugPattern = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const editorialTypes = ["wiki_article", "seo_page", "faq"] as const;
const editorialStatuses = ["draft", "published", "archived", "scheduled"] as const;

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

export function parseBillingV2VpsConfigurationPayload(
  value: unknown,
): BillingV2VpsConfigurationPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<BillingV2VpsConfigurationPayload>;
  if (
    typeof candidate.serviceCode !== "string"
    || typeof candidate.tierCode !== "string"
    || typeof candidate.hostname !== "string"
    || typeof candidate.operatingSystem !== "string"
    || typeof candidate.usage !== "string"
    || typeof candidate.managementMode !== "string"
    || typeof candidate.internetExposure !== "string"
    || typeof candidate.comment !== "string"
    || typeof candidate.idempotencyKey !== "string"
  ) {
    return null;
  }

  const payload: BillingV2VpsConfigurationPayload = {
    serviceCode: candidate.serviceCode.trim(),
    tierCode: candidate.tierCode.trim(),
    hostname: candidate.hostname.trim(),
    operatingSystem: candidate.operatingSystem.trim(),
    usage: candidate.usage.trim(),
    managementMode: candidate.managementMode.trim(),
    internetExposure: candidate.internetExposure as BillingV2VpsConfigurationPayload["internetExposure"],
    comment: candidate.comment.trim(),
    idempotencyKey: candidate.idempotencyKey.trim(),
  };

  return payload.serviceCode.length > 0
    && payload.serviceCode.length <= 64
    && payload.tierCode.length > 0
    && payload.tierCode.length <= 64
    && payload.hostname.length > 0
    && payload.hostname.length <= 253
    && payload.operatingSystem.length > 0
    && payload.operatingSystem.length <= 120
    && payload.usage.length > 0
    && payload.usage.length <= 1000
    && payload.managementMode.length > 0
    && payload.managementMode.length <= 120
    && payload.comment.length <= 1000
    && payload.idempotencyKey.length > 0
    && payload.idempotencyKey.length <= 128
    && ["yes", "no", "to_confirm"].includes(payload.internetExposure)
    ? payload
    : null;
}

export function parseBillingV2VpsManualProvisioningPayload(
  value: unknown,
): BillingV2VpsManualProvisioningPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<BillingV2VpsManualProvisioningPayload>;
  if (
    typeof candidate.infrastructureTarget !== "string"
    || typeof candidate.instanceReference !== "string"
    || typeof candidate.publicIpAddress !== "string"
    || typeof candidate.operationalNotes !== "string"
  ) {
    return null;
  }

  const payload: BillingV2VpsManualProvisioningPayload = {
    infrastructureTarget: candidate.infrastructureTarget.trim(),
    instanceReference: candidate.instanceReference.trim(),
    publicIpAddress: candidate.publicIpAddress.trim(),
    operationalNotes: candidate.operationalNotes.trim(),
  };

  return payload.infrastructureTarget.length > 0
    && payload.infrastructureTarget.length <= 255
    && payload.instanceReference.length > 0
    && payload.instanceReference.length <= 255
    && payload.publicIpAddress.length <= 45
    && payload.operationalNotes.length <= 2000
    ? payload
    : null;
}

/** Coordonnées corrigées par le client depuis son espace. Les bornes reprennent
 *  celles appliquées par l'API interne, pour refuser au plus tôt une saisie que
 *  la base tronquerait. */
export function parseProfileUpdatePayload(
  value: unknown,
): PortalProfileUpdatePayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<PortalProfileUpdatePayload>;
  if (
    typeof candidate.contactName !== "string"
    || typeof candidate.phone !== "string"
    || typeof candidate.address !== "string"
    || typeof candidate.city !== "string"
    || typeof candidate.country !== "string"
  ) {
    return null;
  }

  const payload: PortalProfileUpdatePayload = {
    contactName: candidate.contactName.trim(),
    phone: candidate.phone.trim(),
    address: candidate.address.trim(),
    city: candidate.city.trim(),
    country: candidate.country.trim(),
  };

  return payload.contactName.length >= 2
    && payload.contactName.length <= 200
    && payload.phone.length <= 40
    && payload.address.length <= 255
    && payload.city.length <= 160
    && payload.country.length <= 100
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

export function parseEditorialContentPayload(
  value: unknown,
): EditorialContentPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<EditorialContentPayload>;
  if (
    typeof candidate.contentType !== "string"
    || typeof candidate.title !== "string"
    || typeof candidate.slug !== "string"
    || typeof candidate.bodyMarkdown !== "string"
    || typeof candidate.status !== "string"
    || typeof candidate.noIndex !== "boolean"
    || typeof candidate.sortOrder !== "number"
    || !(
      typeof candidate.summary === "string"
      || candidate.summary === null
      || candidate.summary === undefined
    )
    || !(
      typeof candidate.categoryId === "string"
      || candidate.categoryId === null
      || candidate.categoryId === undefined
    )
    || !(
      typeof candidate.seoTitle === "string"
      || candidate.seoTitle === null
      || candidate.seoTitle === undefined
    )
    || !(
      typeof candidate.seoDescription === "string"
      || candidate.seoDescription === null
      || candidate.seoDescription === undefined
    )
    || !(
      typeof candidate.canonicalUrl === "string"
      || candidate.canonicalUrl === null
      || candidate.canonicalUrl === undefined
    )
    || !Array.isArray(candidate.faqScopes)
    || !candidate.faqScopes.every((scope) => typeof scope === "string")
  ) {
    return null;
  }

  const payload: EditorialContentPayload = {
    contentType: candidate.contentType.trim() as EditorialContentPayload["contentType"],
    title: candidate.title.trim(),
    slug: candidate.slug.trim().toLowerCase(),
    summary:
      typeof candidate.summary === "string"
        ? candidate.summary.trim() || null
        : null,
    bodyMarkdown: candidate.bodyMarkdown.trim(),
    categoryId:
      typeof candidate.categoryId === "string"
        ? candidate.categoryId.trim() || null
        : null,
    status: candidate.status.trim() as EditorialContentPayload["status"],
    seoTitle:
      typeof candidate.seoTitle === "string"
        ? candidate.seoTitle.trim() || null
        : null,
    seoDescription:
      typeof candidate.seoDescription === "string"
        ? candidate.seoDescription.trim() || null
        : null,
    canonicalUrl:
      typeof candidate.canonicalUrl === "string"
        ? candidate.canonicalUrl.trim() || null
        : null,
    noIndex: candidate.noIndex,
    sortOrder: Math.trunc(candidate.sortOrder),
    faqScopes: Array.from(new Set(
      candidate.faqScopes
        .map((scope) => scope.trim().toLowerCase())
        .filter(Boolean),
    )),
  };

  return editorialTypes.includes(payload.contentType)
    && payload.title.length >= 2
    && payload.title.length <= 220
    && editorialSlugPattern.test(payload.slug)
    && payload.slug.length <= 120
    && (payload.summary === null || payload.summary.length <= 600)
    && payload.bodyMarkdown.length <= 160000
    && editorialStatuses.includes(payload.status)
    && (payload.categoryId === null || /^[A-Za-z0-9-]{1,100}$/.test(payload.categoryId))
    && (payload.seoTitle === null || payload.seoTitle.length <= 220)
    && (payload.seoDescription === null || payload.seoDescription.length <= 320)
    && (payload.canonicalUrl === null || isAbsoluteWebUrl(payload.canonicalUrl))
    && payload.sortOrder >= 0
    && payload.sortOrder <= 100000
    && payload.faqScopes.length <= 20
    && payload.faqScopes.every((scope) =>
      editorialSlugPattern.test(scope) && scope.length <= 80
    )
    ? payload
    : null;
}

export function parseEditorialCategoryPayload(
  value: unknown,
): EditorialCategoryPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<EditorialCategoryPayload>;
  if (
    typeof candidate.contentType !== "string"
    || typeof candidate.name !== "string"
    || typeof candidate.slug !== "string"
    || typeof candidate.sortOrder !== "number"
    || !(
      typeof candidate.description === "string"
      || candidate.description === null
      || candidate.description === undefined
    )
  ) {
    return null;
  }

  const payload: EditorialCategoryPayload = {
    contentType: candidate.contentType.trim() as EditorialCategoryPayload["contentType"],
    name: candidate.name.trim(),
    slug: candidate.slug.trim().toLowerCase(),
    description:
      typeof candidate.description === "string"
        ? candidate.description.trim() || null
        : null,
    sortOrder: Math.trunc(candidate.sortOrder),
  };

  return editorialTypes.includes(payload.contentType)
    && payload.name.length >= 2
    && payload.name.length <= 160
    && editorialSlugPattern.test(payload.slug)
    && payload.slug.length <= 100
    && (payload.description === null || payload.description.length <= 500)
    && payload.sortOrder >= 0
    && payload.sortOrder <= 100000
    ? payload
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

const clientSolutionSlugPattern = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

function isAbsoluteWebUrl(value: string): boolean {
  try {
    const url = new URL(value);
    return (
      (url.protocol === "http:" || url.protocol === "https:")
      && !url.username
      && !url.password
    );
  } catch {
    return false;
  }
}

export function parseClientSolutionPayload(
  value: unknown,
): ClientSolutionPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<ClientSolutionPayload>;
  if (
    typeof candidate.title !== "string"
    || typeof candidate.targetUrl !== "string"
    || typeof candidate.status !== "string"
    || typeof candidate.opensInNewTab !== "boolean"
    || typeof candidate.displayOrder !== "number"
    || !(
      typeof candidate.slug === "string"
      || candidate.slug === null
      || candidate.slug === undefined
    )
    || !(
      typeof candidate.tagline === "string"
      || candidate.tagline === null
      || candidate.tagline === undefined
    )
  ) {
    return null;
  }

  const payload: ClientSolutionPayload = {
    slug:
      typeof candidate.slug === "string"
        ? candidate.slug.trim().toLowerCase() || null
        : null,
    title: candidate.title.trim(),
    tagline:
      typeof candidate.tagline === "string"
        ? candidate.tagline.trim() || null
        : null,
    targetUrl: candidate.targetUrl.trim(),
    opensInNewTab: candidate.opensInNewTab,
    status: candidate.status.trim() as ClientSolutionPayload["status"],
    displayOrder: Math.trunc(candidate.displayOrder),
  };

  return payload.title.length >= 2
    && payload.title.length <= 120
    && (payload.tagline === null || payload.tagline.length <= 280)
    && (
      payload.slug === null
      || (
        payload.slug.length >= 2
        && payload.slug.length <= 80
        && clientSolutionSlugPattern.test(payload.slug)
      )
    )
    && payload.targetUrl.length <= 2048
    && isAbsoluteWebUrl(payload.targetUrl)
    && CLIENT_SOLUTION_STATUSES.includes(payload.status)
    && Number.isInteger(payload.displayOrder)
    && payload.displayOrder >= 0
    && payload.displayOrder <= 9999
    ? payload
    : null;
}

export function parseClientSolutionPortalSettingsPayload(
  value: unknown,
): ClientSolutionPortalSettingsPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<ClientSolutionPortalSettingsPayload>;
  if (typeof candidate.title !== "string") {
    return null;
  }

  const optional = (input: unknown) =>
    typeof input === "string" ? input.trim() || null : null;
  const payload: ClientSolutionPortalSettingsPayload = {
    eyebrow: optional(candidate.eyebrow),
    title: candidate.title.trim(),
    description: optional(candidate.description),
    footerNote: optional(candidate.footerNote),
  };

  return payload.title.length >= 2
    && payload.title.length <= 160
    && (payload.eyebrow === null || payload.eyebrow.length <= 120)
    && (payload.description === null || payload.description.length <= 600)
    && (payload.footerNote === null || payload.footerNote.length <= 600)
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
    typeof candidate.label !== "string"
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
    && payload.label.length >= 2
    && payload.label.length <= 200
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

export function parseDemoAccountCreateRequest(
  value: unknown,
): DemoAccountCreateRequest | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<DemoAccountCreateRequest>;
  if (
    typeof candidate.profileKey !== "string"
    || typeof candidate.displayName !== "string"
    || typeof candidate.email !== "string"
    || typeof candidate.initialPassword !== "string"
  ) {
    return null;
  }

  const profileKey = candidate.profileKey.trim().toLowerCase();
  const displayName = candidate.displayName.trim();
  const email = candidate.email.trim().toLowerCase();
  const initialPassword = candidate.initialPassword;
  const userDisplayName =
    typeof candidate.userDisplayName === "string"
      ? candidate.userDisplayName.trim() || null
      : null;
  const lifetimeDaysOverride =
    typeof candidate.lifetimeDaysOverride === "number"
      ? Math.trunc(candidate.lifetimeDaysOverride)
      : null;

  let selectedServiceNames: string[] | null = null;
  if (Array.isArray(candidate.selectedServiceNames)) {
    if (!candidate.selectedServiceNames.every((name) => typeof name === "string")) {
      return null;
    }
    selectedServiceNames = candidate.selectedServiceNames
      .map((name) => name.trim())
      .filter((name) => name.length > 0 && name.length <= 200)
      .slice(0, 50);
  }

  const personalTitleRaw =
    typeof candidate.personalTitle === "string"
      ? candidate.personalTitle.trim().toLowerCase()
      : "";
  const personalTitle =
    personalTitleRaw === "madame" || personalTitleRaw === "monsieur"
      ? personalTitleRaw
      : null;
  const givenName =
    typeof candidate.givenName === "string"
      ? candidate.givenName.trim() || null
      : null;
  const surname =
    typeof candidate.surname === "string"
      ? candidate.surname.trim() || null
      : null;
  const birthDateRaw =
    typeof candidate.birthDate === "string" ? candidate.birthDate.trim() : "";
  const birthDate = /^\d{4}-\d{2}-\d{2}$/.test(birthDateRaw)
    ? birthDateRaw
    : null;

  const isValid =
    profileKey.length > 0
    && profileKey.length <= 64
    && displayName.length >= 2
    && displayName.length <= 200
    && email.length >= 3
    && email.length <= 254
    && email.includes("@")
    && initialPassword.length >= 8
    && initialPassword.length <= 200
    && (userDisplayName === null || userDisplayName.length <= 200)
    && (givenName === null || givenName.length <= 100)
    && (surname === null || surname.length <= 100)
    && (lifetimeDaysOverride === null
      || (Number.isInteger(lifetimeDaysOverride)
        && lifetimeDaysOverride >= 0
        && lifetimeDaysOverride <= 365));

  if (!isValid) {
    return null;
  }

  return {
    profileKey,
    displayName,
    email,
    initialPassword,
    userDisplayName,
    lifetimeDaysOverride,
    selectedServiceNames,
    personalTitle,
    givenName,
    surname,
    birthDate,
  };
}

const DEMO_KINDS = ["showcase", "trial"] as const;
const DEMO_MODES = ["off", "mock", "real_scoped"] as const;
const DEMO_SIMPLE_MODES = ["off", "internal_only", "fake", "native"] as const;
const DEMO_STATUSES = ["active", "inactive"] as const;

export function parseDemoProfilePayload(
  value: unknown,
): DemoProfilePayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<DemoProfilePayload>;
  if (
    typeof candidate.key !== "string"
    || typeof candidate.label !== "string"
    || typeof candidate.kind !== "string"
  ) {
    return null;
  }

  const key = candidate.key.trim().toLowerCase();
  const label = candidate.label.trim();
  const kind = candidate.kind.trim().toLowerCase();
  if (!DEMO_KINDS.includes(kind as (typeof DEMO_KINDS)[number])) {
    return null;
  }

  const optionalString = (input: unknown) =>
    typeof input === "string" && input.trim().length > 0
      ? input.trim()
      : null;
  const optionalMode = (input: unknown, allowed: readonly string[]) => {
    const normalized = optionalString(input)?.toLowerCase() ?? null;
    if (normalized !== null && !allowed.includes(normalized)) {
      return undefined;
    }
    return normalized;
  };

  const adProvisioningMode = optionalMode(candidate.adProvisioningMode, DEMO_MODES);
  const rdsSessionMode = optionalMode(candidate.rdsSessionMode, DEMO_SIMPLE_MODES);
  const status = optionalMode(candidate.status, DEMO_STATUSES);
  if (
    adProvisioningMode === undefined
    || rdsSessionMode === undefined
    || status === undefined
  ) {
    return null;
  }

  const adGroups = Array.isArray(candidate.adGroups)
    ? candidate.adGroups
        .filter((group): group is string => typeof group === "string")
        .map((group) => group.trim())
        .filter((group) => group.length > 0 && group.length <= 64)
        .slice(0, 20)
    : null;

  const lifetimeDays =
    typeof candidate.lifetimeDays === "number"
      ? Math.trunc(candidate.lifetimeDays)
      : null;
  const storageQuotaGo =
    typeof candidate.storageQuotaGo === "number"
      ? Math.trunc(candidate.storageQuotaGo)
      : null;

  const isValid =
    key.length > 0
    && key.length <= 64
    && /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(key)
    && label.length >= 2
    && label.length <= 200
    && (lifetimeDays === null
      || (Number.isInteger(lifetimeDays)
        && lifetimeDays >= 0
        && lifetimeDays <= 365))
    && (storageQuotaGo === null
      || (Number.isInteger(storageQuotaGo)
        && storageQuotaGo >= 0
        && storageQuotaGo <= 100000));

  if (!isValid) {
    return null;
  }

  return {
    key,
    label,
    kind: kind as DemoProfilePayload["kind"],
    contentTemplateKey: optionalString(candidate.contentTemplateKey),
    emailMode: optionalString(candidate.emailMode),
    bpceMode: optionalString(candidate.bpceMode),
    paymentMode: optionalString(candidate.paymentMode),
    adProvisioningMode,
    adGroups,
    storageQuotaGo,
    rdsSessionMode,
    lifetimeDays,
    status,
  };
}
