import type { DownloadCategoryMutationResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { parseDownloadCategoryPayload } from "@/lib/bff-payloads";
import {
  controlledAdminError,
  handleAdminMutation,
} from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { isValidPortalIdentifier } from "@/lib/portal-bff";

type RouteContext = { params: Promise<{ id: string }> };

export async function PATCH(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  if (!isValidPortalIdentifier(id)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La catÃ©gorie demandÃ©e est invalide.",
      correlationId,
    );
  }

  const payload = parseDownloadCategoryPayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La catÃ©gorie fournie est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<
    typeof payload,
    DownloadCategoryMutationResponse
  >(
    request,
    `/internal/admin/download-categories/${encodeURIComponent(id)}`,
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
      "La catÃ©gorie demandÃ©e est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<undefined, DownloadCategoryMutationResponse>(
    request,
    `/internal/admin/download-categories/${encodeURIComponent(id)}`,
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
