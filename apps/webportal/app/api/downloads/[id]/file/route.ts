import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { getInternalSession } from "@/lib/internal-api";
import {
  controlledPortalError,
  isValidPortalIdentifier,
} from "@/lib/portal-bff";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
} from "@/lib/runtime-config";
import { getSessionCookieName } from "@/lib/session-config";

const PORTAL_SESSION_HEADER = "X-Portal-Session";

type RouteContext = { params: Promise<{ id: string }> };

export async function GET(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  if (!isValidPortalIdentifier(id)) {
    return controlledPortalError(
      400,
      "INVALID_REQUEST",
      "L'identifiant demandÃ© est invalide.",
      correlationId,
    );
  }

  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  if (!sessionToken) {
    return controlledPortalError(
      401,
      "UNAUTHORIZED",
      "Une session valide est requise.",
      correlationId,
    );
  }

  let internalApiUrl: string | undefined;
  try {
    internalApiUrl = getInternalApiUrl();
  } catch {
    return controlledPortalError(
      503,
      "INTERNAL_API_UNAVAILABLE",
      "L'API interne est indisponible.",
      correlationId,
    );
  }

  if (!internalApiUrl) {
    return controlledPortalError(
      503,
      "INTERNAL_API_UNAVAILABLE",
      "L'API interne est indisponible.",
      correlationId,
    );
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "client_user") {
      return controlledPortalError(
        403,
        "ACCESS_DENIED",
        "L'accÃ¨s Ã  cette ressource est refusÃ©.",
        correlationId,
      );
    }
  } catch {
    return controlledPortalError(
      401,
      "SESSION_INVALID",
      "Session invalide ou expirÃ©e.",
      correlationId,
    );
  }

  const internalResponse = await fetch(
    `${internalApiUrl}/internal/portal/downloads/${encodeURIComponent(id)}/file`,
    {
      cache: "no-store",
      redirect: "manual",
      signal: AbortSignal.timeout(15000),
      headers: {
        ...getInternalServiceHeaders(),
        [CORRELATION_HEADER]: correlationId,
        [PORTAL_SESSION_HEADER]: sessionToken,
      },
    },
  );

  if (internalResponse.status >= 300 && internalResponse.status < 400) {
    const location = internalResponse.headers.get("Location");
    if (!location) {
      return controlledPortalError(
        503,
        "DOWNLOAD_UNAVAILABLE",
        "Le tÃ©lÃ©chargement demandÃ© n'est pas disponible.",
        correlationId,
      );
    }

    const response = NextResponse.redirect(location, internalResponse.status);
    response.headers.set(CORRELATION_HEADER, correlationId);
    return response;
  }

  if (!internalResponse.ok) {
    const status = internalResponse.status === 404 ? 404 : 503;
    return controlledPortalError(
      status,
      "DOWNLOAD_UNAVAILABLE",
      "Le tÃ©lÃ©chargement demandÃ© n'est pas disponible.",
      correlationId,
    );
  }

  const bytes = await internalResponse.arrayBuffer();
  const response = new NextResponse(bytes, {
    status: 200,
    headers: {
      "Content-Type":
        internalResponse.headers.get("Content-Type")
        ?? "application/octet-stream",
      "Content-Disposition":
        internalResponse.headers.get("Content-Disposition")
        ?? 'attachment; filename="telechargement.bin"',
      [CORRELATION_HEADER]: correlationId,
    },
  });

  return response;
}
