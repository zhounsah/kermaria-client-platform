import { NextRequest } from "next/server";

import {
  controlledAdminError,
  handleAdminMutation,
} from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { parseStatusPayload } from "@/lib/workflow-payloads";

type RouteContext = { params: Promise<{ id: string }> };

export async function PATCH(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const payload = parseStatusPayload(await readJson(request), "support");
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le statut demandé est invalide.",
      correlationId,
    );
  }

  const { id } = await context.params;
  return handleAdminMutation(
    request,
    `/internal/admin/support-requests/${encodeURIComponent(id)}/status`,
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
