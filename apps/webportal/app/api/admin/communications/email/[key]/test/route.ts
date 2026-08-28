import type { CommunicationTemplateSimpleResponse, EmailTemplateTestPayload } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";
import { invalidCommunicationRequest, isTemplateKey, readJsonBody } from "@/lib/admin-communications-bff";

export async function POST(request: NextRequest, { params }: { params: Promise<{ key: string }> }) {
  const key = (await params).key;
  const body = await readJsonBody(request);
  if (!isTemplateKey(key) || !isPayload(body)) {
    return invalidCommunicationRequest(request);
  }
  // L'API-INTERNAL refuse toute adresse autre que celle de l'administrateur
  // connecte : le BFF ne fait que relayer, il ne choisit pas le destinataire.
  return handleAdminMutation<EmailTemplateTestPayload, CommunicationTemplateSimpleResponse>(
    request,
    `/internal/admin/communications/email/${encodeURIComponent(key)}/test`,
    "POST",
    body,
  );
}

function isPayload(value: unknown): value is EmailTemplateTestPayload {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Partial<EmailTemplateTestPayload>;
  return typeof candidate.recipient === "string" && candidate.recipient.trim().length > 0;
}
