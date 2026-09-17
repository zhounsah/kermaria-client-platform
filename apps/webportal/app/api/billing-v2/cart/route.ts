import { NextRequest, NextResponse } from "next/server";

import type { BillingV2CartCommandRequest } from "@kermaria/shared";

import { rejectInvalidPortalCsrf } from "@/lib/portal-bff";
import { commandBillingV2Cart } from "@/lib/internal-api";
import { readPortalSessionToken } from "@/lib/session-cookie";
import {
  clearAnonymousCartToken,
  ensureAnonymousCartToken,
  refreshAnonymousCartToken,
  readAnonymousCartToken,
} from "@/lib/cart-cookie";
import { resolveCorrelationId } from "@/lib/correlation";

// `quote` persiste un snapshot expire dans API-INTERNAL : seul `get` est une
// lecture. Toutes les autres commandes gardent donc la protection CSRF BFF.
const readOnlyCommands = new Set(["get"]);
// Ces resultats ont execute le touch transactionnel du Cart. Le TTL du cookie
// anonyme doit etre prolonge par la meme reponse.
const cartActivityRenewalCodes = new Set([
  "CART_OK",
  "CART_ITEM_ADDED",
  "CART_ITEM_UPDATED",
  "CART_ITEM_REMOVED",
  "CART_COMMITMENT_UPDATED",
  "CART_PAYMENT_MODE_UPDATED",
]);

function isCartCommand(value: unknown): value is BillingV2CartCommandRequest {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Record<string, unknown>;
  return typeof candidate.command === "string" &&
    ["current", "get", "add_item", "update_item", "remove_item", "set_commitment", "set_payment_mode", "quote", "expire", "claim"].includes(candidate.command);
}

function sanitizeCartCommand(payload: BillingV2CartCommandRequest): BillingV2CartCommandRequest {
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
    : payload.command === "current"
      ? await ensureAnonymousCartToken()
      : await readAnonymousCartToken();
  try {
    const result = await commandBillingV2Cart(
      { ...sanitizeCartCommand(payload), anonymousToken: anonymousToken ?? undefined },
      correlationId,
      sessionToken,
    );
    const response = NextResponse.json(result, {
      headers: { "X-Correlation-Id": correlationId },
    });
    if (payload.command === "claim" && result.code === "CART_CLAIMED") {
      clearAnonymousCartToken(response);
    }
    if (!sessionToken && anonymousToken && cartActivityRenewalCodes.has(result.code)) {
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
