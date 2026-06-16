import type { AdMutationResponse, AdUserCreatePayload } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { parseAdUserCreatePayload } from "@/lib/bff-payloads";
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

  const payload = parseAdUserCreatePayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "L'utilisateur Active Directory demande est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<AdUserCreatePayload, AdMutationResponse>(
    request,
    `/internal/admin/customers/${encodeURIComponent(customerReference)}/ad/users`,
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
