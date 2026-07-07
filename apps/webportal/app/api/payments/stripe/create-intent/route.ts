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

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  if (!isStripeConfigured()) {
    return NextResponse.json(
      { code: "STRIPE_NOT_CONFIGURED", message: "Le paiement Stripe n'est pas disponible." },
      { status: 503 },
    );
  }

  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  if (!sessionToken) {
    return NextResponse.json(
      { code: "UNAUTHORIZED", message: "Session requise." },
      { status: 401 },
    );
  }

  let body: { documentId?: string };
  try {
    body = await request.json();
  } catch {
    return NextResponse.json(
      { code: "INVALID_REQUEST", message: "Corps de requête invalide." },
      { status: 400 },
    );
  }

  const { documentId } = body;
  if (!documentId || !/^[A-Za-z0-9-]{1,100}$/.test(documentId)) {
    return NextResponse.json(
      { code: "INVALID_REQUEST", message: "Identifiant de document invalide." },
      { status: 400 },
    );
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "client_user") {
      return NextResponse.json(
        { code: "ACCESS_DENIED", message: "Accès refusé." },
        { status: 403 },
      );
    }
  } catch {
    return NextResponse.json(
      { code: "SESSION_INVALID", message: "Session invalide." },
      { status: 401 },
    );
  }

  let internalApiUrl: string | undefined;
  try {
    internalApiUrl = getInternalApiUrl();
  } catch {
    /* ignored */
  }
  if (!internalApiUrl) {
    return NextResponse.json(
      { code: "UNAVAILABLE", message: "API interne indisponible." },
      { status: 503 },
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
    return NextResponse.json(
      { code: "DOCUMENT_NOT_FOUND", message: "Document introuvable." },
      { status: 404 },
    );
  }

  const doc = (await docResponse.json()) as CommercialDocumentDetail;

  if (doc.status !== "issued") {
    return NextResponse.json(
      { code: "DOCUMENT_NOT_ISSUED", message: "Seules les factures émises peuvent être réglées." },
      { status: 400 },
    );
  }

  if (doc.totalAmountCents <= 0) {
    return NextResponse.json(
      { code: "AMOUNT_INVALID", message: "Montant invalide." },
      { status: 400 },
    );
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
    return NextResponse.json(
      { code: "STRIPE_ERROR", message: "Impossible de créer la session de paiement." },
      { status: 503 },
    );
  }
}
