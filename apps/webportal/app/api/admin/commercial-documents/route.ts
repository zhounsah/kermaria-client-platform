import type {
  AdminCommercialDocumentSummary,
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

export function GET(request: NextRequest) {
  return handleAdminGet<AdminCommercialDocumentSummary[]>(
    request,
    "/internal/admin/commercial-documents",
  );
}

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
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
    "/internal/admin/commercial-documents",
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
