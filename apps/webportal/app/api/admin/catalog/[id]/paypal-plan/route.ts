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
import {
  createPayPalPlan,
  createPayPalProduct,
  getPayPalMode,
  isPayPalConfigured,
} from "@/lib/paypal";
import { getSessionCookieName } from "@/lib/session-config";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
} from "@/lib/runtime-config";

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

  if (!isPayPalConfigured()) {
    return NextResponse.json(
      {
        code: "PAYPAL_NOT_CONFIGURED",
        message: "PayPal n'est pas configure.",
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
        message: "Le plan PayPal n'a de sens que sur une offre recurrente.",
      },
      { status: 400 },
    );
  }

  const mode = getPayPalMode();
  const existingForMode =
    mode === "live" ? offer.paypalPlanIdLive : offer.paypalPlanIdSandbox;
  if (existingForMode) {
    return NextResponse.json(
      {
        code: "PLAN_ALREADY_EXISTS",
        message: `Un plan PayPal ${mode} existe deja pour cette offre.`,
      },
      { status: 409 },
    );
  }

  let paypalProductId: string;
  let paypalPlanId: string;
  try {
    paypalProductId = await createPayPalProduct(offer.name, offer.description);
  } catch (error) {
    console.error("PayPal create product error:", error);
    return NextResponse.json(
      {
        code: "PAYPAL_PRODUCT_ERROR",
        message: "Impossible de creer le produit PayPal.",
      },
      { status: 502 },
    );
  }

  try {
    paypalPlanId = await createPayPalPlan(
      paypalProductId,
      offer.name,
      offer.priceAmountCents,
      offer.currency,
      {
        billingIntervalMonths: offer.billingIntervalMonths ?? 1,
        setupFeeAmountCents: offer.setupFeeAmountCents ?? 0,
      },
    );
  } catch (error) {
    console.error("PayPal create plan error:", error);
    return NextResponse.json(
      {
        code: "PAYPAL_PLAN_ERROR",
        message:
          "Le produit PayPal a ete cree mais la creation du plan a echoue.",
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
    paypalPlanIdSandbox:
      mode === "sandbox" ? paypalPlanId : offer.paypalPlanIdSandbox,
    paypalPlanIdLive: mode === "live" ? paypalPlanId : offer.paypalPlanIdLive,
    stripePriceIdTest: offer.stripePriceIdTest,
    stripePriceIdLive: offer.stripePriceIdLive,
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
    console.error("Persist PayPal plan id error:", error);
    return NextResponse.json(
      {
        code: "PERSIST_ERROR",
        message:
          `Le plan PayPal ${mode} ${paypalPlanId} a ete cree chez PayPal `
          + "mais n'a pas pu etre enregistre localement. Copiez-le et "
          + "saisissez-le manuellement.",
      },
      { status: 503 },
    );
  }

  return NextResponse.json({
    paypalPlanId,
    paypalProductId,
    mode,
  });
}
