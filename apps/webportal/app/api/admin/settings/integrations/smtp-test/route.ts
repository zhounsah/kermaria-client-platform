import type {
  IntegrationTestPayload,
  IntegrationTestResponse,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";
import {
  invalidCommunicationRequest,
  readJsonBody,
} from "@/lib/admin-communications-bff";

// Le BFF ne valide que la forme de l'adresse. L'allowlist d'envoi, seule
// barriere qui empeche d'ecrire a un vrai client, est appliquee par
// API-INTERNAL.
export async function POST(request: NextRequest) {
  const body = await readJsonBody(request);
  if (!isPayload(body)) {
    return invalidCommunicationRequest(request);
  }
  return handleAdminMutation<IntegrationTestPayload, IntegrationTestResponse>(
    request,
    "/internal/admin/settings/integrations/smtp/test",
    "POST",
    body,
  );
}

function isPayload(value: unknown): value is IntegrationTestPayload {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Partial<IntegrationTestPayload>;
  return typeof candidate.recipient === "string"
    && candidate.recipient.trim().length >= 5
    && candidate.recipient.includes("@");
}
