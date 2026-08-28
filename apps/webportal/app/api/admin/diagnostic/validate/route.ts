import type { DiagnosticConfigurationMutationResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";
import {
  invalidCommunicationRequest,
  readJsonBody,
} from "@/lib/admin-communications-bff";

type ValidatePayload = { configuration: unknown };

export async function POST(request: NextRequest) {
  const body = await readJsonBody(request);
  if (!isPayload(body)) {
    return invalidCommunicationRequest(request);
  }
  return handleAdminMutation<ValidatePayload, DiagnosticConfigurationMutationResponse>(
    request,
    "/internal/admin/diagnostic/validate",
    "POST",
    body,
  );
}

function isPayload(value: unknown): value is ValidatePayload {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Partial<ValidatePayload>;
  return typeof candidate.configuration === "object" && candidate.configuration !== null;
}
