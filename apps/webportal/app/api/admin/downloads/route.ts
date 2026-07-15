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

export function GET(request: NextRequest) {
  return handleAdminGet<DownloadResource[]>(
    request,
    "/internal/admin/downloads",
  );
}

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
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
    "/internal/admin/downloads",
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
