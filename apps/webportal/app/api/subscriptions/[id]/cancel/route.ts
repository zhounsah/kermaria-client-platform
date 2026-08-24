import "server-only";

import type { SubscriptionSummary } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalApiError,
  getInternalSession,
  mutateInternalPortalPayloadTyped,
} from "@/lib/internal-api";
import { getSessionCookieName } from "@/lib/session-config";

/**
 * Résiliation d'un abonnement par son titulaire.
 *
 * Ce BFF ne parle à aucun opérateur de paiement. Il authentifie, borne, et
 * transmet. C'est API-INTERNAL qui détient les identifiants fournisseur
 * persistés, met la demande en file dans l'outbox Billing V2 et obtient la
 * convergence — un portail public n'a rien à faire dans cette chaîne, et
 * surtout ne doit pas devenir une seconde autorité fournisseur.
 *
 * L'appartenance de l'abonnement n'est pas vérifiée ici : elle l'est côté
 * API-INTERNAL, qui résout l'abonnement dans le périmètre du client de la
 * session. Un identifiant appartenant à un autre client y ressort en 404.
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
      {
        code: "INVALID_REQUEST",
        message: "Identifiant invalide.",
        correlation_id: correlationId,
      },
      { status: 400 },
    );
  }

  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  if (!sessionToken) {
    return NextResponse.json(
      {
        code: "UNAUTHORIZED",
        message: "Session requise.",
        correlation_id: correlationId,
      },
      { status: 401 },
    );
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "client_user") {
      return NextResponse.json(
        {
          code: "ACCESS_DENIED",
          message: "Accès refusé.",
          correlation_id: correlationId,
        },
        { status: 403 },
      );
    }
  } catch {
    return NextResponse.json(
      {
        code: "SESSION_INVALID",
        message: "Session invalide.",
        correlation_id: correlationId,
      },
      { status: 401 },
    );
  }

  try {
    const result = await mutateInternalPortalPayloadTyped<
      SubscriptionSummary,
      undefined
    >(
      `/internal/portal/subscriptions/${encodeURIComponent(id)}/cancel`,
      undefined,
      sessionToken,
      correlationId,
    );
    return NextResponse.json(result);
  } catch (error) {
    console.error("Client cancel subscription error:", error);
    const failure = getInternalApiError(error);

    // L'erreur d'API-INTERNAL est relayée telle quelle. La masquer derrière un
    // succès local afficherait « résilié » à un client encore prélevé.
    return NextResponse.json(failure.error, { status: failure.status });
  }
}
