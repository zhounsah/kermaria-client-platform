import type { AdGroupMemberPayload, AdMutationResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { parseAdGroupMemberPayload } from "@/lib/bff-payloads";
import { controlledAdminError, handleAdminMutation } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { isValidPortalIdentifier } from "@/lib/portal-bff";

type RouteContext = {
  params: Promise<{ customerReference: string; groupSamAccountName: string }>;
};

export async function POST(request: NextRequest, context: RouteContext) {
  const { customerReference, groupSamAccountName } = await context.params;
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  if (
    !isValidPortalIdentifier(customerReference)
    || !/^[A-Za-z0-9._-]{1,64}$/.test(groupSamAccountName)
  ) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La ressource demandee est invalide.",
      correlationId,
    );
  }

  const payload = parseAdGroupMemberPayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le membre Active Directory demande est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<AdGroupMemberPayload, AdMutationResponse>(
    request,
    `/internal/admin/customers/${encodeURIComponent(customerReference)}/ad/groups/${encodeURIComponent(groupSamAccountName)}/members`,
    "POST",
    payload,
  );
}

async function readJson(request: NextRequest) {
  try {
    return await request.json();
  } catch {
    return null;
  }
}
