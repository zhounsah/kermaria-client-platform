import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
} from "@/lib/runtime-config";
import {
  isWebhookVerificationEnabled,
  verifyStripeSignature,
} from "@/lib/stripe-webhook";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  const rawBody = await request.text();
  if (!rawBody) {
    return NextResponse.json(
      { code: "INVALID_REQUEST", message: "Empty webhook body." },
      { status: 400 },
    );
  }

  if (isWebhookVerificationEnabled()) {
    const verification = verifyStripeSignature(
      request.headers.get("stripe-signature"),
      rawBody,
    );
    if (!verification.ok) {
      console.error(
        "Stripe webhook signature verification failed:",
        verification.reason,
      );
      return NextResponse.json(
        { code: "SIGNATURE_INVALID", message: verification.reason },
        { status: 401 },
      );
    }
    if (!verification.verified) {
      console.error(
        "Stripe webhook signature rejected:",
        verification.reason,
      );
      return NextResponse.json(
        {
          code: "SIGNATURE_REJECTED",
          message: verification.reason ?? "Signature rejected.",
        },
        { status: 401 },
      );
    }
  } else {
    console.warn(
      "Stripe webhook signature verification disabled via STRIPE_WEBHOOK_VERIFY=false",
    );
  }

  let parsed: {
    id?: string;
    type?: string;
    data?: { object?: { id?: string } };
  };
  try {
    parsed = JSON.parse(rawBody);
  } catch {
    return NextResponse.json(
      { code: "INVALID_REQUEST", message: "Webhook body is not valid JSON." },
      { status: 400 },
    );
  }

  const eventId = parsed.id?.trim();
  const eventType = parsed.type?.trim();
  if (!eventId || !eventType) {
    return NextResponse.json(
      { code: "INVALID_REQUEST", message: "Missing id or type." },
      { status: 400 },
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

  try {
    const response = await fetch(
      `${internalApiUrl}/internal/webhooks/stripe`,
      {
        method: "POST",
        cache: "no-store",
        signal: AbortSignal.timeout(30000),
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
          ...getInternalServiceHeaders(),
          [CORRELATION_HEADER]: correlationId,
        },
        body: JSON.stringify({
          eventId,
          eventType,
          resourceId: parsed.data?.object?.id ?? null,
          rawPayload: rawBody,
        }),
      },
    );
    if (!response.ok) {
      const text = await response.text();
      console.error(
        "Stripe webhook internal processing returned non-2xx:",
        response.status,
        text,
      );
      return NextResponse.json(
        {
          code: "INTERNAL_PROCESS_FAILED",
          message: "Le traitement interne du webhook a échoué.",
        },
        { status: 502 },
      );
    }
    const result = await response.json();
    return NextResponse.json(result);
  } catch (error) {
    console.error("Stripe webhook forwarding error:", error);
    return NextResponse.json(
      {
        code: "INTERNAL_PROCESS_FAILED",
        message: "Le traitement interne du webhook a échoué.",
      },
      { status: 502 },
    );
  }
}
