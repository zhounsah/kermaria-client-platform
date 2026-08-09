import type {
  EditorialListResponse,
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

export function GET(request: NextRequest) {
  return handleAdminGet<EditorialListResponse>(
    request,
    `/internal/admin/editorial${request.nextUrl.search}`,
  );
}

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
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
    "/internal/admin/editorial",
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
