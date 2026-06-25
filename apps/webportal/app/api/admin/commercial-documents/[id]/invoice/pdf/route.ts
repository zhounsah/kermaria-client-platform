import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { getInternalSession } from "@/lib/internal-api";
import { getSessionCookieName } from "@/lib/session-config";
import { controlledAdminError } from "@/lib/admin-bff";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
} from "@/lib/runtime-config";

const PORTAL_SESSION_HEADER = "X-Portal-Session";

type RouteContext = { params: Promise<{ id: string }> };

export async function GET(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  if (!/^[A-Za-z0-9-]{1,100}$/.test(id)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "L'identifiant demandé est invalide.",
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

  let internalApiUrl: string | undefined;
  try {
    internalApiUrl = getInternalApiUrl();
  } catch {
    return controlledAdminError(
      503,
      "INTERNAL_API_UNAVAILABLE",
      "L'API interne est indisponible.",
      correlationId,
    );
  }
  if (!internalApiUrl) {
    return controlledAdminError(
      503,
      "INTERNAL_API_UNAVAILABLE",
      "L'API interne est indisponible.",
      correlationId,
    );
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "internal_admin") {
      return controlledAdminError(
        403,
        "ACCESS_DENIED",
        "Accès réservé aux administrateurs.",
        correlationId,
      );
    }
  } catch {
    return controlledAdminError(
      401,
      "SESSION_INVALID",
      "Session invalide ou expirée.",
      correlationId,
    );
  }

  const internalResponse = await fetch(
    `${internalApiUrl}/internal/admin/commercial-documents/${encodeURIComponent(id)}/invoice/pdf`,
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
    return controlledAdminError(
      internalResponse.status === 404 ? 404 : 503,
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
