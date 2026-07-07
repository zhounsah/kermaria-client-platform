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
import { cancelPayPalSubscription, isPayPalConfigured } from "@/lib/paypal";
import { getSessionCookieName } from "@/lib/session-config";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
} from "@/lib/runtime-config";
import {
  cancelStripeSubscription,
  getStripeMode,
  scheduleStripeSubscriptionCancellationAtPeriodEnd,
} from "@/lib/stripe";

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
  const subscription = detail.subscription;

  if (subscription.status === "pending_cancellation") {
    return NextResponse.json(subscription);
  }

  const nextBillingAt = subscription.nextBillingAt
    ? new Date(subscription.nextBillingAt)
    : null;
  const shouldDeferCancellation =
    (subscription.status === "active" || subscription.status === "suspended")
    && nextBillingAt !== null
    && nextBillingAt.getTime() > Date.now();

  try {
    if (shouldDeferCancellation) {
      if (subscription.rail === "stripe" && subscription.stripeSubscriptionId) {
        if (getStripeMode() === "disabled") {
          return NextResponse.json(
            {
              code: "STRIPE_NOT_CONFIGURED",
              message: "Stripe n'est pas configure.",
            },
            { status: 503 },
          );
        }

        await scheduleStripeSubscriptionCancellationAtPeriodEnd(
          subscription.stripeSubscriptionId,
        );
      }
    } else if (
      subscription.status !== "cancelled"
      && subscription.status !== "expired"
    ) {
      if (subscription.rail === "paypal" && subscription.paypalSubscriptionId) {
        if (!isPayPalConfigured()) {
          return NextResponse.json(
            {
              code: "PAYPAL_NOT_CONFIGURED",
              message: "PayPal n'est pas configure.",
            },
            { status: 503 },
          );
        }

        await cancelPayPalSubscription(
          subscription.paypalSubscriptionId,
          "Annulation administrative depuis le portail Kermaria.",
        );
      }

      if (subscription.rail === "stripe" && subscription.stripeSubscriptionId) {
        if (getStripeMode() === "disabled") {
          return NextResponse.json(
            {
              code: "STRIPE_NOT_CONFIGURED",
              message: "Stripe n'est pas configure.",
            },
            { status: 503 },
          );
        }

        await cancelStripeSubscription(subscription.stripeSubscriptionId);
      }
    }
  } catch (error) {
    console.error("Payment cancel subscription error:", error);
    return NextResponse.json(
      {
        code:
          subscription.rail === "stripe" ? "STRIPE_ERROR" : "PAYPAL_ERROR",
        message:
          shouldDeferCancellation
            ? "L'operateur de paiement n'a pas pu programmer la fin de terme. Le statut local n'a pas ete modifie."
            : "L'operateur de paiement n'a pas pu annuler la souscription. Le statut local n'a pas ete modifie.",
      },
      { status: 502 },
    );
  }

  try {
    const result = await mutateInternalAdminData<SubscriptionSummary, undefined>(
      `/internal/admin/subscriptions/${encodeURIComponent(id)}/cancel`,
      "POST",
      undefined,
      sessionToken,
      correlationId,
    );
    return NextResponse.json(result);
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
}
