import type {
  ApiError,
  BillingV2VpsCheckoutResponse,
  CorrelationId,
} from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { hasValidCsrfToken } from "@/lib/csrf-server";
import {
  getInternalApiError,
  mutateInternalPortalPayloadTyped,
} from "@/lib/internal-api";
import { getPortalPublicUrl } from "@/lib/public-routes";
import { getSessionCookieName } from "@/lib/session-config";

type InternalCheckoutResponse = {
  subscriptionId: string;
  approvalUrl?: string | null;
  correlationId: string;
};

type InternalCheckoutPayload = {
  technicalRequestId: string;
  provider: "stripe";
  idempotencyKey: string;
  successUrl: string;
  cancelUrl: string;
};

export const dynamic = "force-dynamic";

/**
 * Le client ne peut transmettre ici que l'identifiant de sa demande technique.
 * API-INTERNAL relit ensuite service, palier, revision et catalogue avant de
 * deleguer au checkout Billing V2 authoritative.
 */
export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(request.headers.get(CORRELATION_HEADER));
  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  if (!sessionToken) return fail("SESSION_REQUIRED", "Une session valide est requise.", 401, correlationId);
  if (!hasValidCsrfToken(request)) return fail("CSRF_FORBIDDEN", "La session doit confirmer cette action.", 403, correlationId);

  const idempotencyKey = request.headers.get("Idempotency-Key")?.trim();
  if (!idempotencyKey || idempotencyKey.length > 128) {
    return fail("BILLING_V2_IDEMPOTENCY_KEY_REQUIRED", "Une clé d’idempotence est requise.", 400, correlationId);
  }

  let candidate: unknown;
  try {
    candidate = await request.json();
  } catch {
    return fail("INVALID_REQUEST", "Le corps de la requête est invalide.", 400, correlationId);
  }
  const technicalRequestId = readTechnicalRequestId(candidate);
  if (!technicalRequestId) {
    return fail("INVALID_REQUEST", "La demande technique VPS est invalide.", 400, correlationId);
  }

  const portalUrl = getPortalPublicUrl(request);
  try {
    const result = await mutateInternalPortalPayloadTyped<
      InternalCheckoutResponse,
      InternalCheckoutPayload
    >(
      "/internal/portal/billing-v2/vps/configurations/checkout",
      {
        technicalRequestId,
        provider: "stripe",
        idempotencyKey,
        successUrl: `${portalUrl}/api/vps/checkout/return?technicalRequestId=${encodeURIComponent(technicalRequestId)}&session_id={CHECKOUT_SESSION_ID}`,
        cancelUrl: `${portalUrl}/services/vps/choisir?checkout=cancelled`,
      },
      sessionToken,
      correlationId,
    );
    if (!result.approvalUrl) {
      return fail(
        "BILLING_V2_CHECKOUT_PENDING_PROVIDER_SESSION",
        "Votre commande est enregistrée, mais la page de paiement n’est pas encore prête. Réessayez dans quelques instants.",
        409,
        correlationId,
      );
    }
    const response: BillingV2VpsCheckoutResponse = {
      approveUrl: result.approvalUrl,
      subscriptionId: result.subscriptionId,
      correlationId: result.correlationId,
    };
    return NextResponse.json(response, {
      headers: { [CORRELATION_HEADER]: result.correlationId },
    });
  } catch (error) {
    const failure = getInternalApiError(error);
    return NextResponse.json(failure.error, {
      status: failure.status,
      headers: { [CORRELATION_HEADER]: failure.error.correlation_id },
    });
  }
}

function readTechnicalRequestId(value: unknown): string | null {
  if (!value || typeof value !== "object") return null;
  const candidate = value as { technicalRequestId?: unknown };
  if (typeof candidate.technicalRequestId !== "string") return null;
  const id = candidate.technicalRequestId.trim();
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(id)
    ? id
    : null;
}

function fail(
  code: string,
  message: string,
  status: number,
  correlationId: CorrelationId,
) {
  const error: ApiError = { code, message, correlation_id: correlationId };
  return NextResponse.json(error, {
    status,
    headers: { [CORRELATION_HEADER]: correlationId },
  });
}
