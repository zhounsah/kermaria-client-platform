import { NextRequest } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  controlledPortalError,
  handlePortalPayloadMutation,
  isValidPortalIdentifier,
} from "@/lib/portal-bff";
import { parseRequestTextPayload } from "@/lib/workflow-payloads";

type RouteContext = { params: Promise<{ id: string }> };

export async function POST(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  const payload = parseRequestTextPayload(await readJson(request));
  if (!isValidPortalIdentifier(id) || !payload) {
    return controlledPortalError(
      400,
      "INVALID_REQUEST",
      "La réponse doit contenir entre 3 et 2 000 caractères.",
      correlationId,
    );
  }

  return handlePortalPayloadMutation(
    request,
    `/internal/portal/service-requests/${encodeURIComponent(id)}/messages`,
    payload,
  );
}

async function readJson(request: NextRequest) {
  try {
    return await request.json();
  } catch {
    return null;
  }
}
