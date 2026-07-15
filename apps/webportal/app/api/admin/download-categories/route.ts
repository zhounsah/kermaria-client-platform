import type {
  DownloadCategory,
  DownloadCategoryMutationResponse,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import { parseDownloadCategoryPayload } from "@/lib/bff-payloads";
import {
  controlledAdminError,
  handleAdminGet,
  handleAdminMutation,
} from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

export function GET(request: NextRequest) {
  return handleAdminGet<DownloadCategory[]>(
    request,
    "/internal/admin/download-categories",
  );
}

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
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
    "/internal/admin/download-categories",
    "POST",
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
