import "server-only";

import { randomBytes } from "node:crypto";
import { cookies, headers } from "next/headers";
import type { NextResponse } from "next/server";
import { getPortalFamilyCookieDomain } from "@/lib/public-route-config";
import { getPortalRequestOriginFromHeaders } from "@/lib/public-routes";
import { getSessionCookieOptions } from "@/lib/session-config";

export const CART_COOKIE_NAME = "kermaria_billing_v2_cart";
const CART_TOKEN_PATTERN = /^[a-f0-9]{64}$/;
const CART_COOKIE_MAX_AGE_SECONDS = 60 * 60 * 24 * 30;

async function cartCookieOptions() {
  const requestHeaders = await headers();
  const origin = getPortalRequestOriginFromHeaders(requestHeaders);
  const domain = origin
    ? getPortalFamilyCookieDomain(new URL(origin).hostname)
    : null;
  return {
    ...getSessionCookieOptions(),
    maxAge: CART_COOKIE_MAX_AGE_SECONDS,
    ...(domain ? { domain } : {}),
  };
}

export async function readAnonymousCartToken() {
  const cookieStore = await cookies();
  const token = cookieStore.get(CART_COOKIE_NAME)?.value;
  return token && CART_TOKEN_PATTERN.test(token) ? token : null;
}

/**
 * Produit un candidat de possession anonyme sans l'ecrire dans la reponse.
 * Le BFF ne persiste le cookie qu'apres une reponse Cart valide : une panne
 * API ne peut donc pas remplacer un cookie coherent par un token orphelin.
 */
export async function resolveAnonymousCartToken() {
  const existing = await readAnonymousCartToken();
  return existing ?? randomBytes(32).toString("hex");
}

/** Prolonge le cookie lorsque l'API a prolonge l'activite du Cart. */
export async function refreshAnonymousCartToken(response: NextResponse, token: string) {
  if (!CART_TOKEN_PATTERN.test(token)) return;
  response.cookies.set(CART_COOKIE_NAME, token, await cartCookieOptions());
}

/** Le claim retire le secret navigateur : seul le customer_id reste en base. */
export async function clearAnonymousCartToken(response: NextResponse) {
  response.cookies.set({
    ...(await cartCookieOptions()),
    name: CART_COOKIE_NAME,
    value: "",
    expires: new Date(0),
  });
}
