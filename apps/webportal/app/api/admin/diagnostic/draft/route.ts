import type {
  DiagnosticConfigurationMutationResponse,
  DiagnosticConfigurationUpdatePayload,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";
import {
  invalidCommunicationRequest,
  isExpectedVersion,
  readJsonBody,
} from "@/lib/admin-communications-bff";

// Le BFF ne valide que la forme de l'enveloppe : la DSL elle-meme est validee
// par le registre ferme d'API-INTERNAL, seule autorite.
export async function PUT(request: NextRequest) {
  const body = await readJsonBody(request);
  if (!isPayload(body)) {
    return invalidCommunicationRequest(request);
  }
  return handleAdminMutation<
    DiagnosticConfigurationUpdatePayload,
    DiagnosticConfigurationMutationResponse
  >(request, "/internal/admin/diagnostic/draft", "PUT", body);
}

function isPayload(value: unknown): value is DiagnosticConfigurationUpdatePayload {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Partial<DiagnosticConfigurationUpdatePayload>;
  return typeof candidate.configuration === "object"
    && candidate.configuration !== null
    && isExpectedVersion(candidate.expectedVersion);
}
