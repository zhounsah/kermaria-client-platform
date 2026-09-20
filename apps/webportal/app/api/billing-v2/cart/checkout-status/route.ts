import { NextRequest, NextResponse } from "next/server";

import { resolveCorrelationId } from "@/lib/correlation";
import { getBillingV2CartCheckoutStatus } from "@/lib/internal-api";
import { readPortalSessionToken } from "@/lib/session-cookie";

/**
 * Reprise strictement authentifiee d'un checkout deja commit. Une lecture ne
 * porte pas de CSRF, mais elle n'a aucun effet de bord et ne revele que l'etat
 * appartenant au client de la session BFF courante.
 */
export async function GET(request: NextRequest) {
  const correlationId = resolveCorrelationId(request.headers.get("X-Correlation-Id"));
  const sessionToken = await readPortalSessionToken();
  if (!sessionToken) {
    return NextResponse.json({ code: "AUTH_REQUIRED", correlation_id: correlationId }, {
      status: 401,
      headers: { "X-Correlation-Id": correlationId },
    });
  }

  const cartId = request.nextUrl.searchParams.get("cartId")?.trim() || null;
  try {
    const result = await getBillingV2CartCheckoutStatus(cartId, correlationId, sessionToken);
    return NextResponse.json({ ...result, correlation_id: correlationId }, {
      headers: { "X-Correlation-Id": correlationId },
    });
  } catch (error) {
    const candidate = error as { status?: number; apiError?: unknown };
    return NextResponse.json(candidate.apiError ?? {
      code: "INTERNAL_API_UNAVAILABLE", correlation_id: correlationId,
    }, {
      status: candidate.status ?? 503,
      headers: { "X-Correlation-Id": correlationId },
    });
  }
}
