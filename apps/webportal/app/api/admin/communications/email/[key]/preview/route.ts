import type { EmailTemplatePreviewPayload, EmailTemplatePreviewResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";
import { invalidCommunicationRequest, isTemplateKey, readJsonBody } from "@/lib/admin-communications-bff";

export async function POST(request: NextRequest, { params }: { params: Promise<{ key: string }> }) {
  const key = (await params).key;
  const body = await readJsonBody(request);
  if (!isTemplateKey(key) || !isPayload(body)) {
    return invalidCommunicationRequest(request);
  }
  return handleAdminMutation<EmailTemplatePreviewPayload, EmailTemplatePreviewResponse>(
    request,
    `/internal/admin/communications/email/${encodeURIComponent(key)}/preview`,
    "POST",
    body,
  );
}

function isPayload(value: unknown): value is EmailTemplatePreviewPayload {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Partial<EmailTemplatePreviewPayload>;
  return typeof candidate.subject === "string" && typeof candidate.body === "string";
}
