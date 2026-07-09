import "server-only";

import type { CommercialDocumentDetail } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { getInternalSession } from "@/lib/internal-api";
import { getPortalPublicUrl } from "@/lib/public-routes";
import { getSessionCookieName } from "@/lib/session-config";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
  isStripeConfigured,
} from "@/lib/runtime-config";
import { createStripeOneShotCheckoutSession } from "@/lib/stripe";

const PORTAL_SESSION_HEADER = "X-Portal-Session";

function errorJson(
  status: number,
  code: string,
  message: string,
  correlationId: string,
) {
  return NextResponse.json(
    {
      code,
      message,
      correlation_id: correlationId,
    },
    { status },
  );
}

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  if (!isStripeConfigured()) {
    return errorJson(
      503,
      "STRIPE_NOT_CONFIGURED",
      "Le paiement Stripe n'est pas disponible.",
      correlationId,
    );
  }

  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  if (!sessionToken) {
    return errorJson(401, "UNAUTHORIZED", "Session requise.", correlationId);
  }

  let body: { documentId?: string };
  try {
    body = await request.json();
  } catch {
    return errorJson(
      400,
      "INVALID_REQUEST",
      "Corps de requête invalide.",
      correlationId,
    );
  }

  const { documentId } = body;
  if (!documentId || !/^[A-Za-z0-9-]{1,100}$/.test(documentId)) {
    return errorJson(
      400,
      "INVALID_REQUEST",
      "Identifiant de document invalide.",
      correlationId,
    );
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "client_user") {
      return errorJson(403, "ACCESS_DENIED", "Accès refusé.", correlationId);
    }
  } catch {
    return errorJson(401, "SESSION_INVALID", "Session invalide.", correlationId);
  }

  let internalApiUrl: string | undefined;
  try {
    internalApiUrl = getInternalApiUrl();
  } catch {
    internalApiUrl = undefined;
  }

  if (!internalApiUrl) {
    return errorJson(
      503,
      "UNAVAILABLE",
      "API interne indisponible.",
      correlationId,
    );
  }

  const docResponse = await fetch(
    `${internalApiUrl}/internal/portal/commercial-documents/${encodeURIComponent(documentId)}`,
    {
      cache: "no-store",
      signal: AbortSignal.timeout(10000),
      headers: {
        Accept: "application/json",
        ...getInternalServiceHeaders(),
        [CORRELATION_HEADER]: correlationId,
        [PORTAL_SESSION_HEADER]: sessionToken,
      },
    },
  );

  if (!docResponse.ok) {
    return errorJson(
      404,
      "DOCUMENT_NOT_FOUND",
      "Document introuvable.",
      correlationId,
    );
  }

  const doc = (await docResponse.json()) as CommercialDocumentDetail;

  if (doc.status !== "issued") {
    return errorJson(
      400,
      "DOCUMENT_NOT_ISSUED",
      "Seules les factures émises peuvent être réglées.",
      correlationId,
    );
  }

  if (doc.totalAmountCents <= 0) {
    return errorJson(400, "AMOUNT_INVALID", "Montant invalide.", correlationId);
  }

  const portalUrl = getPortalPublicUrl(request);
  const successUrl = `${portalUrl}/api/payments/stripe/return?documentId=${encodeURIComponent(documentId)}&session_id={CHECKOUT_SESSION_ID}`;
  const cancelUrl = `${portalUrl}/commercial-documents/${encodeURIComponent(documentId)}?payment=cancelled`;

  try {
    const { sessionId, approveUrl } = await createStripeOneShotCheckoutSession(
      doc.totalAmountCents,
      doc.currency,
      `Règlement facture ${doc.internalReference}`,
      successUrl,
      cancelUrl,
      documentId,
    );

    return NextResponse.json({ sessionId, approveUrl });
  } catch (error) {
    console.error("Stripe create checkout session error:", error);
    return errorJson(
      503,
      "STRIPE_ERROR",
      "Impossible de créer la session de paiement.",
      correlationId,
    );
  }
}
