import "server-only";

import type { SubscriptionSummary } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalPortalData,
  getInternalSession,
  mutateInternalPortalPayloadTyped,
} from "@/lib/internal-api";
import { cancelPayPalSubscription, isPayPalConfigured } from "@/lib/paypal";
import { getSessionCookieName } from "@/lib/session-config";
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
    if (session.user.role !== "client_user") {
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

  let subscription: SubscriptionSummary | null = null;
  try {
    const subscriptions = await getInternalPortalData<SubscriptionSummary[]>(
      "/internal/portal/subscriptions",
      sessionToken,
      correlationId,
    );
    subscription =
      subscriptions.find((candidate) => candidate.id === id) ?? null;
  } catch {
    subscription = null;
  }

  if (!subscription) {
    return NextResponse.json(
      { code: "SUBSCRIPTION_NOT_FOUND", message: "Souscription introuvable." },
      { status: 404 },
    );
  }

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
          "Resiliation client depuis le portail Kermaria.",
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
    console.error("Client cancel subscription error:", error);
    return NextResponse.json(
      {
        code:
          subscription.rail === "stripe" ? "STRIPE_ERROR" : "PAYPAL_ERROR",
        message:
          shouldDeferCancellation
            ? "La resiliation a echoue chez l'operateur de paiement. Le statut local n'a pas ete modifie."
            : "Le paiement n'a pas pu etre resilie. Le statut local n'a pas ete modifie.",
      },
      { status: 502 },
    );
  }

  try {
    const result = await mutateInternalPortalPayloadTyped<
      SubscriptionSummary,
      undefined
    >(
      `/internal/portal/subscriptions/${encodeURIComponent(id)}/cancel`,
      undefined,
      sessionToken,
      correlationId,
    );
    return NextResponse.json(result);
  } catch (error) {
    console.error("Client persist cancel error:", error);
    return NextResponse.json(
      {
        code: "PERSIST_ERROR",
        message: "Impossible d'enregistrer la resiliation locale.",
      },
      { status: 503 },
    );
  }
}
