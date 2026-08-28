import type { ApplicationSettingMutationResponse, ApplicationSettingUpdatePayload } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { controlledAdminError, handleAdminMutation } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

export async function PATCH(request: NextRequest, { params }: { params: Promise<{ key: string }> }) {
  const correlationId = resolveCorrelationId(request.headers.get(CORRELATION_HEADER));
  const key = (await params).key;
  let body: unknown = null;
  try { body = await request.json(); } catch { /* invalid payload below */ }
  if (!/^[a-z][a-z0-9_]{1,119}$/.test(key) || !isPayload(body)) {
    return controlledAdminError(400, "INVALID_REQUEST", "La modification demandée est invalide.", correlationId);
  }
  return handleAdminMutation<ApplicationSettingUpdatePayload, ApplicationSettingMutationResponse>(request, `/internal/admin/settings/${encodeURIComponent(key)}`, "PATCH", body);
}

function isPayload(value: unknown): value is ApplicationSettingUpdatePayload {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Partial<ApplicationSettingUpdatePayload>;
  return (typeof candidate.value === "string" || typeof candidate.value === "number" || typeof candidate.value === "boolean")
    && typeof candidate.expectedVersion === "number"
    && Number.isInteger(candidate.expectedVersion) && candidate.expectedVersion >= 0;
}
