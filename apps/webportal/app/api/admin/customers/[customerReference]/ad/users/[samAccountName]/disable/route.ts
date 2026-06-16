import type { AdMutationResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { controlledAdminError, handleAdminMutation } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { isValidPortalIdentifier } from "@/lib/portal-bff";

type RouteContext = {
  params: Promise<{ customerReference: string; samAccountName: string }>;
};

export async function POST(request: NextRequest, context: RouteContext) {
  const { customerReference, samAccountName } = await context.params;
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  if (
    !isValidPortalIdentifier(customerReference)
    || !/^[A-Za-z0-9._-]{1,64}$/.test(samAccountName)
  ) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La ressource demandee est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<Record<string, never>, AdMutationResponse>(
    request,
    `/internal/admin/customers/${encodeURIComponent(customerReference)}/ad/users/${encodeURIComponent(samAccountName)}/disable`,
    "POST",
    {},
  );
}
