import type { AdminSupportRequestDetail } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { controlledAdminError, handleAdminGet } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { isValidPortalIdentifier } from "@/lib/portal-bff";

type RouteContext = { params: Promise<{ id: string }> };

export async function GET(request: NextRequest, context: RouteContext) {
  const { id } = await context.params;
  if (!isValidPortalIdentifier(id)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La ressource demandée est invalide.",
      resolveCorrelationId(request.headers.get(CORRELATION_HEADER)),
    );
  }

  return handleAdminGet<AdminSupportRequestDetail>(
    request,
    `/internal/admin/support-requests/${encodeURIComponent(id)}`,
  );
}
