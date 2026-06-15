import type {
  CommercialOfferMutationResponse,
  CommercialOfferSummary,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import {
  parseCommercialOfferPayload,
} from "@/lib/bff-payloads";
import {
  controlledAdminError,
  handleAdminGet,
  handleAdminMutation,
} from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

export function GET(request: NextRequest) {
  return handleAdminGet<CommercialOfferSummary[]>(
    request,
    "/internal/admin/catalog",
  );
}

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const payload = parseCommercialOfferPayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "L'offre demandée est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<
    typeof payload,
    CommercialOfferMutationResponse
  >(
    request,
    "/internal/admin/catalog",
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
