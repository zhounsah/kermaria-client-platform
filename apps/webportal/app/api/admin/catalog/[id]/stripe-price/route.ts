import "server-only";

import type {
  CommercialOfferPayload,
  CommercialOfferSummary,
} from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalSession,
  mutateInternalAdminData,
} from "@/lib/internal-api";
import { getSessionCookieName } from "@/lib/session-config";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
  isStripeConfigured,
} from "@/lib/runtime-config";
import {
  createStripePrice,
  createStripeProduct,
  getStripeMode,
} from "@/lib/stripe";

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

  if (!isStripeConfigured()) {
    return NextResponse.json(
      {
        code: "STRIPE_NOT_CONFIGURED",
        message: "Stripe n'est pas configure.",
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

  const catalogResponse = await fetch(`${internalApiUrl}/internal/admin/catalog`, {
    cache: "no-store",
    signal: AbortSignal.timeout(10000),
    headers: {
      Accept: "application/json",
      ...getInternalServiceHeaders(),
      [CORRELATION_HEADER]: correlationId,
      "X-Portal-Session": sessionToken,
    },
  });

  if (!catalogResponse.ok) {
    return NextResponse.json(
      { code: "CATALOG_UNAVAILABLE", message: "Catalogue indisponible." },
      { status: 503 },
    );
  }

  const catalog = (await catalogResponse.json()) as CommercialOfferSummary[];
  const offer = catalog.find((candidate) => candidate.id === id);
  if (!offer) {
    return NextResponse.json(
      { code: "OFFER_NOT_FOUND", message: "Offre introuvable." },
      { status: 404 },
    );
  }

  if (offer.billingCadence !== "monthly") {
    return NextResponse.json(
      {
        code: "OFFER_NOT_RECURRING",
        message: "Le prix Stripe n'a de sens que sur une offre r?currente.",
      },
      { status: 400 },
    );
  }

  const mode = getStripeMode();
  const existingForMode =
    mode === "live" ? offer.stripePriceIdLive : offer.stripePriceIdTest;
  if (existingForMode) {
    return NextResponse.json(
      {
        code: "PRICE_ALREADY_EXISTS",
        message: `Un prix Stripe ${mode} existe deja pour cette offre.`,
      },
      { status: 409 },
    );
  }

  let stripeProductId: string;
  let stripePriceId: string;
  try {
    stripeProductId = await createStripeProduct(offer.name, offer.description);
  } catch (error) {
    console.error("Stripe create product error:", error);
    return NextResponse.json(
      {
        code: "STRIPE_PRODUCT_ERROR",
        message: "Impossible de cr?er le produit Stripe.",
      },
      { status: 502 },
    );
  }

  try {
    stripePriceId = await createStripePrice(
      stripeProductId,
      offer.priceAmountCents,
      offer.currency,
      offer.billingIntervalMonths ?? 1,
    );
  } catch (error) {
    console.error("Stripe create price error:", error);
    return NextResponse.json(
      {
        code: "STRIPE_PRICE_ERROR",
        message:
          "Le produit Stripe a ete cree mais la creation du prix a echoue.",
      },
      { status: 502 },
    );
  }

  const payload: CommercialOfferPayload = {
    name: offer.name,
    description: offer.description,
    category: offer.category,
    unitLabel: offer.unitLabel,
    priceAmountCents: offer.priceAmountCents,
    status: offer.status,
    displayOrder: offer.displayOrder,
    billingCadence: offer.billingCadence,
    setupFeeAmountCents: offer.setupFeeAmountCents,
    billingIntervalMonths: offer.billingIntervalMonths,
    commitmentMonths: offer.commitmentMonths,
    paymentMode: offer.paymentMode,
    publicPackCode: offer.publicPackCode,
    paypalPlanIdSandbox: offer.paypalPlanIdSandbox,
    paypalPlanIdLive: offer.paypalPlanIdLive,
    stripePriceIdTest: mode === "test" ? stripePriceId : offer.stripePriceIdTest,
    stripePriceIdLive: mode === "live" ? stripePriceId : offer.stripePriceIdLive,
  };

  try {
    await mutateInternalAdminData<unknown, CommercialOfferPayload>(
      `/internal/admin/catalog/${encodeURIComponent(id)}`,
      "PATCH",
      payload,
      sessionToken,
      correlationId,
    );
  } catch (error) {
    console.error("Persist Stripe price id error:", error);
    return NextResponse.json(
      {
        code: "PERSIST_ERROR",
        message:
          `Le prix Stripe ${mode} ${stripePriceId} a ete cree chez Stripe `
          + "mais n'a pas pu etre enregistre localement. Copiez-le et "
          + "saisissez-le manuellement.",
      },
      { status: 503 },
    );
  }

  return NextResponse.json({
    stripePriceId,
    stripeProductId,
    mode,
  });
}
