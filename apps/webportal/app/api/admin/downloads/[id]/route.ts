import type {
  DownloadResource,
  DownloadResourceMutationResponse,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import { parseDownloadResourcePayload } from "@/lib/bff-payloads";
import {
  controlledAdminError,
  handleAdminGet,
  handleAdminMutation,
} from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { isValidPortalIdentifier } from "@/lib/portal-bff";

type RouteContext = { params: Promise<{ id: string }> };

export async function GET(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  if (!isValidPortalIdentifier(id)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le tÃ©lÃ©chargement demandÃ© est invalide.",
      correlationId,
    );
  }

  return handleAdminGet<DownloadResource>(
    request,
    `/internal/admin/downloads/${encodeURIComponent(id)}`,
  );
}

export async function PATCH(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  if (!isValidPortalIdentifier(id)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le tÃ©lÃ©chargement demandÃ© est invalide.",
      correlationId,
    );
  }

  const payload = parseDownloadResourcePayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le tÃ©lÃ©chargement fourni est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<
    typeof payload,
    DownloadResourceMutationResponse
  >(
    request,
    `/internal/admin/downloads/${encodeURIComponent(id)}`,
    "PATCH",
    payload,
  );
}

export async function DELETE(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  if (!isValidPortalIdentifier(id)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le tÃ©lÃ©chargement demandÃ© est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<undefined, DownloadResourceMutationResponse>(
    request,
    `/internal/admin/downloads/${encodeURIComponent(id)}`,
    "DELETE",
    undefined,
  );
}

async function readJson(request: NextRequest) {
  try {
    return await request.json();
  } catch {
    return null;
  }
}
