import { NextRequest, NextResponse } from "next/server";

import type { BillingV2CartCheckoutRequest } from "@kermaria/shared";

import { checkoutBillingV2Cart } from "@/lib/internal-api";
import { rejectInvalidPortalCsrf } from "@/lib/portal-bff";
import { resolveCorrelationId } from "@/lib/correlation";
import { readPortalSessionToken } from "@/lib/session-cookie";

function isCheckoutRequest(value: unknown): value is BillingV2CartCheckoutRequest {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Record<string, unknown>;
  return typeof candidate.cartId === "string"
    && Number.isInteger(candidate.expectedCartVersion)
    && Number.isInteger(candidate.acceptedQuoteVersion)
    && typeof candidate.acceptedCompositionFingerprint === "string";
}

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(request.headers.get("X-Correlation-Id"));
  const csrf = rejectInvalidPortalCsrf(request);
  if (csrf) return csrf;
  const sessionToken = await readPortalSessionToken();
  if (!sessionToken) {
    return NextResponse.json({ code: "AUTH_REQUIRED", correlation_id: correlationId }, {
      status: 401,
      headers: { "X-Correlation-Id": correlationId },
    });
  }
  let payload: unknown;
  try {
    payload = await request.json();
  } catch {
    return NextResponse.json({ code: "INVALID_REQUEST", correlation_id: correlationId }, { status: 400 });
  }
  if (!isCheckoutRequest(payload)) {
    return NextResponse.json({ code: "INVALID_REQUEST", correlation_id: correlationId }, { status: 400 });
  }
  try {
    const result = await checkoutBillingV2Cart({
      cartId: payload.cartId,
      expectedCartVersion: payload.expectedCartVersion,
      acceptedQuoteVersion: payload.acceptedQuoteVersion,
      acceptedCompositionFingerprint: payload.acceptedCompositionFingerprint,
      successUrl: new URL("/souscription?status=return", request.url).toString(),
      cancelUrl: new URL("/panier", request.url).toString(),
    }, correlationId, sessionToken);
    return NextResponse.json({ ...result, correlation_id: correlationId }, {
      headers: { "X-Correlation-Id": correlationId },
    });
  } catch (error) {
    const candidate = error as { status?: number; apiError?: unknown };
    return NextResponse.json(candidate.apiError ?? {
      code: "INTERNAL_API_UNAVAILABLE", correlation_id: correlationId,
    }, { status: candidate.status ?? 503, headers: { "X-Correlation-Id": correlationId } });
  }
}
