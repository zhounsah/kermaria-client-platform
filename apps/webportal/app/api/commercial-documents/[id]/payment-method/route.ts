import type { CommercialDocumentMutationResponse } from "@/lib/commercial-document-api";
import { NextRequest } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  controlledPortalError,
  handlePortalPayloadMutationTyped,
  isValidPortalIdentifier,
} from "@/lib/portal-bff";

type RouteContext = { params: Promise<{ id: string }> };

type PaymentMethodSelectionPayload = {
  paymentMethod?: string;
};

export async function POST(request: NextRequest, context: RouteContext) {
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

  let body: PaymentMethodSelectionPayload;
  try {
    body = (await request.json()) as PaymentMethodSelectionPayload;
  } catch {
    return controlledPortalError(
      400,
      "INVALID_REQUEST",
      "Le corps de la requête est invalide.",
      correlationId,
    );
  }

  if (body.paymentMethod !== "manual") {
    return controlledPortalError(
      400,
      "INVALID_REQUEST",
      "Le mode de règlement demandé est invalide.",
      correlationId,
    );
  }

  return handlePortalPayloadMutationTyped<
    CommercialDocumentMutationResponse,
    PaymentMethodSelectionPayload
  >(
    request,
    `/internal/portal/commercial-documents/${encodeURIComponent(id)}/payment-method`,
    { paymentMethod: "manual" },
  );
}
