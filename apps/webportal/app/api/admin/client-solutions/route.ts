import type {
  AdminClientSolutionPortal,
  ClientSolutionMutationResponse,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import { parseClientSolutionPayload } from "@/lib/bff-payloads";
import {
  controlledAdminError,
  handleAdminGet,
  handleAdminMutation,
} from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

export function GET(request: NextRequest) {
  return handleAdminGet<AdminClientSolutionPortal>(
    request,
    "/internal/admin/client-solutions",
  );
}

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const payload = parseClientSolutionPayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La solution fournie est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<typeof payload, ClientSolutionMutationResponse>(
    request,
    "/internal/admin/client-solutions",
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
