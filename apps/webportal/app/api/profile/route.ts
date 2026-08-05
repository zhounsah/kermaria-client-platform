import type {
  PortalProfileUpdatePayload,
  PortalProfileUpdateResponse,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import { parseProfileUpdatePayload } from "@/lib/bff-payloads";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  controlledPortalError,
  handlePortalPayloadMutationTyped,
} from "@/lib/portal-bff";

export const dynamic = "force-dynamic";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const payload = parseProfileUpdatePayload(await readJson(request));
  if (!payload) {
    return controlledPortalError(
      400,
      "INVALID_REQUEST",
      "Vérifiez les coordonnées saisies puis réessayez.",
      correlationId,
    );
  }

  return handlePortalPayloadMutationTyped<
    PortalProfileUpdateResponse,
    PortalProfileUpdatePayload
  >(request, "/internal/portal/profile", payload);
}

async function readJson(request: NextRequest) {
  try {
    return await request.json();
  } catch {
    return null;
  }
}
