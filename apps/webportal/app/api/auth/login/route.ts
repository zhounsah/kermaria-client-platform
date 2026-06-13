import type { ApiError, LoginPayload } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  createInternalSession,
  getInternalApiError,
} from "@/lib/internal-api";
import {
  getSessionCookieName,
  getSessionCookieOptions,
} from "@/lib/session-config";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  let payload: unknown;

  try {
    payload = await request.json();
  } catch {
    return invalidCredentials(correlationId);
  }

  if (!isLoginPayload(payload)) {
    return invalidCredentials(correlationId);
  }

  try {
    const session = await createInternalSession(
      payload,
      correlationId,
      request.headers.get("user-agent"),
    );
    const response = NextResponse.json({
      authenticated: true,
      user: session.user,
      expiresAt: session.expiresAt,
    });

    response.cookies.set({
      name: getSessionCookieName(),
      value: session.sessionToken,
      ...getSessionCookieOptions(),
      expires: new Date(session.expiresAt),
    });
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

function isLoginPayload(payload: unknown): payload is LoginPayload {
  if (!payload || typeof payload !== "object") {
    return false;
  }

  const candidate = payload as Partial<LoginPayload>;
  return (
    typeof candidate.email === "string"
    && candidate.email.trim().length > 0
    && candidate.email.length <= 254
    && typeof candidate.password === "string"
    && candidate.password.length > 0
    && candidate.password.length <= 1024
  );
}

function invalidCredentials(correlationId: ApiError["correlation_id"]) {
  const response = NextResponse.json(
    {
      code: "INVALID_CREDENTIALS",
      message: "Identifiants invalides.",
      correlation_id: correlationId,
    } satisfies ApiError,
    { status: 401 },
  );
  response.headers.set(CORRELATION_HEADER, correlationId);
  return response;
}
