import type { ApiError, ServiceRequestPayload } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  createServiceRequest,
  getInternalApiError,
} from "@/lib/internal-api";
import { getSessionCookieName } from "@/lib/session-config";

export const dynamic = "force-dynamic";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const sessionToken = request.cookies.get(
    getSessionCookieName(),
  )?.value;

  if (!sessionToken) {
    return sessionRequired(correlationId);
  }

  let payload: ServiceRequestPayload;

  try {
    payload = (await request.json()) as ServiceRequestPayload;
  } catch {
    return invalidRequest(correlationId);
  }

  if (!isValidServiceRequestPayload(payload)) {
    return invalidRequest(correlationId);
  }

  try {
    const result = await createServiceRequest(
      payload,
      correlationId,
      sessionToken,
    );
    const response = NextResponse.json(result, { status: 202 });

    response.headers.set(CORRELATION_HEADER, result.correlation_id);

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

function sessionRequired(correlationId: ApiError["correlation_id"]) {
  const error: ApiError = {
    code: "SESSION_REQUIRED",
    message: "Une session valide est requise.",
    correlation_id: correlationId,
  };
  const response = NextResponse.json(error, { status: 401 });
  response.headers.set(CORRELATION_HEADER, correlationId);
  return response;
}

function isValidServiceRequestPayload(
  payload: unknown,
): payload is ServiceRequestPayload {
  if (!payload || typeof payload !== "object") {
    return false;
  }

  const candidate = payload as Partial<ServiceRequestPayload>;

  return (
    typeof candidate.catalogItemId === "string"
    && candidate.catalogItemId.trim().length > 0
    && typeof candidate.subject === "string"
    && candidate.subject.trim().length > 0
    && candidate.subject.length <= 160
    && typeof candidate.description === "string"
    && candidate.description.trim().length > 0
    && candidate.description.length <= 4000
  );
}

function invalidRequest(correlationId: ApiError["correlation_id"]) {
  const error: ApiError = {
    code: "INVALID_REQUEST",
    message: "La demande est incomplète ou invalide.",
    correlation_id: correlationId,
  };
  const response = NextResponse.json(error, { status: 400 });

  response.headers.set(CORRELATION_HEADER, correlationId);

  return response;
}
