import "server-only";

import type { SubscriptionProvisioningSummary } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
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

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "internal_admin") {
      return NextResponse.json(
        { code: "ACCESS_DENIED", message: "Accès refusé." },
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
    const result = await mutateInternalAdminData<
      SubscriptionProvisioningSummary,
      undefined
    >(
      `/internal/admin/subscriptions/${encodeURIComponent(id)}/provisioning/reconcile`,
      "POST",
      undefined,
      sessionToken,
      correlationId,
    );
    return NextResponse.json(result);
  } catch (error) {
    console.error("Admin reconcile provisioning error:", error);
    return NextResponse.json(
      {
        code: "PERSIST_ERROR",
        message: "Impossible de relancer le provisioning.",
      },
      { status: 503 },
    );
  }
}
