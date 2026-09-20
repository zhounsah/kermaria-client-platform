import { NextRequest, NextResponse } from "next/server";

import type { BillingV2CartCommandRequest } from "@kermaria/shared";

import { rejectInvalidPortalCsrf } from "@/lib/portal-bff";
import { commandBillingV2Cart } from "@/lib/internal-api";
import { readBillingV2SelectionPayload } from "@/lib/billing-v2-selection";
import { readPortalSessionToken } from "@/lib/session-cookie";
import {
  clearAnonymousCartToken,
  refreshAnonymousCartToken,
  readAnonymousCartToken,
  resolveAnonymousCartToken,
} from "@/lib/cart-cookie";
import { resolveCorrelationId } from "@/lib/correlation";

// `quote` persiste un snapshot expire dans API-INTERNAL : seul `get` est une
// lecture. Toutes les autres commandes gardent donc la protection CSRF BFF.
const readOnlyCommands = new Set(["get", "get_current", "project_legacy_selection"]);
// Le BFF ne reclassifie jamais les resultats metier. Cette liste exprime
// seulement les commandes qui renouvellent l'activite du Cart cote API.
const cartActivityCommands = new Set([
  "current",
  "import_formula_selection",
  "initialize_preset",
  "replace_preset",
  "add_item",
  "add_preset_item",
  "update_item",
  "remove_item",
  "set_commitment",
  "set_payment_mode",
]);
// Seules les commandes qui peuvent materialiser un Cart ont le droit de
// proposer un nouveau token anonyme. Le token reste un candidat jusqu'a une
// reponse Cart reussie : consulter/configurer une formule ne pose donc aucun
// cookie Cart.
const cartCreatingCommands = new Set(["current", "import_formula_selection"]);

function isCartCommand(value: unknown): value is BillingV2CartCommandRequest {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Record<string, unknown>;
  return typeof candidate.command === "string" &&
    ["current", "get_current", "import_formula_selection", "initialize_preset", "replace_preset", "get", "add_item", "add_preset_item", "update_item", "remove_item", "set_commitment", "set_payment_mode", "quote", "project_legacy_selection", "expire", "claim", "claim_current"].includes(candidate.command);
}

function sanitizeCartCommand(payload: BillingV2CartCommandRequest): BillingV2CartCommandRequest {
  const formulaSelection = payload.formulaSelection
    ? readBillingV2SelectionPayload(payload.formulaSelection)
    : null;
  return {
    command: payload.command,
    ...(typeof payload.cartId === "string" ? { cartId: payload.cartId } : {}),
    ...(typeof payload.itemId === "string" ? { itemId: payload.itemId } : {}),
    ...(typeof payload.currency === "string" ? { currency: payload.currency } : {}),
    ...(Number.isInteger(payload.expectedVersion) ? { expectedVersion: payload.expectedVersion } : {}),
    ...(typeof payload.commitmentCode === "string" || payload.commitmentCode === null
      ? { commitmentCode: payload.commitmentCode }
      : {}),
    ...(payload.paymentMode === "monthly" || payload.paymentMode === "upfront" || payload.paymentMode === null
      ? { paymentMode: payload.paymentMode }
      : {}),
    ...(typeof payload.presetCode === "string" ? { presetCode: payload.presetCode } : {}),
    ...(typeof payload.presetItemId === "string" ? { presetItemId: payload.presetItemId } : {}),
    ...(formulaSelection ? { formulaSelection } : {}),
    ...(payload.item ? {
      item: {
        serviceCode: payload.item.serviceCode,
        ...(typeof payload.item.tierCode === "string" || payload.item.tierCode === null ? { tierCode: payload.item.tierCode } : {}),
        quantity: payload.item.quantity,
        ...(typeof payload.item.scopeTemplate === "string" || payload.item.scopeTemplate === null ? { scopeTemplate: payload.item.scopeTemplate } : {}),
        ...(typeof payload.item.subjectBinding === "string" || payload.item.subjectBinding === null ? { subjectBinding: payload.item.subjectBinding } : {}),
        ...(typeof payload.item.sourcePresetId === "string" || payload.item.sourcePresetId === null ? { sourcePresetId: payload.item.sourcePresetId } : {}),
        ...(typeof payload.item.sourcePresetItemId === "string" || payload.item.sourcePresetItemId === null ? { sourcePresetItemId: payload.item.sourcePresetItemId } : {}),
        ...(typeof payload.item.configurationKind === "string" || payload.item.configurationKind === null ? { configurationKind: payload.item.configurationKind } : {}),
        ...(typeof payload.item.configurationReference === "string" || payload.item.configurationReference === null ? { configurationReference: payload.item.configurationReference } : {}),
        origin: payload.item.origin,
      },
    } : {}),
  };
}

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(request.headers.get("X-Correlation-Id"));
  let payload: unknown;
  try {
    payload = await request.json();
  } catch {
    return NextResponse.json({ code: "INVALID_REQUEST", correlation_id: correlationId }, { status: 400 });
  }
  if (!isCartCommand(payload)) {
    return NextResponse.json({ code: "INVALID_REQUEST", correlation_id: correlationId }, { status: 400 });
  }
  if (!readOnlyCommands.has(payload.command)) {
    const csrf = rejectInvalidPortalCsrf(request);
    if (csrf) return csrf;
  }
  const sessionToken = await readPortalSessionToken();
  const anonymousToken = sessionToken
    ? await readAnonymousCartToken()
    : cartCreatingCommands.has(payload.command)
      ? await resolveAnonymousCartToken()
      : await readAnonymousCartToken();
  // Une lecture de header sans session ni cookie ne doit ni appeler
  // API-INTERNAL ni fabriquer un proprietaire anonyme qui n'existe pas.
  if (payload.command === "get_current" && !sessionToken && !anonymousToken) {
    return NextResponse.json({ code: "CART_NOT_FOUND", cart: null, quote: null }, {
      headers: { "X-Correlation-Id": correlationId },
    });
  }
  try {
    const result = await commandBillingV2Cart(
      { ...sanitizeCartCommand(payload), anonymousToken: anonymousToken ?? undefined },
      correlationId,
      sessionToken,
    );
    const response = NextResponse.json(result, {
      headers: { "X-Correlation-Id": correlationId },
    });
    if ((payload.command === "claim" || payload.command === "claim_current") && result.code === "CART_CLAIMED") {
      clearAnonymousCartToken(response);
    }
    if (!sessionToken && anonymousToken && result.cart && cartActivityCommands.has(payload.command)) {
      refreshAnonymousCartToken(response, anonymousToken);
    }
    return response;
  } catch (error) {
    const candidate = error as { status?: number; apiError?: unknown };
    return NextResponse.json(
      candidate.apiError ?? { code: "INTERNAL_API_UNAVAILABLE", correlation_id: correlationId },
      { status: candidate.status ?? 503, headers: { "X-Correlation-Id": correlationId } },
    );
  }
}
