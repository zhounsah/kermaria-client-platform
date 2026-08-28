import type { NotificationTemplateMutationResponse, NotificationTemplateUpdatePayload } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";
import { invalidCommunicationRequest, isExpectedVersion, isTemplateKey, readJsonBody } from "@/lib/admin-communications-bff";

export async function PATCH(request: NextRequest, { params }: { params: Promise<{ key: string }> }) {
  const key = (await params).key;
  const body = await readJsonBody(request);
  if (!isTemplateKey(key) || !isPayload(body)) {
    return invalidCommunicationRequest(request);
  }
  return handleAdminMutation<NotificationTemplateUpdatePayload, NotificationTemplateMutationResponse>(
    request,
    `/internal/admin/communications/notification/${encodeURIComponent(key)}`,
    "PATCH",
    body,
  );
}

function isPayload(value: unknown): value is NotificationTemplateUpdatePayload {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Partial<NotificationTemplateUpdatePayload>;
  return typeof candidate.title === "string"
    && typeof candidate.message === "string"
    && typeof candidate.enabled === "boolean"
    && isExpectedVersion(candidate.expectedVersion);
}
