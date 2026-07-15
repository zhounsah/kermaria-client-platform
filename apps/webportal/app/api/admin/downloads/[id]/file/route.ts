import "server-only";

import type { DownloadResourceMutationResponse } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { controlledAdminError, handleAdminMutation } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { hasValidCsrfToken } from "@/lib/csrf-server";
import { getInternalApiError, getInternalSession } from "@/lib/internal-api";
import { isValidPortalIdentifier } from "@/lib/portal-bff";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
} from "@/lib/runtime-config";
import { getSessionCookieName } from "@/lib/session-config";

const PORTAL_SESSION_HEADER = "X-Portal-Session";

type RouteContext = { params: Promise<{ id: string }> };

export async function POST(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  if (!isValidPortalIdentifier(id)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le tÃ©lÃ©chargement demandÃ© est invalide.",
      correlationId,
    );
  }

  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  if (!sessionToken) {
    return controlledAdminError(
      401,
      "UNAUTHORIZED",
      "Une session valide est requise.",
      correlationId,
    );
  }

  if (!hasValidCsrfToken(request)) {
    return controlledAdminError(
      403,
      "CSRF_FORBIDDEN",
      "La requete d'administration doit etre confirmee par un jeton CSRF valide.",
      correlationId,
    );
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "internal_admin") {
      return controlledAdminError(
        403,
        "ACCESS_DENIED",
        "L'accÃ¨s Ã  cette ressource est refusÃ©.",
        correlationId,
      );
    }

    const internalApiUrl = getInternalApiUrl();
    if (!internalApiUrl) {
      return controlledAdminError(
        503,
        "INTERNAL_API_UNAVAILABLE",
        "L'API interne est indisponible.",
        correlationId,
      );
    }

    const formData = await request.formData();
    const file = formData.get("file");
    if (!(file instanceof File) || file.size <= 0) {
      return controlledAdminError(
        400,
        "INVALID_REQUEST",
        "Un fichier doit Ãªtre envoyÃ©.",
        correlationId,
      );
    }

    const internalResponse = await fetch(
      `${internalApiUrl}/internal/admin/downloads/${encodeURIComponent(id)}/file`,
      {
        method: "POST",
        body: formData,
        cache: "no-store",
        signal: AbortSignal.timeout(15000),
        headers: {
          ...getInternalServiceHeaders(),
          [CORRELATION_HEADER]: correlationId,
          [PORTAL_SESSION_HEADER]: sessionToken,
        },
      },
    );

    const payload = await internalResponse.json();
    if (!internalResponse.ok) {
      const response = NextResponse.json(payload, {
        status: internalResponse.status,
      });
      response.headers.set(
        CORRELATION_HEADER,
        typeof payload?.correlation_id === "string"
          ? payload.correlation_id
          : correlationId,
      );
      return response;
    }

    const response = NextResponse.json(
      payload as DownloadResourceMutationResponse,
    );
    response.headers.set(
      CORRELATION_HEADER,
      typeof payload?.correlation_id === "string"
        ? payload.correlation_id
        : correlationId,
    );
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

export async function DELETE(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  if (!isValidPortalIdentifier(id)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le tÃ©lÃ©chargement demandÃ© est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<undefined, DownloadResourceMutationResponse>(
    request,
    `/internal/admin/downloads/${encodeURIComponent(id)}/file`,
    "DELETE",
    undefined,
  );
}
