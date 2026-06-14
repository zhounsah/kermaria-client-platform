import "server-only";

import type { ApiError } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalAdminData,
  getInternalApiError,
  getInternalSession,
  mutateInternalAdminData,
} from "@/lib/internal-api";
import { getSessionCookieName } from "@/lib/session-config";

export async function handleAdminGet<T>(
  request: NextRequest,
  internalPath: string,
) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const sessionToken = request.cookies.get(getSessionCookieName())?.value;

  if (!sessionToken) {
    return controlledAdminError(
      401,
      "UNAUTHORIZED",
      "Une session valide est requise.",
      correlationId,
    );
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "internal_admin") {
      return controlledAdminError(
        403,
        "ACCESS_DENIED",
        "L'accès à cette ressource est refusé.",
        correlationId,
      );
    }

    const data = await getInternalAdminData<T>(
      internalPath,
      sessionToken,
      correlationId,
    );
    const response = NextResponse.json(data);
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

export async function handleAdminMutation<TPayload>(
  request: NextRequest,
  internalPath: string,
  method: "PATCH" | "POST",
  payload: TPayload,
) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const sessionToken = request.cookies.get(getSessionCookieName())?.value;

  if (!sessionToken) {
    return controlledAdminError(
      401,
      "UNAUTHORIZED",
      "Une session valide est requise.",
      correlationId,
    );
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "internal_admin") {
      return controlledAdminError(
        403,
        "ACCESS_DENIED",
        "L'accès à cette ressource est refusé.",
        correlationId,
      );
    }

    const data = await mutateInternalAdminData(
      internalPath,
      method,
      payload,
      sessionToken,
      correlationId,
    );
    const response = NextResponse.json(data);
    response.headers.set(CORRELATION_HEADER, data.correlation_id);
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

export function controlledAdminError(
  status: number,
  code: string,
  message: string,
  correlationId: ApiError["correlation_id"],
) {
  const response = NextResponse.json(
    {
      code,
      message,
      correlation_id: correlationId,
    } satisfies ApiError,
    { status },
  );
  response.headers.set(CORRELATION_HEADER, correlationId);
  return response;
}
