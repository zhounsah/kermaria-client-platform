import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { hasValidCsrfToken } from "@/lib/csrf-server";
import {
  getInternalApiError,
  getInternalEmailVerificationState,
  resendInternalEmailVerification,
} from "@/lib/internal-api";
import { getSessionCookieName } from "@/lib/session-config";

export const dynamic = "force-dynamic";

export async function GET(request: NextRequest) {
  const correlationId = resolveCorrelationId(request.headers.get(CORRELATION_HEADER));
  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  if (!sessionToken) return fail("AUTH_REQUIRED", "Une session valide est requise.", 401, correlationId);
  try {
    const state = await getInternalEmailVerificationState(sessionToken, correlationId);
    return NextResponse.json(state, { headers: { [CORRELATION_HEADER]: correlationId } });
  } catch (error) {
    const failure = getInternalApiError(error);
    return NextResponse.json(failure.error, { status: failure.status, headers: { [CORRELATION_HEADER]: failure.error.correlation_id } });
  }
}

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(request.headers.get(CORRELATION_HEADER));
  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  if (!sessionToken) return fail("AUTH_REQUIRED", "Une session valide est requise.", 401, correlationId);
  if (!hasValidCsrfToken(request)) return fail("CSRF_FORBIDDEN", "La session doit confirmer cette action.", 403, correlationId);
  try {
    const result = await resendInternalEmailVerification(sessionToken, correlationId);
    return NextResponse.json(result, { headers: { [CORRELATION_HEADER]: correlationId } });
  } catch (error) {
    const failure = getInternalApiError(error);
    return NextResponse.json(failure.error, { status: failure.status, headers: { [CORRELATION_HEADER]: failure.error.correlation_id } });
  }
}

function fail(code: string, message: string, status: number, correlationId: string) {
  return NextResponse.json({ code, message, correlation_id: correlationId }, { status, headers: { [CORRELATION_HEADER]: correlationId } });
}
