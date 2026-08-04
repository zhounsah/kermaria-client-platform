import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  controlledPortalError,
  isValidPortalIdentifier,
} from "@/lib/portal-bff";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
} from "@/lib/runtime-config";

type RouteContext = { params: Promise<{ id: string }> };

// Route publique : le logo d'une solution publiee est un media de vitrine,
// il n'exige donc aucune session. Seules les solutions publiees sont servies,
// le controle est fait par l'API interne.
export async function GET(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  if (!isValidPortalIdentifier(id)) {
    return controlledPortalError(
      400,
      "INVALID_REQUEST",
      "Le logo demandé est invalide.",
      correlationId,
    );
  }

  let internalApiUrl: string | undefined;
  try {
    internalApiUrl = getInternalApiUrl();
  } catch {
    internalApiUrl = undefined;
  }

  if (!internalApiUrl) {
    return controlledPortalError(
      503,
      "INTERNAL_API_UNAVAILABLE",
      "L'API interne est indisponible.",
      correlationId,
    );
  }

  let internalResponse: Response;
  try {
    internalResponse = await fetch(
      `${internalApiUrl}/internal/portal/client-solutions/${encodeURIComponent(id)}/logo`,
      {
        cache: "no-store",
        signal: AbortSignal.timeout(10000),
        headers: {
          ...getInternalServiceHeaders(),
          [CORRELATION_HEADER]: correlationId,
        },
      },
    );
  } catch {
    return controlledPortalError(
      503,
      "CLIENT_SOLUTION_LOGO_UNAVAILABLE",
      "Le logo demandé n'est pas disponible.",
      correlationId,
    );
  }

  if (!internalResponse.ok) {
    return controlledPortalError(
      internalResponse.status === 404 ? 404 : 503,
      "CLIENT_SOLUTION_LOGO_UNAVAILABLE",
      "Le logo demandé n'est pas disponible.",
      correlationId,
    );
  }

  const bytes = await internalResponse.arrayBuffer();
  return new NextResponse(bytes, {
    status: 200,
    headers: {
      "Content-Type":
        internalResponse.headers.get("Content-Type")
        ?? "application/octet-stream",
      "Content-Disposition": "inline",
      // Un logo SVG reste un document actif : on le sert sans script possible.
      "Content-Security-Policy":
        "default-src 'none'; style-src 'unsafe-inline'; sandbox",
      "X-Content-Type-Options": "nosniff",
      "Cache-Control": "public, max-age=300",
      [CORRELATION_HEADER]: correlationId,
    },
  });
}
