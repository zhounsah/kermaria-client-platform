import type { EmailTemplateMutationResponse, EmailTemplateUpdatePayload } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";
import { invalidCommunicationRequest, isExpectedVersion, isTemplateKey, readJsonBody } from "@/lib/admin-communications-bff";

export async function PATCH(request: NextRequest, { params }: { params: Promise<{ key: string }> }) {
  const key = (await params).key;
  const body = await readJsonBody(request);
  if (!isTemplateKey(key) || !isPayload(body)) {
    return invalidCommunicationRequest(request);
  }
  return handleAdminMutation<EmailTemplateUpdatePayload, EmailTemplateMutationResponse>(
    request,
    `/internal/admin/communications/email/${encodeURIComponent(key)}`,
    "PATCH",
    body,
  );
}

function isPayload(value: unknown): value is EmailTemplateUpdatePayload {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Partial<EmailTemplateUpdatePayload>;
  return typeof candidate.subject === "string"
    && typeof candidate.body === "string"
    && typeof candidate.enabled === "boolean"
    && isExpectedVersion(candidate.expectedVersion);
}
