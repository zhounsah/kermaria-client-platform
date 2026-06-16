import type { AdLinkMutationResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { controlledAdminError, handleAdminMutation } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { isValidPortalIdentifier } from "@/lib/portal-bff";

type RouteContext = {
  params: Promise<{ customerReference: string; linkId: string }>;
};

export async function DELETE(request: NextRequest, context: RouteContext) {
  const { customerReference, linkId } = await context.params;
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  if (
    !isValidPortalIdentifier(customerReference)
    || !/^[A-Fa-f0-9-]{36}$/.test(linkId)
  ) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La ressource demandee est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<Record<string, never>, AdLinkMutationResponse>(
    request,
    `/internal/admin/customers/${encodeURIComponent(customerReference)}/ad-links/${encodeURIComponent(linkId)}`,
    "DELETE",
    undefined,
  );
}
