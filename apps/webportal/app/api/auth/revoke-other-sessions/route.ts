import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalApiError,
  revokeOtherInternalSessions,
} from "@/lib/internal-api";
import { getSessionCookieName } from "@/lib/session-config";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const sessionToken = request.cookies.get(getSessionCookieName())?.value;

  if (!sessionToken) {
    const response = NextResponse.json(
      {
        code: "UNAUTHORIZED",
        message: "Une session valide est requise.",
        correlation_id: correlationId,
      },
      { status: 401 },
    );
    response.headers.set(CORRELATION_HEADER, correlationId);
    return response;
  }

  try {
    const result = await revokeOtherInternalSessions(
      sessionToken,
      correlationId,
    );
    const response = NextResponse.json(result);
    response.headers.set(CORRELATION_HEADER, correlationId);
    return response;
  } catch (error) {
    const failure = getInternalApiError(error);
    const response = NextResponse.json(failure.error, {
      status: failure.status,
    });
    response.headers.set(
      CORRELATION_HEADER,
      failure.error.correlation_id,
    );
    return response;
  }
}
