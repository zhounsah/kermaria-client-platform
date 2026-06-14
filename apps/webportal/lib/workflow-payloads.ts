import type {
  RequestStatusPayload,
  RequestTextPayload,
  RequestType,
  ServiceRequestStatus,
  SupportRequestStatus,
} from "@kermaria/shared";

const supportStatuses = new Set<SupportRequestStatus>([
  "open",
  "in_progress",
  "waiting_for_customer",
  "resolved",
  "closed",
  "cancelled",
]);

const serviceStatuses = new Set<ServiceRequestStatus>([
  "received",
  "under_review",
  "accepted",
  "rejected",
  "cancelled",
  "completed",
]);

export function parseStatusPayload(
  value: unknown,
  requestType: RequestType,
): RequestStatusPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<RequestStatusPayload>;
  if (typeof candidate.status !== "string") {
    return null;
  }

  const status = candidate.status.trim();
  const valid = requestType === "support"
    ? supportStatuses.has(status as SupportRequestStatus)
    : serviceStatuses.has(status as ServiceRequestStatus);

  return valid
    ? { status: status as RequestStatusPayload["status"] }
    : null;
}

export function parseRequestTextPayload(
  value: unknown,
): RequestTextPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<RequestTextPayload>;
  if (typeof candidate.text !== "string") {
    return null;
  }

  const text = candidate.text.trim();
  return text.length >= 3 && text.length <= 2000 ? { text } : null;
}
