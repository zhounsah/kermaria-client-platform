import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { getInternalSession, mutateInternalPortalPayload } from "@/lib/internal-api";
import { getPortalPublicUrl } from "@/lib/public-routes";
import { getSessionCookieName } from "@/lib/session-config";
import { capturePayPalOrder } from "@/lib/paypal";

export async function GET(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  const { searchParams } = request.nextUrl;
  const documentId = searchParams.get("documentId");
  const token = searchParams.get("token"); // PayPal order ID

  const portalUrl = getPortalPublicUrl(request);

  if (!documentId || !token) {
    return NextResponse.redirect(
      `${portalUrl}/invoices?payment=error`,
    );
  }

  const errorUrl = `${portalUrl}/commercial-documents/${encodeURIComponent(documentId)}?payment=error`;
  const successUrl = `${portalUrl}/commercial-documents/${encodeURIComponent(documentId)}/payment-success?rail=paypal`;

  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  if (!sessionToken) {
    return NextResponse.redirect(errorUrl);
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "client_user") {
      return NextResponse.redirect(errorUrl);
    }
  } catch {
    return NextResponse.redirect(errorUrl);
  }

  try {
    const capture = await capturePayPalOrder(token);

    if (capture.status !== "COMPLETED") {
      return NextResponse.redirect(errorUrl);
    }

    await mutateInternalPortalPayload(
      `/internal/portal/commercial-documents/${encodeURIComponent(documentId)}/payment-confirm`,
      {
        paymentMethod: "paypal",
        paypalOrderId: token,
        paypalCaptureId: capture.captureId,
      },
      sessionToken,
      correlationId,
    );
  } catch (error) {
    console.error("PayPal capture/confirm error:", error);
    return NextResponse.redirect(errorUrl);
  }

  return NextResponse.redirect(successUrl);
}
