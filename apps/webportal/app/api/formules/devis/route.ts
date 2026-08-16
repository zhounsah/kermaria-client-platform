import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { getInternalApiError, quoteBillingV2Formule } from "@/lib/internal-api";
import { readBillingV2SelectionPayload } from "@/lib/billing-v2-selection";

/**
 * Devis Billing V2 pour le configurateur public.
 *
 * Projection pure : aucune ecriture, aucune intention creee, aucun objet
 * provider touche. Le corps recu ne porte que des codes catalogue — tout champ
 * tarifaire envoye par le navigateur serait ignore par API-INTERNAL, qui
 * recalcule le montant avec BillingV2PricingEngine.
 */
export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  let payload: unknown;
  try {
    payload = await request.json();
  } catch {
    return NextResponse.json(
      {
        code: "INVALID_REQUEST",
        message: "Le corps de la requete est invalide.",
        correlation_id: correlationId,
      },
      { status: 400, headers: { [CORRELATION_HEADER]: correlationId } },
    );
  }

  const selection = readBillingV2SelectionPayload(payload);
  if (!selection) {
    return NextResponse.json(
      {
        code: "INVALID_REQUEST",
        message: "La configuration demandee est incomplete.",
        correlation_id: correlationId,
      },
      { status: 400, headers: { [CORRELATION_HEADER]: correlationId } },
    );
  }

  try {
    const quote = await quoteBillingV2Formule(selection, correlationId);
    return NextResponse.json(quote, {
      headers: { [CORRELATION_HEADER]: correlationId },
    });
  } catch (error) {
    const failure = getInternalApiError(error);
    return NextResponse.json(failure.error, {
      status: failure.status,
      headers: { [CORRELATION_HEADER]: correlationId },
    });
  }
}
