import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalApiError,
  getInternalSession,
} from "@/lib/internal-api";
import {
  getSessionCookieName,
  getSessionCookieOptions,
} from "@/lib/session-config";

export async function GET(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const cookieName = getSessionCookieName();
  const sessionToken = request.cookies.get(cookieName)?.value;

  if (!sessionToken) {
    return unauthenticated(correlationId);
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    const response = NextResponse.json({
      authenticated: true,
      user: session.user,
      expiresAt: session.expiresAt,
    });
    response.headers.set(CORRELATION_HEADER, correlationId);
    return response;
  } catch (error) {
    const failure = getInternalApiError(error);

    if (failure.status !== 401) {
      const response = NextResponse.json(failure.error, {
        status: failure.status,
      });
      response.headers.set(
        CORRELATION_HEADER,
        failure.error.correlation_id,
      );
      return response;
    }

    const response = unauthenticated(
      failure.error.correlation_id,
    );
    response.cookies.set({
      name: cookieName,
      value: "",
      ...getSessionCookieOptions(),
      expires: new Date(0),
    });
    return response;
  }
}

function unauthenticated(correlationId: string) {
  const response = NextResponse.json({ authenticated: false });
  response.headers.set(CORRELATION_HEADER, correlationId);
  return response;
}
