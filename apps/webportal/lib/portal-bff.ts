import "server-only";

import type { ApiError } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalApiError,
  getInternalPortalData,
  getInternalSession,
  mutateInternalPortalData,
  mutateInternalPortalPayload,
} from "@/lib/internal-api";
import { getSessionCookieName } from "@/lib/session-config";

export async function handlePortalGet<T>(
  request: NextRequest,
  internalPath: string,
) {
  const context = await resolveClientContext(request);
  if (context instanceof NextResponse) {
    return context;
  }

  try {
    const data = await getInternalPortalData<T>(
      internalPath,
      context.sessionToken,
      context.correlationId,
    );
    const response = NextResponse.json(data);
    response.headers.set(CORRELATION_HEADER, context.correlationId);
    return response;
  } catch (error) {
    return portalFailure(error);
  }
}

export async function handlePortalMutation(
  request: NextRequest,
  internalPath: string,
) {
  const context = await resolveClientContext(request);
  if (context instanceof NextResponse) {
    return context;
  }

  try {
    const data = await mutateInternalPortalData(
      internalPath,
      context.sessionToken,
      context.correlationId,
    );
    const response = NextResponse.json(data);
    response.headers.set(CORRELATION_HEADER, data.correlation_id);
    return response;
  } catch (error) {
    return portalFailure(error);
  }
}

export async function handlePortalPayloadMutation<TPayload>(
  request: NextRequest,
  internalPath: string,
  payload: TPayload,
) {
  const context = await resolveClientContext(request);
  if (context instanceof NextResponse) {
    return context;
  }

  try {
    const data = await mutateInternalPortalPayload(
      internalPath,
      payload,
      context.sessionToken,
      context.correlationId,
    );
    const response = NextResponse.json(data);
    response.headers.set(CORRELATION_HEADER, data.correlation_id);
    return response;
  } catch (error) {
    return portalFailure(error);
  }
}

export function isValidPortalIdentifier(value: string) {
  return /^[A-Za-z0-9-]{1,100}$/.test(value);
}

async function resolveClientContext(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  if (!sessionToken) {
    return controlledPortalError(
      401,
      "UNAUTHORIZED",
      "Une session valide est requise.",
      correlationId,
    );
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "client_user") {
      return controlledPortalError(
        403,
        "ACCESS_DENIED",
        "L'accès à cette ressource est refusé.",
        correlationId,
      );
    }

    return { correlationId, sessionToken };
  } catch (error) {
    return portalFailure(error);
  }
}

function portalFailure(error: unknown) {
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

export function controlledPortalError(
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
