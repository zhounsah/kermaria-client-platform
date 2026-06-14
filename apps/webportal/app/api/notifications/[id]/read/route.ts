import { NextRequest } from "next/server";

import {
  handlePortalMutation,
  isValidPortalIdentifier,
} from "@/lib/portal-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

type RouteContext = { params: Promise<{ id: string }> };

export async function POST(request: NextRequest, context: RouteContext) {
  const { id } = await context.params;
  if (!isValidPortalIdentifier(id)) {
    const correlationId = resolveCorrelationId(
      request.headers.get(CORRELATION_HEADER),
    );
    return Response.json(
      {
        code: "INVALID_REQUEST",
        message: "La notification demandée est invalide.",
        correlation_id: correlationId,
      },
      {
        status: 400,
        headers: { [CORRELATION_HEADER]: correlationId },
      },
    );
  }

  return handlePortalMutation(
    request,
    `/internal/portal/notifications/${encodeURIComponent(id)}/read`,
  );
}
