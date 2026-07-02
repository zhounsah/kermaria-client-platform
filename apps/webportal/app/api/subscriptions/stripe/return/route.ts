import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalSession,
  mutateInternalPortalPayloadTyped,
} from "@/lib/internal-api";
import type { SubscriptionSummary } from "@kermaria/shared";
import { getSessionCookieName } from "@/lib/session-config";
import { getStripeCheckoutSession } from "@/lib/stripe";

export async function GET(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  const portalUrl =
    process.env.PUBLIC_PORTAL_URL?.replace(/\/$/, "") ?? "http://localhost:3000";
  const errorUrl = `${portalUrl}/profile/subscriptions?subscription=error`;
  const successUrl = `${portalUrl}/profile/subscriptions?subscription=approved`;

  const { searchParams } = request.nextUrl;
  const offerId = searchParams.get("offerId");
  const stripeCheckoutSessionId = searchParams.get("session_id");
  if (!offerId || !stripeCheckoutSessionId) {
    return NextResponse.redirect(errorUrl);
  }

  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  if (!sessionToken) {
    return NextResponse.redirect(errorUrl);
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "client_user") {
      return NextResponse.redirect(errorUrl);
    }
  } catch {
    return NextResponse.redirect(errorUrl);
  }

  let stripeSubscriptionId: string | null;
  try {
    const checkoutSession = await getStripeCheckoutSession(
      stripeCheckoutSessionId,
    );
    stripeSubscriptionId = checkoutSession.subscriptionId;
  } catch (error) {
    console.error("Stripe checkout session lookup error:", error);
    return NextResponse.redirect(errorUrl);
  }

  if (!stripeSubscriptionId) {
    return NextResponse.redirect(errorUrl);
  }

  let subscription: SubscriptionSummary;
  try {
    subscription = await mutateInternalPortalPayloadTyped<
      SubscriptionSummary,
      { offerId: string; rail: string; stripeSubscriptionId: string }
    >(
      "/internal/portal/subscriptions",
      { offerId, rail: "stripe", stripeSubscriptionId },
      sessionToken,
      correlationId,
    );
  } catch (error) {
    console.error("Stripe subscription persist error:", error);
    return NextResponse.redirect(errorUrl);
  }

  try {
    await mutateInternalPortalPayloadTyped<
      SubscriptionSummary,
      Record<string, never>
    >(
      `/internal/portal/subscriptions/${encodeURIComponent(
        subscription.id,
      )}/return-approved`,
      undefined,
      sessionToken,
      correlationId,
    );
  } catch (error) {
    console.error("Stripe subscription return-approved error:", error);
    return NextResponse.redirect(errorUrl);
  }

  return NextResponse.redirect(successUrl);
}
