import "server-only";

import type {
  SubscriptionProvisioningReconcilePayload,
  SubscriptionProvisioningSummary,
} from "@kermaria/shared";
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
    let payload: SubscriptionProvisioningReconcilePayload | undefined;
    if (request.headers.get("content-type")?.includes("application/json")) {
      const body = await request.json().catch(() => null);
      if (body && typeof body === "object" && !Array.isArray(body)) {
        const candidate = body as Partial<SubscriptionProvisioningReconcilePayload>;
        if (
          candidate.targetUserSamAccountNames !== undefined
          && candidate.targetUserSamAccountNames !== null
          && (!Array.isArray(candidate.targetUserSamAccountNames)
            || !candidate.targetUserSamAccountNames.every((value) =>
              typeof value === "string"
              && /^[A-Za-z0-9._-]{1,64}$/.test(value)))
        ) {
          return NextResponse.json(
            {
              code: "INVALID_REQUEST",
              message: "La liste des utilisateurs AD est invalide.",
            },
            { status: 400 },
          );
        }

        payload = {
          targetUserSamAccountNames:
            candidate.targetUserSamAccountNames?.map((value) => value.trim())
              .filter(Boolean) ?? null,
        };
      }
    }

    const result = await mutateInternalAdminData<
      SubscriptionProvisioningSummary,
      SubscriptionProvisioningReconcilePayload | undefined
    >(
      `/internal/admin/subscriptions/${encodeURIComponent(id)}/provisioning/reconcile`,
      "POST",
      payload,
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
