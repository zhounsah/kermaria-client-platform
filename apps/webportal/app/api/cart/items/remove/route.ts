import { NextRequest } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { controlledPortalError, handlePortalPayloadMutation } from "@/lib/portal-bff";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  let body: { offerId?: string };
  try {
    body = (await request.json()) as { offerId?: string };
  } catch {
    return controlledPortalError(
      400,
      "INVALID_REQUEST",
      "Le corps de la requête est invalide.",
      correlationId,
    );
  }

  if (!body?.offerId || !/^[A-Za-z0-9-]{1,100}$/.test(body.offerId)) {
    return controlledPortalError(
      400,
      "INVALID_REQUEST",
      "L'identifiant de l'offre est invalide.",
      correlationId,
    );
  }

  return handlePortalPayloadMutation<{ offerId: string }>(
    request,
    "/internal/portal/cart/items/remove",
    { offerId: body.offerId },
  );
}
