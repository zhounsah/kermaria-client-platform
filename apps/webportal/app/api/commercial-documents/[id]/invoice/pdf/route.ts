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
      "L'identifiant demandé est invalide.",
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
        "L'accès à cette ressource est refusé.",
        correlationId,
      );
    }
  } catch {
    return controlledPortalError(
      401,
      "SESSION_INVALID",
      "Session invalide ou expirée.",
      correlationId,
    );
  }

  const internalResponse = await fetch(
    `${internalApiUrl}/internal/portal/commercial-documents/${encodeURIComponent(id)}/invoice/pdf`,
    {
      cache: "no-store",
      signal: AbortSignal.timeout(15000),
      headers: {
        ...getInternalServiceHeaders(),
        [CORRELATION_HEADER]: correlationId,
        [PORTAL_SESSION_HEADER]: sessionToken,
      },
    },
  );

  if (!internalResponse.ok) {
    const status = internalResponse.status === 404 ? 404 : 503;
    return controlledPortalError(
      status,
      "PDF_UNAVAILABLE",
      "Le PDF de la facture n'est pas disponible.",
      correlationId,
    );
  }

  const pdfBytes = await internalResponse.arrayBuffer();
  const contentDisposition =
    internalResponse.headers.get("Content-Disposition")
    ?? `attachment; filename="facture.pdf"`;

  return new NextResponse(pdfBytes, {
    status: 200,
    headers: {
      "Content-Type": "application/pdf",
      "Content-Disposition": contentDisposition,
      [CORRELATION_HEADER]: correlationId,
    },
  });
}
