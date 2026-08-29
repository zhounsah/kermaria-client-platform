import {
  FISCAL_REGIMES,
  type FiscalMentionCreatePayload,
  type FiscalPolicyMutationResponse,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";
import {
  invalidCommunicationRequest,
  isExpectedVersion,
  readJsonBody,
} from "@/lib/admin-communications-bff";

// Le BFF ne valide que la forme : la date d'effet, les bornes du texte et
// l'interdiction d'antidater sont revalidees par API-INTERNAL, seule autorite.
export async function POST(request: NextRequest) {
  const body = await readJsonBody(request);
  if (!isPayload(body)) {
    return invalidCommunicationRequest(request);
  }
  return handleAdminMutation<
    FiscalMentionCreatePayload,
    FiscalPolicyMutationResponse
  >(request, "/internal/admin/settings/fiscal-policy/mentions", "POST", body);
}

function isPayload(value: unknown): value is FiscalMentionCreatePayload {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Partial<FiscalMentionCreatePayload>;
  return typeof candidate.regime === "string"
    && FISCAL_REGIMES.includes(candidate.regime)
    && typeof candidate.mention === "string"
    && candidate.mention.trim().length > 0
    && typeof candidate.effectiveFrom === "string"
    && candidate.effectiveFrom.trim().length > 0
    && isExpectedVersion(candidate.expectedVersion);
}
