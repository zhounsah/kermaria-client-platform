import "server-only";

import { randomBytes, timingSafeEqual } from "node:crypto";
import type { NextRequest, NextResponse } from "next/server";

import {
  CSRF_COOKIE_NAME,
  CSRF_HEADER_NAME,
} from "@/lib/csrf";
import { getSessionCookieOptions } from "@/lib/session-config";

const csrfTokenPattern = /^[A-Fa-f0-9]{64}$/;

export function ensureCsrfCookie(
  request: NextRequest,
  response: NextResponse,
) {
  const existingToken = request.cookies.get(CSRF_COOKIE_NAME)?.value?.trim();
  const token = isValidCsrfToken(existingToken)
    ? existingToken
    : randomBytes(32).toString("hex");

  response.cookies.set({
    ...getClientCookieOptions(),
    name: CSRF_COOKIE_NAME,
    value: token,
  });

  return token;
}

export function clearCsrfCookie(response: NextResponse) {
  response.cookies.set({
    ...getClientCookieOptions(),
    name: CSRF_COOKIE_NAME,
    value: "",
    expires: new Date(0),
  });
}

export function hasValidCsrfToken(request: NextRequest) {
  const cookieToken = request.cookies.get(CSRF_COOKIE_NAME)?.value?.trim();
  const headerToken = request.headers.get(CSRF_HEADER_NAME)?.trim();

  if (!isValidCsrfToken(cookieToken) || !isValidCsrfToken(headerToken)) {
    return false;
  }

  return timingSafeEqual(
    Buffer.from(cookieToken, "utf8"),
    Buffer.from(headerToken, "utf8"),
  );
}

function getClientCookieOptions() {
  const sessionCookieOptions = getSessionCookieOptions();
  return {
    httpOnly: false,
    sameSite: sessionCookieOptions.sameSite,
    secure: sessionCookieOptions.secure,
    path: sessionCookieOptions.path,
  };
}

function isValidCsrfToken(value: string | null | undefined): value is string {
  return typeof value === "string" && csrfTokenPattern.test(value);
}
