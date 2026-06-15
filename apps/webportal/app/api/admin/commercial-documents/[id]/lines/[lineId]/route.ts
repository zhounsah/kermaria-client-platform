import type { CommercialDocumentLineMutationResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import {
  parseCommercialDocumentLinePayload,
} from "@/lib/bff-payloads";
import {
  controlledAdminError,
  handleAdminMutation,
} from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

type RouteContext = { params: Promise<{ id: string; lineId: string }> };

export async function PATCH(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id, lineId } = await context.params;
  if (
    !/^[A-Za-z0-9-]{1,100}$/.test(id)
    || !/^[A-Za-z0-9-]{1,100}$/.test(lineId)
  ) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "L'identifiant demandé est invalide.",
      correlationId,
    );
  }

  const payload = parseCommercialDocumentLinePayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La ligne demandée est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<
    typeof payload,
    CommercialDocumentLineMutationResponse
  >(
    request,
    `/internal/admin/commercial-documents/${encodeURIComponent(id)}/lines/${encodeURIComponent(lineId)}`,
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
