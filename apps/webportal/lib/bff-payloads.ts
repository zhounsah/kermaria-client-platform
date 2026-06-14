import type {
  ServiceRequestPayload,
  SupportRequestPayload,
} from "@kermaria/shared";

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
