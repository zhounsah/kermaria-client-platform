import "server-only";

import type {
  CommercialOfferSummary,
  SubscriptionSummary,
} from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalApiError,
  getInternalSession,
  mutateInternalPortalPayloadTyped,
} from "@/lib/internal-api";
import {
  createPayPalSubscription,
  getPayPalMode,
  isPayPalConfigured,
} from "@/lib/paypal";
import { getPortalPublicUrl } from "@/lib/public-routes";
import { getSessionCookieName } from "@/lib/session-config";
import {
  isBillingV2AuthoritativeCheckoutBffEnabled,
  getInternalApiUrl,
  getInternalServiceHeaders,
  isStripeConfigured,
} from "@/lib/runtime-config";
import {
  createStripeSubscriptionCheckoutSession,
  getStripeMode,
} from "@/lib/stripe";

const PORTAL_SESSION_HEADER = "X-Portal-Session";

type BillingV2AuthoritativeCheckoutResponse = {
  created: boolean;
  subscriptionId: string;
  provider: string;
  environment: string;
  outboxEventId: string;
  idempotencyKeyHash: string;
  totalDueNowCents: number;
  reasonCode: string;
  approvalUrl?: string | null;
  correlationId: string;
};

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  let body: { offerId?: string; rail?: string };
  try {
    body = await request.json();
  } catch {
    return NextResponse.json(
      { code: "INVALID_REQUEST", message: "Corps de requete invalide." },
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

  const rail = body.rail === "stripe" ? "stripe" : "paypal";
  const useBillingV2AuthoritativeCheckout =
    isBillingV2AuthoritativeCheckoutBffEnabled();

  if (
    !useBillingV2AuthoritativeCheckout
    && rail === "stripe"
    && !isStripeConfigured()
  ) {
    return NextResponse.json(
      {
        code: "STRIPE_NOT_CONFIGURED",
        message: "Le paiement Stripe n'est pas disponible.",
      },
      { status: 503 },
    );
  }
  if (
    !useBillingV2AuthoritativeCheckout
    && rail === "paypal"
    && !isPayPalConfigured()
  ) {
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

  let session;
  try {
    session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "client_user") {
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

  const portalUrl = getPortalPublicUrl(request);
  const cancelPath = "/profile/subscriptions?subscription=cancelled";

  if (useBillingV2AuthoritativeCheckout) {
    const idempotencyKey = request.headers.get("Idempotency-Key")?.trim();
    if (!idempotencyKey || idempotencyKey.length > 128) {
      return NextResponse.json(
        {
          code: "BILLING_V2_IDEMPOTENCY_KEY_REQUIRED",
          message:
            "Une cle d'idempotence est requise pour initialiser un checkout Billing V2.",
        },
        { status: 400 },
      );
    }

    const returnPath =
      rail === "stripe"
        ? `/api/subscriptions/billing-v2/return?provider=stripe&offerId=${encodeURIComponent(
            offer.id,
          )}&session_id={CHECKOUT_SESSION_ID}`
        : `/api/subscriptions/billing-v2/return?provider=paypal&offerId=${encodeURIComponent(
            offer.id,
          )}`;
    let result: BillingV2AuthoritativeCheckoutResponse;
    try {
      result = await mutateInternalPortalPayloadTyped<
        BillingV2AuthoritativeCheckoutResponse,
        {
          legacyOfferId: string;
          provider: string;
          idempotencyKey: string;
          successUrl: string;
          cancelUrl: string;
        }
      >(
        "/internal/portal/billing-v2/subscriptions/checkout",
        {
          legacyOfferId: offer.id,
          provider: rail,
          idempotencyKey,
          successUrl: `${portalUrl}${returnPath}`,
          cancelUrl: `${portalUrl}${cancelPath}`,
        },
        sessionToken,
        correlationId,
      );
    } catch (error) {
      const failure = getInternalApiError(error);
      return NextResponse.json(failure.error, { status: failure.status });
    }

    if (result.approvalUrl) {
      return NextResponse.json({
        approveUrl: result.approvalUrl,
        subscriptionId: result.subscriptionId,
        correlation_id: result.correlationId,
      });
    }

    return NextResponse.json(
      {
        code: "BILLING_V2_CHECKOUT_PENDING_PROVIDER_SESSION",
        message:
          "Le checkout Billing V2 local est pret, mais la session fournisseur n'est pas encore disponible.",
        subscriptionId: result.subscriptionId,
        outboxEventId: result.outboxEventId,
        correlation_id: result.correlationId,
      },
      { status: 409 },
    );
  }

  if (rail === "stripe") {
    const mode = getStripeMode();
    const activePriceId =
      mode === "live" ? offer.stripePriceIdLive : offer.stripePriceIdTest;
    if (
      offer.billingCadence !== "monthly"
      || !activePriceId
      || offer.status !== "active"
    ) {
      return NextResponse.json(
        {
          code: "OFFER_NOT_SUBSCRIBABLE",
          message:
            `Cette offre n'a pas de prix Stripe ${mode}. Demandez a un admin `
            + "de cr?er le prix avant de souscrire.",
        },
        { status: 400 },
      );
    }

    const returnPath = `/api/subscriptions/stripe/return?offerId=${encodeURIComponent(
      offer.id,
    )}&session_id={CHECKOUT_SESSION_ID}`;

    try {
      const result = await createStripeSubscriptionCheckoutSession({
        priceId: activePriceId,
        customerEmail: session.user.email,
        successUrl: `${portalUrl}${returnPath}`,
        cancelUrl: `${portalUrl}${cancelPath}`,
        setupFeeAmountCents: offer.setupFeeAmountCents ?? 0,
        currency: offer.currency,
        setupFeeLabel: `Mise en service - ${offer.name}`,
      });
      return NextResponse.json({
        subscriptionId: null,
        approveUrl: result.approveUrl,
      });
    } catch (error) {
      console.error("Stripe create subscription checkout error:", error);
      return NextResponse.json(
        {
          code: "STRIPE_ERROR",
          message: "Impossible de cr?er la souscription Stripe.",
        },
        { status: 503 },
      );
    }
  }

  const mode = getPayPalMode();
  const activePlanId =
    mode === "live" ? offer.paypalPlanIdLive : offer.paypalPlanIdSandbox;
  if (
    offer.billingCadence !== "monthly"
    || !activePlanId
    || offer.status !== "active"
  ) {
    return NextResponse.json(
      {
        code: "OFFER_NOT_SUBSCRIBABLE",
        message:
          `Cette offre n'a pas de plan PayPal ${mode}. Demandez a un admin `
          + "de cr?er le plan avant de souscrire.",
      },
      { status: 400 },
    );
  }

  const returnPath = `/api/subscriptions/return?offerId=${encodeURIComponent(
    offer.id,
  )}`;

  let paypalSubscriptionId: string;
  let approveUrl: string;
  try {
    const result = await createPayPalSubscription(
      activePlanId,
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
        message: "Impossible de cr?er la souscription PayPal.",
      },
      { status: 503 },
    );
  }

  let summary: SubscriptionSummary;
  try {
    summary = await mutateInternalPortalPayloadTyped<
      SubscriptionSummary,
      { offerId: string; rail: string; paypalSubscriptionId: string }
    >(
      "/internal/portal/subscriptions",
      { offerId: offer.id, rail: "paypal", paypalSubscriptionId },
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
