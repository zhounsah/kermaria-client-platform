import "server-only";

import type {
  CommunicationTemplateRestorePayload,
  CommunicationTemplateRevisionsResponse,
  CommunicationTemplateScope,
} from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { controlledAdminError, handleAdminGet, handleAdminMutation } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

// Les cles de gabarit sont fermees cote API-INTERNAL. Le BFF ne fait donc que
// verifier la forme lexicale avant de relayer : le point est autorise parce que
// les notifications sont nommees `{type}.{statut}`.
const KEY_PATTERN = /^[a-z][a-z0-9_.]{1,119}$/;

export function isTemplateKey(key: string) {
  return KEY_PATTERN.test(key);
}

export function invalidCommunicationRequest(request: NextRequest) {
  return controlledAdminError(
    400,
    "INVALID_REQUEST",
    "La modification de gabarit demandée est invalide.",
    resolveCorrelationId(request.headers.get(CORRELATION_HEADER)),
  );
}

export async function readJsonBody(request: NextRequest): Promise<unknown> {
  try {
    return await request.json();
  } catch {
    return null;
  }
}

export function isExpectedVersion(value: unknown): value is number {
  return typeof value === "number" && Number.isInteger(value) && value >= 0;
}

export function isRestorePayload(
  value: unknown,
): value is CommunicationTemplateRestorePayload {
  if (!value || typeof value !== "object") return false;
  return isExpectedVersion((value as { expectedVersion?: unknown }).expectedVersion);
}

/** Relaie une restauration de gabarit vers sa route API-INTERNAL. */
export async function handleTemplateRestore(
  request: NextRequest,
  scope: CommunicationTemplateScope,
  key: string,
): Promise<NextResponse> {
  const body = await readJsonBody(request);
  if (!isTemplateKey(key) || !isRestorePayload(body)) {
    return invalidCommunicationRequest(request);
  }
  return handleAdminMutation<CommunicationTemplateRestorePayload, unknown>(
    request,
    `/internal/admin/communications/${scope}/${encodeURIComponent(key)}/restore-default`,
    "POST",
    body,
  );
}

/** Relaie la lecture de l'historique d'un gabarit. */
export function handleTemplateRevisions(
  request: NextRequest,
  scope: CommunicationTemplateScope,
  key: string,
): Promise<NextResponse> | NextResponse {
  if (!isTemplateKey(key)) {
    return invalidCommunicationRequest(request);
  }
  return handleAdminGet<CommunicationTemplateRevisionsResponse>(
    request,
    `/internal/admin/communications/${scope}/${encodeURIComponent(key)}/revisions`,
  );
}
