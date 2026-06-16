import type { AdGroupCreatePayload, AdMutationResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { parseAdGroupCreatePayload } from "@/lib/bff-payloads";
import { controlledAdminError, handleAdminMutation } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { isValidPortalIdentifier } from "@/lib/portal-bff";

type RouteContext = { params: Promise<{ customerReference: string }> };

export async function POST(request: NextRequest, context: RouteContext) {
  const { customerReference } = await context.params;
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  if (!isValidPortalIdentifier(customerReference)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La ressource demandee est invalide.",
      correlationId,
    );
  }

  const payload = parseAdGroupCreatePayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le groupe Active Directory demande est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<AdGroupCreatePayload, AdMutationResponse>(
    request,
    `/internal/admin/customers/${encodeURIComponent(customerReference)}/ad/groups`,
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
