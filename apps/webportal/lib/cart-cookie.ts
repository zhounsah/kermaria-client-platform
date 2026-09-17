import "server-only";

import { randomBytes } from "node:crypto";
import { cookies } from "next/headers";
import type { NextResponse } from "next/server";
import { getSessionCookieOptions } from "@/lib/session-config";

export const CART_COOKIE_NAME = "kermaria_billing_v2_cart";
const CART_TOKEN_PATTERN = /^[a-f0-9]{64}$/;
const CART_COOKIE_MAX_AGE_SECONDS = 60 * 60 * 24 * 30;

function cartCookieOptions() {
  return {
    ...getSessionCookieOptions(),
    maxAge: CART_COOKIE_MAX_AGE_SECONDS,
  };
}

export async function readAnonymousCartToken() {
  const cookieStore = await cookies();
  const token = cookieStore.get(CART_COOKIE_NAME)?.value;
  return token && CART_TOKEN_PATTERN.test(token) ? token : null;
}

export async function ensureAnonymousCartToken() {
  const cookieStore = await cookies();
  const existing = cookieStore.get(CART_COOKIE_NAME)?.value;
  if (existing && CART_TOKEN_PATTERN.test(existing)) return existing;
  const token = randomBytes(32).toString("hex");
  cookieStore.set(CART_COOKIE_NAME, token, cartCookieOptions());
  return token;
}

/** Prolonge le cookie lorsque l'API a prolonge l'activite du Cart. */
export function refreshAnonymousCartToken(response: NextResponse, token: string) {
  if (!CART_TOKEN_PATTERN.test(token)) return;
  response.cookies.set(CART_COOKIE_NAME, token, cartCookieOptions());
}

/** Le claim retire le secret navigateur : seul le customer_id reste en base. */
export function clearAnonymousCartToken(response: NextResponse) {
  response.cookies.set({
    ...getSessionCookieOptions(),
    name: CART_COOKIE_NAME,
    value: "",
    expires: new Date(0),
  });
}
