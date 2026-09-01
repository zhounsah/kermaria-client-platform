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
  reason_code: string;
  subscription_id?: string | null;
};

/**
 * Surface UX VPS minimale : le retour Stripe est toujours remis au processeur
 * Billing V2 commun. La redirection ne vaut jamais settlement ; le message de
 * confirmation indique donc que la verification provider reste en cours.
 */
export async function GET(request: NextRequest) {
  const correlationId = resolveCorrelationId(request.headers.get(CORRELATION_HEADER));
  const portalUrl = getPortalPublicUrl(request);
  const errorUrl = `${portalUrl}/services/vps/choisir?checkout=error`;
  const technicalRequestId = normalizeTechnicalRequestId(
    request.nextUrl.searchParams.get("technicalRequestId"),
  );
  const confirmationUrl = technicalRequestId
    ? `${portalUrl}/services/vps/choisir/confirmation?technicalRequestId=${encodeURIComponent(technicalRequestId)}`
    : `${portalUrl}/services/vps/choisir/confirmation`;
  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  const providerCheckoutId = request.nextUrl.searchParams.get("session_id")?.trim() || null;

  if (!sessionToken || !providerCheckoutId) return NextResponse.redirect(errorUrl);
  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "client_user") return NextResponse.redirect(errorUrl);

    const result = await mutateInternalPortalPayloadTyped<
      BillingV2ProviderReturnResponse,
      {
        provider: "stripe";
        providerCheckoutId: string;
        providerSubscriptionId: null;
        rawPayload: string;
      }
    >(
      "/internal/portal/billing-v2/provider-return",
      {
        provider: "stripe",
        providerCheckoutId,
        providerSubscriptionId: null,
        rawPayload: request.nextUrl.toString(),
      },
      sessionToken,
      correlationId,
    );
    return isRecordedReturn(result)
      ? NextResponse.redirect(confirmationUrl)
      : NextResponse.redirect(errorUrl);
  } catch {
    return NextResponse.redirect(errorUrl);
  }
}

function isRecordedReturn(result: BillingV2ProviderReturnResponse): boolean {
  return Boolean(result.subscription_id)
    && (
      result.reason_code === "BILLING_V2_PROVIDER_CHECKOUT_RETURN_RECORDED"
      || result.reason_code === "BILLING_V2_PROVIDER_EVENT_ALREADY_PROCESSED"
      || result.reason_code === "BILLING_V2_PROVIDER_EVENT_IDEMPOTENT_NOOP"
    );
}

function normalizeTechnicalRequestId(value: string | null) {
  const id = value?.trim() ?? "";
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(id)
    ? id
    : null;
}
