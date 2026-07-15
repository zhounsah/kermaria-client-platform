import type { AdminCustomerAdWorkspace } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { controlledAdminError, handleAdminGet } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { isValidPortalIdentifier } from "@/lib/portal-bff";

type RouteContext = { params: Promise<{ customerReference: string }> };

export async function GET(request: NextRequest, context: RouteContext) {
  const { customerReference } = await context.params;
  if (!isValidPortalIdentifier(customerReference)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La ressource demandée est invalide.",
      resolveCorrelationId(request.headers.get(CORRELATION_HEADER)),
    );
  }

  const subscriptionId = request.nextUrl.searchParams.get("subscriptionId");
  if (subscriptionId && !/^[A-Za-z0-9-]{1,100}$/.test(subscriptionId)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le contexte d'abonnement est invalide.",
      resolveCorrelationId(request.headers.get(CORRELATION_HEADER)),
    );
  }

  const query = subscriptionId
    ? `?subscriptionId=${encodeURIComponent(subscriptionId)}`
    : "";
  return handleAdminGet<AdminCustomerAdWorkspace>(
    request,
    `/internal/admin/customers/${encodeURIComponent(customerReference)}/active-directory${query}`,
  );
}
