import "server-only";

import type {
  AdminSubscriptionDetail,
  SubscriptionSummary,
} from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalSession,
  mutateInternalAdminData,
} from "@/lib/internal-api";
import { getSessionCookieName } from "@/lib/session-config";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
} from "@/lib/runtime-config";
import { cancelPayPalSubscription, isPayPalConfigured } from "@/lib/paypal";

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

  let internalApiUrl: string | undefined;
  try {
    internalApiUrl = getInternalApiUrl();
  } catch {
    /* ignored */
  }
  if (!internalApiUrl) {
    return NextResponse.json(
      { code: "UNAVAILABLE", message: "API interne indisponible." },
      { status: 503 },
    );
  }

  const detailResponse = await fetch(
    `${internalApiUrl}/internal/admin/subscriptions/${encodeURIComponent(id)}`,
    {
      cache: "no-store",
      signal: AbortSignal.timeout(10000),
      headers: {
        Accept: "application/json",
        ...getInternalServiceHeaders(),
        [CORRELATION_HEADER]: correlationId,
        "X-Portal-Session": sessionToken,
      },
    },
  );

  if (!detailResponse.ok) {
    return NextResponse.json(
      { code: "SUBSCRIPTION_NOT_FOUND", message: "Souscription introuvable." },
      { status: 404 },
    );
  }

  const detail = (await detailResponse.json()) as AdminSubscriptionDetail;
  const paypalSubscriptionId = detail.subscription.paypalSubscriptionId;

  if (
    paypalSubscriptionId
    && detail.subscription.status !== "cancelled"
    && detail.subscription.status !== "expired"
  ) {
    if (!isPayPalConfigured()) {
      return NextResponse.json(
        {
          code: "PAYPAL_NOT_CONFIGURED",
          message: "PayPal n'est pas configuré.",
        },
        { status: 503 },
      );
    }
    try {
      await cancelPayPalSubscription(
        paypalSubscriptionId,
        "Annulation administrative depuis le portail Kermaria.",
      );
    } catch (error) {
      console.error("PayPal cancel subscription error:", error);
      return NextResponse.json(
        {
          code: "PAYPAL_ERROR",
          message:
            "PayPal n'a pas pu annuler la souscription. Le statut local n'a pas été modifié.",
        },
        { status: 502 },
      );
    }
  }

  let result: SubscriptionSummary;
  try {
    result = await mutateInternalAdminData<SubscriptionSummary, undefined>(
      `/internal/admin/subscriptions/${encodeURIComponent(id)}/cancel`,
      "POST",
      undefined,
      sessionToken,
      correlationId,
    );
  } catch (error) {
    console.error("Admin cancel subscription error:", error);
    return NextResponse.json(
      {
        code: "PERSIST_ERROR",
        message: "Impossible d'enregistrer l'annulation locale.",
      },
      { status: 503 },
    );
  }

  return NextResponse.json(result);
}
