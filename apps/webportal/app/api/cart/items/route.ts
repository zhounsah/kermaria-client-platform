import type { CartAddPayload } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { controlledPortalError, handlePortalPayloadMutation } from "@/lib/portal-bff";

function resolveRedirectTarget(request: NextRequest, redirectTo: string) {
  const referer = request.headers.get("referer");
  if (referer) {
    return new URL(redirectTo, referer);
  }

  return new URL(redirectTo, request.url);
}

export async function GET(request: NextRequest) {
  const offerId = request.nextUrl.searchParams.get("offerId") ?? "";
  const quantity = Number.parseInt(
    request.nextUrl.searchParams.get("quantity") ?? "1",
    10,
  ) || 1;
  const redirectTo = request.nextUrl.searchParams.get("redirectTo") ?? "/souscrire";

  if (!offerId || !/^[A-Za-z0-9-]{1,100}$/.test(offerId)) {
    return NextResponse.redirect(resolveRedirectTarget(request, "/souscrire?cartError=1"), 303);
  }

  const response = await handlePortalPayloadMutation<CartAddPayload>(
    request,
    "/internal/portal/cart/items",
    { offerId, quantity },
  );

  if (response.ok) {
    return NextResponse.redirect(resolveRedirectTarget(request, redirectTo), 303);
  }

  return NextResponse.redirect(resolveRedirectTarget(request, "/souscrire?cartError=1"), 303);
}

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const contentType = request.headers.get("content-type") ?? "";
  const isFormPost = contentType.includes("application/x-www-form-urlencoded")
    || contentType.includes("multipart/form-data");

  let body: CartAddPayload;
  let redirectTo = "/souscrire";
  try {
    if (isFormPost) {
      const formData = await request.formData();
      body = {
        offerId: String(formData.get("offerId") ?? ""),
        quantity: Number.parseInt(String(formData.get("quantity") ?? "1"), 10) || 1,
      };
      redirectTo = String(formData.get("redirectTo") ?? "/souscrire");
    } else {
      body = (await request.json()) as CartAddPayload;
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

  const response = await handlePortalPayloadMutation<CartAddPayload>(
    request,
    "/internal/portal/cart/items",
    { offerId: body.offerId, quantity: body.quantity },
  );

  if (!isFormPost) {
    return response;
  }

  if (response.ok) {
    return NextResponse.redirect(resolveRedirectTarget(request, redirectTo), 303);
  }

  return NextResponse.redirect(resolveRedirectTarget(request, "/souscrire?cartError=1"), 303);
}
