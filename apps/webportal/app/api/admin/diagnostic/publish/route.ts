import type {
  DiagnosticConfigurationMutationResponse,
  DiagnosticConfigurationPublishPayload,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";
import {
  invalidCommunicationRequest,
  isExpectedVersion,
  readJsonBody,
} from "@/lib/admin-communications-bff";

export async function POST(request: NextRequest) {
  const body = await readJsonBody(request);
  if (!isPayload(body)) {
    return invalidCommunicationRequest(request);
  }
  return handleAdminMutation<
    DiagnosticConfigurationPublishPayload,
    DiagnosticConfigurationMutationResponse
  >(request, "/internal/admin/diagnostic/publish", "POST", body);
}

function isPayload(value: unknown): value is DiagnosticConfigurationPublishPayload {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Partial<DiagnosticConfigurationPublishPayload>;
  return isExpectedVersion(candidate.expectedDraftVersion)
    && isExpectedVersion(candidate.expectedPublishedVersion);
}
