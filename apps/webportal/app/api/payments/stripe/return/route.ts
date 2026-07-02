import "server-only";

import { NextRequest, NextResponse } from "next/server";

export async function GET(request: NextRequest) {
  const { searchParams } = request.nextUrl;
  const documentId = searchParams.get("documentId");
  const sessionId = searchParams.get("session_id");

  const portalUrl = process.env.PUBLIC_PORTAL_URL?.replace(/\/$/, "") ?? "http://localhost:3000";

  if (!documentId || !sessionId) {
    return NextResponse.redirect(`${portalUrl}/invoices?payment=error`);
  }

  // Unlike PayPal, the Stripe Checkout Session has already completed payment
  // server-side by the time the browser returns here. Confirmation into BPCE
  // happens asynchronously via the payment_intent.succeeded webhook — this
  // route is purely a redirect, never the source of truth for payment state.
  return NextResponse.redirect(
    `${portalUrl}/commercial-documents/${encodeURIComponent(documentId)}/payment-success`,
  );
}
