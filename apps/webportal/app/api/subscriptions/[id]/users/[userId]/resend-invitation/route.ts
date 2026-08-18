import "server-only";

import { NextRequest } from "next/server";

import { resolveCorrelationId, CORRELATION_HEADER } from "@/lib/correlation";
import {
  controlledPortalError,
  handlePortalPayloadMutationTyped,
  isValidPortalIdentifier,
} from "@/lib/portal-bff";

export const dynamic = "force-dynamic";

type ResendResponse = {
  code: string;
  message: string;
  correlation_id: string;
};

export async function POST(
  request: NextRequest,
  context: { params: Promise<{ id: string; userId: string }> },
) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id, userId } = await context.params;

  if (!isValidPortalIdentifier(id) || !isValidPortalIdentifier(userId)) {
    return controlledPortalError(
      400,
      "INVALID_REQUEST",
      "Identifiant invalide.",
      correlationId,
    );
  }

  // Aucun corps : la place et le client sont les seules entrées, et le client
  // vient de la session. Renvoyer une invitation n'a rien à recevoir du
  // navigateur.
  return handlePortalPayloadMutationTyped<ResendResponse, undefined>(
    request,
    `/internal/portal/billing-v2/subscriptions/${encodeURIComponent(id)}`
      + `/users/${encodeURIComponent(userId)}/resend-invitation`,
    undefined,
  );
}
