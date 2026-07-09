import type { CheckoutRecurringAddPayload, CheckoutRecurringMutationResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  controlledPortalError,
  handlePortalPayloadMutationTyped,
} from "@/lib/portal-bff";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  let body: CheckoutRecurringAddPayload;
  try {
    body = (await request.json()) as CheckoutRecurringAddPayload;
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

  return handlePortalPayloadMutationTyped<
    CheckoutRecurringMutationResponse,
    CheckoutRecurringAddPayload
  >(
    request,
    "/internal/portal/checkout/subscriptions/items/remove",
    { offerId: body.offerId },
  );
}
