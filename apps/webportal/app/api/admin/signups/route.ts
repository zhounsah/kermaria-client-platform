import { NextRequest } from "next/server";

import { controlledAdminError, handleAdminGet } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import type { SignupAdminSummary } from "@/lib/internal-api";

const allowedStatuses = new Set([
  "email_pending",
  "email_verified",
  "approved",
  "rejected",
  "expired",
]);

export async function GET(request: NextRequest) {
  const status = request.nextUrl.searchParams.get("status")?.trim();
  if (status && !allowedStatuses.has(status)) {
    const correlationId = resolveCorrelationId(
      request.headers.get(CORRELATION_HEADER),
    );
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le filtre de statut demandé est invalide.",
      correlationId,
    );
  }

  const suffix = status ? `?status=${encodeURIComponent(status)}` : "";
  return handleAdminGet<SignupAdminSummary[]>(
    request,
    `/internal/admin/signups${suffix}`,
  );
}
