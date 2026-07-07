import type {
  PublicPackCatalogContent,
  PublicPackCatalogMutationResponse,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import { parsePublicPackCatalogContentPayload } from "@/lib/bff-payloads";
import {
  controlledAdminError,
  handleAdminGet,
  handleAdminMutation,
} from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

export function GET(request: NextRequest) {
  return handleAdminGet<PublicPackCatalogContent>(
    request,
    "/internal/admin/public-pack-catalog",
  );
}

export async function PATCH(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const payload = parsePublicPackCatalogContentPayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La configuration publique des packs est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<
    typeof payload,
    PublicPackCatalogMutationResponse
  >(
    request,
    "/internal/admin/public-pack-catalog",
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
