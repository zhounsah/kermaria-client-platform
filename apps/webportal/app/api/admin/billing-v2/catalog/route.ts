import { NextRequest } from "next/server";

import {
  controlledAdminError,
  handleAdminGet,
  handleAdminMutation,
} from "@/lib/admin-bff";
import { parseBillingV2CatalogAdminCommand } from "@/lib/billing-v2-catalog-commands";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

/**
 * Administration du catalogue Billing V2.
 *
 * Une seule route pour toutes les mutations : le corps porte une commande
 * (`kind`) que le BFF reconstruit strictement avant de la relayer. Le BFF
 * n'arbitre rien — il refuse ce qu'il ne reconnait pas, et laisse
 * API-INTERNAL seul juge de la coherence tarifaire, du versionnage et du
 * recouvrement des fenetres de prix.
 */
export function GET(request: NextRequest) {
  return handleAdminGet(request, "/internal/admin/billing-v2/catalog");
}

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  let body: unknown;
  try {
    body = await request.json();
  } catch {
    body = null;
  }

  const command = parseBillingV2CatalogAdminCommand(body);
  if (!command) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La commande de catalogue est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation(
    request,
    `/internal/admin/billing-v2/catalog${command.path}`,
    command.method,
    command.payload,
  );
}
