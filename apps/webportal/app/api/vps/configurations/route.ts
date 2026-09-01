import type {
  ApiError,
  CorrelationId,
  BillingV2VpsConfigurationPayload,
  BillingV2VpsConfigurationQuoteResponse,
} from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { parseBillingV2VpsConfigurationPayload } from "@/lib/bff-payloads";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { hasValidCsrfToken } from "@/lib/csrf-server";
import {
  getInternalApiError,
  mutateInternalPortalPayloadTyped,
} from "@/lib/internal-api";
import { getSessionCookieName } from "@/lib/session-config";

export const dynamic = "force-dynamic";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(request.headers.get(CORRELATION_HEADER));
  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  if (!sessionToken) return fail("SESSION_REQUIRED", "Une session valide est requise.", 401, correlationId);
  if (!hasValidCsrfToken(request)) return fail("CSRF_FORBIDDEN", "La session doit confirmer cette action.", 403, correlationId);

  let candidate: unknown;
  try {
    candidate = await request.json();
  } catch {
    return fail("INVALID_REQUEST", "Le corps de la requête est invalide.", 400, correlationId);
  }

  const payload = parseBillingV2VpsConfigurationPayload(candidate);
  if (!payload) return fail("INVALID_REQUEST", "La configuration VPS est incomplète ou invalide.", 400, correlationId);

  try {
    const result = await mutateInternalPortalPayloadTyped<
      BillingV2VpsConfigurationQuoteResponse,
      BillingV2VpsConfigurationPayload
    >(
      "/internal/portal/billing-v2/vps/configurations",
      payload,
      sessionToken,
      correlationId,
    );
    return NextResponse.json(result, {
      headers: { [CORRELATION_HEADER]: result.correlationId },
    });
  } catch (error) {
    const failure = getInternalApiError(error);
    return NextResponse.json(failure.error, {
      status: failure.status,
      headers: { [CORRELATION_HEADER]: failure.error.correlation_id },
    });
  }
}

function fail(
  code: string,
  message: string,
  status: number,
  correlationId: CorrelationId,
) {
  const error: ApiError = { code, message, correlation_id: correlationId };
  return NextResponse.json(error, {
    status,
    headers: { [CORRELATION_HEADER]: correlationId },
  });
}
