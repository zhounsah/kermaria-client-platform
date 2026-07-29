import type {
  CheckoutRecurringAddPayload,
  CheckoutRecurringMutationResponse,
} from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  controlledPortalError,
  handlePortalPayloadMutationTyped,
} from "@/lib/portal-bff";

function resolveRedirectTarget(request: NextRequest, redirectTo: string) {
  const referer = request.headers.get("referer");
  if (referer) {
    return new URL(redirectTo, referer);
  }

  return new URL(redirectTo, request.url);
}

export async function GET(request: NextRequest) {
  const offerId = request.nextUrl.searchParams.get("offerId") ?? "";
  const redirectTo = request.nextUrl.searchParams.get("redirectTo") ?? "/souscrire";

  if (!offerId || !/^[A-Za-z0-9-]{1,100}$/.test(offerId)) {
    return NextResponse.redirect(
      resolveRedirectTarget(request, "/souscrire?recurringError=1"),
      303,
    );
  }

  const response = await handlePortalPayloadMutationTyped<
    CheckoutRecurringMutationResponse,
    CheckoutRecurringAddPayload
  >(
    request,
    "/internal/portal/checkout/subscriptions/items",
    { offerId },
  );

  if (response.ok) {
    return NextResponse.redirect(resolveRedirectTarget(request, redirectTo), 303);
  }

  return NextResponse.redirect(
    resolveRedirectTarget(request, "/souscrire?recurringError=1"),
    303,
  );
}

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const contentType = request.headers.get("content-type") ?? "";
  const isFormPost = contentType.includes("application/x-www-form-urlencoded")
    || contentType.includes("multipart/form-data");

  let body: CheckoutRecurringAddPayload;
  let redirectTo = "/souscrire";
  try {
    if (isFormPost) {
      const formData = await request.formData();
      body = {
        offerId: String(formData.get("offerId") ?? ""),
      };
      redirectTo = String(formData.get("redirectTo") ?? "/souscrire");
    } else {
      body = (await request.json()) as CheckoutRecurringAddPayload;
    }
  } catch {
    return controlledPortalError(
      400,
      "INVALID_REQUEST",
      "Le corps de la requete est invalide.",
      correlationId,
    );
  }

  if (!body?.offerId || !/^[A-Za-z0-9-]{1,100}$/.test(body.offerId)) {
    return controlledPortalError(
      400,
      "INVALID_REQUEST",
      "L'identifiant de l'offre est invalide.",
      correlationId,
    );
  }

  const response = await handlePortalPayloadMutationTyped<
    CheckoutRecurringMutationResponse,
    CheckoutRecurringAddPayload
  >(
    request,
    "/internal/portal/checkout/subscriptions/items",
    { offerId: body.offerId },
  );

  if (!isFormPost) {
    return response;
  }

  if (response.ok) {
    return NextResponse.redirect(resolveRedirectTarget(request, redirectTo), 303);
  }

  return NextResponse.redirect(
    resolveRedirectTarget(request, "/souscrire?recurringError=1"),
    303,
  );
}
