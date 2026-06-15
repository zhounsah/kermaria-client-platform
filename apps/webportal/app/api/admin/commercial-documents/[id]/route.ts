import type {
  AdminCommercialDocumentDetail,
  CommercialDocumentMutationResponse,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import {
  parseCommercialDocumentPayload,
} from "@/lib/bff-payloads";
import {
  controlledAdminError,
  handleAdminGet,
  handleAdminMutation,
} from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

type RouteContext = { params: Promise<{ id: string }> };

export async function GET(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  if (!/^[A-Za-z0-9-]{1,100}$/.test(id)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "L'identifiant demandé est invalide.",
      correlationId,
    );
  }

  return handleAdminGet<AdminCommercialDocumentDetail>(
    request,
    `/internal/admin/commercial-documents/${encodeURIComponent(id)}`,
  );
}

export async function PATCH(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  if (!/^[A-Za-z0-9-]{1,100}$/.test(id)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "L'identifiant demandé est invalide.",
      correlationId,
    );
  }

  const payload = parseCommercialDocumentPayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le document commercial demandé est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<
    typeof payload,
    CommercialDocumentMutationResponse
  >(
    request,
    `/internal/admin/commercial-documents/${encodeURIComponent(id)}`,
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
