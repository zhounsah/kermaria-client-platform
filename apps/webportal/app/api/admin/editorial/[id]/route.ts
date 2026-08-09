import type {
  EditorialContentDetail,
  EditorialMutationResponse,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import { parseEditorialContentPayload } from "@/lib/bff-payloads";
import {
  controlledAdminError,
  handleAdminGet,
  handleAdminMutation,
} from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

type RouteContext = { params: Promise<{ id: string }> };

export async function GET(request: NextRequest, context: RouteContext) {
  const { id } = await context.params;
  return handleAdminGet<EditorialContentDetail>(
    request,
    `/internal/admin/editorial/${encodeURIComponent(id)}`,
  );
}

export async function PATCH(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  const payload = parseEditorialContentPayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le contenu editorial fourni est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<typeof payload, EditorialMutationResponse>(
    request,
    `/internal/admin/editorial/${encodeURIComponent(id)}`,
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
