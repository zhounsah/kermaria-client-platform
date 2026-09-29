import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { controlledAdminError } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { hasValidCsrfToken } from "@/lib/csrf-server";
import {
  getInternalApiError,
  getInternalSession,
  mutateInternalAdminData,
} from "@/lib/internal-api";
import { getSessionCookieName } from "@/lib/session-config";

export async function POST(
  request: NextRequest,
  context: { params: Promise<{ id: string }> },
) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;

  if (!/^[A-Za-z0-9-]{1,100}$/.test(id)) {
    return NextResponse.json(
      { code: "INVALID_REQUEST", message: "Identifiant invalide." },
      { status: 400 },
    );
  }

  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  if (!sessionToken) {
    return NextResponse.json(
      { code: "UNAUTHORIZED", message: "Session requise." },
      { status: 401 },
    );
  }

  if (!hasValidCsrfToken(request)) {
    return controlledAdminError(
      403,
      "CSRF_FORBIDDEN",
      "La requete d administration doit etre confirmee par un jeton CSRF valide.",
      correlationId,
    );
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "internal_admin") {
      return NextResponse.json(
        { code: "ACCESS_DENIED", message: "Acces refuse." },
        { status: 403 },
      );
    }
  } catch {
    return NextResponse.json(
      { code: "SESSION_INVALID", message: "Session invalide." },
      { status: 401 },
    );
  }

  try {
    const result = await mutateInternalAdminData<unknown, undefined>(
      `/internal/admin/billing-v2/subscriptions/${encodeURIComponent(id)}/provisioning/reconcile`,
      "POST",
      undefined,
      sessionToken,
      correlationId,
    );
    return NextResponse.json(result);
  } catch (error) {
    console.error("Admin Billing V2 reconcile provisioning error:", error);
    const failure = getInternalApiError(error);
    return NextResponse.json(failure.error, { status: failure.status });
  }
}
