import type {
  ApiError,
  BackupRestoreRequestPayload,
} from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { rejectInvalidPortalCsrf } from "@/lib/portal-bff";
import {
  createBackupRestoreRequest,
  getInternalApiError,
} from "@/lib/internal-api";
import { getSessionCookieName } from "@/lib/session-config";

export const dynamic = "force-dynamic";

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  if (!sessionToken) {
    return jsonError(401, "SESSION_REQUIRED", "Une session valide est requise.", correlationId);
  }

  const csrfFailure = rejectInvalidPortalCsrf(request);
  if (csrfFailure) {
    return csrfFailure;
  }

  let candidate: unknown;
  try {
    candidate = await request.json();
  } catch {
    return jsonError(400, "INVALID_REQUEST", "La demande est invalide.", correlationId);
  }

  const payload = parsePayload(candidate);
  if (!payload) {
    return jsonError(400, "INVALID_REQUEST", "La demande est incomplete ou invalide.", correlationId);
  }

  try {
    const { id } = await params;
    const result = await createBackupRestoreRequest(
      id,
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
    response.headers.set(CORRELATION_HEADER, failure.error.correlation_id);
    return response;
  }
}

function parsePayload(value: unknown): BackupRestoreRequestPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<BackupRestoreRequestPayload>;
  const itemPath = typeof candidate.itemPath === "string"
    ? candidate.itemPath.trim()
    : "";
  const description = typeof candidate.description === "string"
    ? candidate.description.trim()
    : "";
  const desiredRestoreAt = typeof candidate.desiredRestoreAt === "string"
    ? candidate.desiredRestoreAt.trim()
    : undefined;
  const priority = candidate.priority;

  if (
    !["low", "normal", "high"].includes(String(priority))
    || (itemPath.length === 0 && description.length === 0)
    || itemPath.length > 300
    || description.length > 2000
  ) {
    return null;
  }

  return {
    itemPath,
    description,
    desiredRestoreAt: desiredRestoreAt || undefined,
    priority: priority as BackupRestoreRequestPayload["priority"],
  };
}

function jsonError(
  status: number,
  code: string,
  message: string,
  correlationId: ApiError["correlation_id"],
) {
  const response = NextResponse.json(
    { code, message, correlation_id: correlationId } satisfies ApiError,
    { status },
  );
  response.headers.set(CORRELATION_HEADER, correlationId);
  return response;
}
