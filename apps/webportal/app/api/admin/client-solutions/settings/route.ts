import type { ClientSolutionPortalMutationResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { parseClientSolutionPortalSettingsPayload } from "@/lib/bff-payloads";
import { controlledAdminError, handleAdminMutation } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

export async function PATCH(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const payload = parseClientSolutionPortalSettingsPayload(
    await readJson(request),
  );
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "L'en-tête de page fourni est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<
    typeof payload,
    ClientSolutionPortalMutationResponse
  >(
    request,
    "/internal/admin/client-solutions/settings",
    "PATCH",
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
