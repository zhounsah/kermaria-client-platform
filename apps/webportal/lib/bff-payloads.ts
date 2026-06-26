import type {
  AdGroupCreatePayload,
  AdGroupMemberPayload,
  AdUserCreatePayload,
  CommercialDocumentLinePayload,
  CommercialDocumentPayload,
  CommercialOfferPayload,
  CustomerAdLinkPayload,
  ServiceRequestPayload,
  SupportRequestPayload,
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
    || typeof candidate.status !== "string"
    || typeof candidate.displayOrder !== "number"
    || typeof candidate.billingCadence !== "string"
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
  const payload: CommercialOfferPayload = {
    name: candidate.name.trim(),
    description: candidate.description.trim(),
    category: candidate.category.trim(),
    unitLabel: candidate.unitLabel.trim(),
    priceAmountCents: Math.trunc(candidate.priceAmountCents),
    status: candidate.status as CommercialOfferPayload["status"],
    displayOrder: Math.trunc(candidate.displayOrder),
    billingCadence:
      candidate.billingCadence as CommercialOfferPayload["billingCadence"],
    paypalPlanIdSandbox,
    paypalPlanIdLive,
  };

  const planIdPattern = /^[A-Za-z0-9_-]{1,64}$/;
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
    && (payload.paypalPlanIdSandbox === null
      || planIdPattern.test(payload.paypalPlanIdSandbox))
    && (payload.paypalPlanIdLive === null
      || planIdPattern.test(payload.paypalPlanIdLive))
    && !(
      payload.billingCadence === "one_time"
      && (payload.paypalPlanIdSandbox !== null
        || payload.paypalPlanIdLive !== null)
    )
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
