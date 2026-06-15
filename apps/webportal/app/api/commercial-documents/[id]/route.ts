import type { CommercialDocumentDetail } from "@kermaria/shared";
import { NextRequest } from "next/server";

import {
  controlledPortalError,
  handlePortalGet,
  isValidPortalIdentifier,
} from "@/lib/portal-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

type RouteContext = { params: Promise<{ id: string }> };

export async function GET(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  if (!isValidPortalIdentifier(id)) {
    return controlledPortalError(
      400,
      "INVALID_REQUEST",
      "L'identifiant demandé est invalide.",
      correlationId,
    );
  }

  return handlePortalGet<CommercialDocumentDetail>(
    request,
    `/internal/portal/commercial-documents/${encodeURIComponent(id)}`,
  );
}
