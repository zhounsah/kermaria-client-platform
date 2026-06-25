import "server-only";

import type { SubscriptionSummary } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalSession,
  mutateInternalPortalPayloadTyped,
} from "@/lib/internal-api";
import { getSessionCookieName } from "@/lib/session-config";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
} from "@/lib/runtime-config";

const PORTAL_SESSION_HEADER = "X-Portal-Session";

export async function GET(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  const portalUrl =
    process.env.PUBLIC_PORTAL_URL?.replace(/\/$/, "") ?? "http://localhost:3000";
  const errorUrl = `${portalUrl}/profile/subscriptions?subscription=error`;
  const successUrl = `${portalUrl}/profile/subscriptions?subscription=approved`;

  const { searchParams } = request.nextUrl;
  const paypalSubscriptionId = searchParams.get("subscription_id");
  if (!paypalSubscriptionId) {
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

  let internalApiUrl: string | undefined;
  try {
    internalApiUrl = getInternalApiUrl();
  } catch {
    /* ignored */
  }
  if (!internalApiUrl) {
    return NextResponse.redirect(errorUrl);
  }

  let subscriptions: SubscriptionSummary[];
  try {
    const response = await fetch(
      `${internalApiUrl}/internal/portal/subscriptions`,
      {
        cache: "no-store",
        signal: AbortSignal.timeout(10000),
        headers: {
          Accept: "application/json",
          ...getInternalServiceHeaders(),
          [CORRELATION_HEADER]: correlationId,
          [PORTAL_SESSION_HEADER]: sessionToken,
        },
      },
    );
    if (!response.ok) {
      return NextResponse.redirect(errorUrl);
    }
    subscriptions = (await response.json()) as SubscriptionSummary[];
  } catch (error) {
    console.error("Subscriptions lookup error:", error);
    return NextResponse.redirect(errorUrl);
  }

  const subscription = subscriptions.find(
    (item) => item.paypalSubscriptionId === paypalSubscriptionId,
  );
  if (!subscription) {
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
    console.error("Subscription return-approved error:", error);
    return NextResponse.redirect(errorUrl);
  }

  return NextResponse.redirect(successUrl);
}
