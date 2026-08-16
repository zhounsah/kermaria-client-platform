import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { getInternalApiError, quoteBillingV2Formule } from "@/lib/internal-api";

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

  const selection = readSelection(payload);
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

/**
 * Reconstruction stricte : on ne relaie que les champs attendus. Un corps
 * enrichi par le client ne peut donc pas atteindre API-INTERNAL.
 */
function readSelection(payload: unknown) {
  if (typeof payload !== "object" || payload === null) {
    return null;
  }

  const source = payload as Record<string, unknown>;
  const presetCode = readString(source.presetCode);
  const storagePersonalTierCode = readString(source.storagePersonalTierCode);
  if (!presetCode || !storagePersonalTierCode) {
    return null;
  }

  return {
    presetCode,
    commitmentCode: readString(source.commitmentCode) ?? "FLEX",
    storagePersonalTierCode,
    backupPersonal: source.backupPersonal === true,
    storageSharedTierCode: readString(source.storageSharedTierCode),
    backupShared: source.backupShared === true,
    vpnTierCode: readString(source.vpnTierCode),
    remoteDesktop: source.remoteDesktop === true,
    additionalUsers:
      typeof source.additionalUsers === "number"
      && Number.isInteger(source.additionalUsers)
        ? source.additionalUsers
        : 0,
    supportPlus: source.supportPlus === true,
  };
}

function readString(value: unknown) {
  if (typeof value !== "string") {
    return null;
  }

  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}
