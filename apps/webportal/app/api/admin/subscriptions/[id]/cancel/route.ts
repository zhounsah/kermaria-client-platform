import "server-only";

import type { SubscriptionSummary } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalApiError,
  getInternalSession,
  mutateInternalAdminData,
} from "@/lib/internal-api";
import { getSessionCookieName } from "@/lib/session-config";

/**
 * Résiliation administrative d'un abonnement.
 *
 * Comme son homologue client, ce BFF ne contacte aucun opérateur de paiement :
 * il authentifie l'administrateur et transmet. La coupure immédiate d'une
 * période déjà payée est une décision humaine, tracée dans le journal d'audit
 * Billing V2 par API-INTERNAL.
 */
export async function POST(
  request: NextRequest,
  context: { params: Promise<{ id: string }> },
) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;

  if (!/^[A-Za-z0-9-]{1,100}$/.test(id)) {
    return NextResponse.json(
      { code: "INVALID_REQUEST", message: "Identifiant invalide." },
      { status: 400 },
    );
  }

  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  if (!sessionToken) {
    return NextResponse.json(
      { code: "UNAUTHORIZED", message: "Session requise." },
      { status: 401 },
    );
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "internal_admin") {
      return NextResponse.json(
        { code: "ACCESS_DENIED", message: "Acces refuse." },
        { status: 403 },
      );
    }
  } catch {
    return NextResponse.json(
      { code: "SESSION_INVALID", message: "Session invalide." },
      { status: 401 },
    );
  }

  try {
    const result = await mutateInternalAdminData<SubscriptionSummary, undefined>(
      `/internal/admin/subscriptions/${encodeURIComponent(id)}/cancel`,
      "POST",
      undefined,
      sessionToken,
      correlationId,
    );
    return NextResponse.json(result);
  } catch (error) {
    console.error("Admin cancel subscription error:", error);
    const failure = getInternalApiError(error);

    // Relayé tel quel : un échec de convergence fournisseur doit rester
    // visible en administration, pas être converti en succès local.
    return NextResponse.json(failure.error, { status: failure.status });
  }
}
