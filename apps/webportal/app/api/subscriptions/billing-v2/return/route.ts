import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalSession,
  mutateInternalPortalPayloadTyped,
} from "@/lib/internal-api";
import { getPortalPublicUrl } from "@/lib/public-routes";
import { getSessionCookieName } from "@/lib/session-config";

type BillingV2ProviderReturnResponse = {
  applied: boolean;
  reason_code: string;
  subscription_id?: string | null;
  checkout_session_id?: string | null;
  correlation_id: string;
};

export async function GET(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const portalUrl = getPortalPublicUrl(request);
  const errorUrl = `${portalUrl}/profile/subscriptions?subscription=error`;
  const successUrl = `${portalUrl}/profile/subscriptions?subscription=approved`;

  const { searchParams } = request.nextUrl;
  const provider = resolveProvider(searchParams);
  const providerCheckoutId = searchParams.get("session_id")?.trim() || null;
  const providerSubscriptionId =
    searchParams.get("subscription_id")?.trim()
    || searchParams.get("subscriptionId")?.trim()
    || null;
  if (
    !provider
    || (providerCheckoutId === null && providerSubscriptionId === null)
  ) {
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

  let result: BillingV2ProviderReturnResponse;
  try {
    result = await mutateInternalPortalPayloadTyped<
      BillingV2ProviderReturnResponse,
      {
        provider: string;
        providerCheckoutId: string | null;
        providerSubscriptionId: string | null;
        rawPayload: string;
      }
    >(
      "/internal/portal/billing-v2/provider-return",
      {
        provider,
        providerCheckoutId,
        providerSubscriptionId,
        rawPayload: request.nextUrl.toString(),
      },
      sessionToken,
      correlationId,
    );
  } catch (error) {
    console.error("Billing V2 provider return error:", error);
    return NextResponse.redirect(errorUrl);
  }

  if (!isSuccessfulReturn(result)) {
    return NextResponse.redirect(errorUrl);
  }

  return NextResponse.redirect(successUrl);
}

function resolveProvider(searchParams: URLSearchParams): "stripe" | "paypal" | null {
  const provider = searchParams.get("provider")?.trim().toLowerCase();
  if (provider === "stripe" || provider === "paypal") {
    return provider;
  }

  if (searchParams.has("session_id")) {
    return "stripe";
  }

  if (
    searchParams.has("subscription_id")
    || searchParams.has("subscriptionId")
  ) {
    return "paypal";
  }

  return null;
}

function isSuccessfulReturn(result: BillingV2ProviderReturnResponse): boolean {
  return Boolean(result.subscription_id)
    && (
      result.reason_code === "BILLING_V2_PROVIDER_CHECKOUT_RETURN_RECORDED"
      || result.reason_code === "BILLING_V2_PROVIDER_EVENT_ALREADY_PROCESSED"
      || result.reason_code === "BILLING_V2_PROVIDER_EVENT_IDEMPOTENT_NOOP"
    );
}
