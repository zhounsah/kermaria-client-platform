import type {
  ManagedContentDetail,
  ManagedContentMutationResponse,
} from "@kermaria/shared";
import { isManagedContentKey } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { parseManagedContentPayload } from "@/lib/bff-payloads";
import {
  controlledAdminError,
  handleAdminGet,
  handleAdminMutation,
} from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

type RouteContext = { params: Promise<{ key: string }> };

function normalizeManagedContentKey(value: string): string {
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}

export async function GET(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { key } = await context.params;
  const normalizedKey = normalizeManagedContentKey(key);
  if (!isManagedContentKey(normalizedKey)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La clé de contenu demandée est invalide.",
      correlationId,
    );
  }

  return handleAdminGet<ManagedContentDetail>(
    request,
    `/internal/admin/content/${encodeURIComponent(normalizedKey)}`,
  );
}

export async function PATCH(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { key } = await context.params;
  const normalizedKey = normalizeManagedContentKey(key);
  if (!isManagedContentKey(normalizedKey)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La clé de contenu demandée est invalide.",
      correlationId,
    );
  }

  const payload = parseManagedContentPayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le contenu administrable fourni est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<
    typeof payload,
    ManagedContentMutationResponse
  >(
    request,
    `/internal/admin/content/${encodeURIComponent(normalizedKey)}`,
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
