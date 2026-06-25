import "server-only";

import type {
  CommercialOfferSummary,
  SubscriptionSummary,
} from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalSession,
  mutateInternalPortalPayloadTyped,
} from "@/lib/internal-api";
import { getSessionCookieName } from "@/lib/session-config";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
} from "@/lib/runtime-config";
import {
  createPayPalSubscription,
  isPayPalConfigured,
} from "@/lib/paypal";

const PORTAL_SESSION_HEADER = "X-Portal-Session";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  if (!isPayPalConfigured()) {
    return NextResponse.json(
      {
        code: "PAYPAL_NOT_CONFIGURED",
        message: "Le paiement en ligne n'est pas disponible.",
      },
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

  let body: { offerId?: string };
  try {
    body = await request.json();
  } catch {
    return NextResponse.json(
      { code: "INVALID_REQUEST", message: "Corps de requête invalide." },
      { status: 400 },
    );
  }

  const offerId = body.offerId?.trim();
  if (!offerId || !/^[A-Za-z0-9-]{1,100}$/.test(offerId)) {
    return NextResponse.json(
      { code: "INVALID_REQUEST", message: "Identifiant d'offre invalide." },
      { status: 400 },
    );
  }

  let session;
  try {
    session = await getInternalSession(sessionToken, correlationId);
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

  const catalogResponse = await fetch(
    `${internalApiUrl}/internal/portal/catalog`,
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

  if (!catalogResponse.ok) {
    return NextResponse.json(
      { code: "CATALOG_UNAVAILABLE", message: "Catalogue indisponible." },
      { status: 503 },
    );
  }

  const catalog = (await catalogResponse.json()) as CommercialOfferSummary[];
  const offer = catalog.find((candidate) => candidate.id === offerId);
  if (!offer) {
    return NextResponse.json(
      { code: "OFFER_NOT_FOUND", message: "Offre introuvable." },
      { status: 404 },
    );
  }

  if (
    offer.billingCadence !== "monthly"
    || !offer.paypalPlanId
    || offer.status !== "active"
  ) {
    return NextResponse.json(
      {
        code: "OFFER_NOT_SUBSCRIBABLE",
        message: "Cette offre n'accepte pas de souscription mensuelle.",
      },
      { status: 400 },
    );
  }

  const portalUrl =
    process.env.PUBLIC_PORTAL_URL?.replace(/\/$/, "") ?? "http://localhost:3000";
  const returnPath = `/api/subscriptions/return?offerId=${encodeURIComponent(
    offer.id,
  )}`;
  const cancelPath = `/profile/subscriptions?subscription=cancelled`;

  let paypalSubscriptionId: string;
  let approveUrl: string;
  try {
    const result = await createPayPalSubscription(
      offer.paypalPlanId,
      session.user.email,
      `${portalUrl}${returnPath}`,
      `${portalUrl}${cancelPath}`,
    );
    paypalSubscriptionId = result.subscriptionId;
    approveUrl = result.approveUrl;
  } catch (error) {
    console.error("PayPal create subscription error:", error);
    return NextResponse.json(
      {
        code: "PAYPAL_ERROR",
        message: "Impossible de créer la souscription PayPal.",
      },
      { status: 503 },
    );
  }

  let summary: SubscriptionSummary;
  try {
    summary = await mutateInternalPortalPayloadTyped<
      SubscriptionSummary,
      { offerId: string; paypalSubscriptionId: string }
    >(
      "/internal/portal/subscriptions",
      { offerId: offer.id, paypalSubscriptionId },
      sessionToken,
      correlationId,
    );
  } catch (error) {
    console.error("Subscription persist error:", error);
    return NextResponse.json(
      {
        code: "PERSIST_ERROR",
        message: "Impossible d'enregistrer la souscription.",
      },
      { status: 503 },
    );
  }

  return NextResponse.json({
    subscriptionId: summary.id,
    approveUrl,
  });
}
