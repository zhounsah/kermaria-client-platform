import type { SystemSnippetMutationResponse, SystemSnippetUpdatePayload } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";
import { invalidCommunicationRequest, isExpectedVersion, isTemplateKey, readJsonBody } from "@/lib/admin-communications-bff";

export async function PATCH(request: NextRequest, { params }: { params: Promise<{ key: string }> }) {
  const key = (await params).key;
  const body = await readJsonBody(request);
  if (!isTemplateKey(key) || !isPayload(body)) {
    return invalidCommunicationRequest(request);
  }
  return handleAdminMutation<SystemSnippetUpdatePayload, SystemSnippetMutationResponse>(
    request,
    `/internal/admin/communications/snippet/${encodeURIComponent(key)}`,
    "PATCH",
    body,
  );
}

function isPayload(value: unknown): value is SystemSnippetUpdatePayload {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Partial<SystemSnippetUpdatePayload>;
  return typeof candidate.body === "string" && isExpectedVersion(candidate.expectedVersion);
}
