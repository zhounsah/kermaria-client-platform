import "server-only";

import type { NextRequest } from "next/server";

import {
  getPortalArea,
  isPublicRoute,
  PORTFOLIO_URL,
  PUBLIC_SITE_URL,
  PUBLIC_ROUTES,
} from "./public-route-config";

const LOCAL_HOSTNAMES = new Set(["localhost", "127.0.0.1", "::1"]);

type PortalRequestLike = Pick<NextRequest, "headers" | "nextUrl">;
type HeaderLookup = Pick<Headers, "get">;

export {
  getPortalArea,
  isPublicRoute,
  PORTFOLIO_URL,
  PUBLIC_SITE_URL,
  PUBLIC_ROUTES,
};

export function isVitrinePublicEnabled(): boolean {
  return process.env.PUBLIC_VITRINE_ENABLED?.trim().toLowerCase() === "true";
}

export function isSignupEnabled(): boolean {
  return process.env.SIGNUP_ENABLED?.trim().toLowerCase() === "true";
}

function normalizeAbsoluteUrl(value: string): string | null {
  try {
    const url = new URL(value);
    if (
      !["http:", "https:"].includes(url.protocol)
      || url.username
      || url.password
    ) {
      return null;
    }
    return url.toString().replace(/\/+$/, "");
  } catch {
    return null;
  }
}

function isLocalAbsoluteUrl(value: string): boolean {
  try {
    const hostname = new URL(value).hostname.toLowerCase();
    return LOCAL_HOSTNAMES.has(
      hostname.startsWith("[") && hostname.endsWith("]")
        ? hostname.slice(1, -1)
        : hostname,
    );
  } catch {
    return false;
  }
}

function isLoopbackHost(host: string): boolean {
  try {
    const hostname = new URL(`http://${host}`).hostname.toLowerCase();
    return LOCAL_HOSTNAMES.has(
      hostname.startsWith("[") && hostname.endsWith("]")
        ? hostname.slice(1, -1)
        : hostname,
    );
  } catch {
    return false;
  }
}

function getRequestOrigin(request: PortalRequestLike): string | null {
  const requestOrigin = getPortalRequestOriginFromHeaders(request.headers);
  if (requestOrigin) {
    return requestOrigin;
  }

  return normalizeAbsoluteUrl(request.nextUrl.origin);
}

export function getPortalRequestOriginFromHeaders(
  headers: HeaderLookup,
): string | null {
  const forwardedProto = headers
    .get("x-forwarded-proto")
    ?.split(",")[0]
    ?.trim()
    .toLowerCase();
  const forwardedHost = headers
    .get("x-forwarded-host")
    ?.split(",")[0]
    ?.trim();
  const host = forwardedHost || headers.get("host")?.trim();
  const protocol = forwardedProto || (host && isLoopbackHost(host)
    ? "http"
    : "https");

  if (
    !host
    || (protocol !== "http" && protocol !== "https")
    || /[/\\?#@\u0000-\u001f\u007f]/.test(host)
  ) {
    return null;
  }

  try {
    const origin = new URL(`${protocol}://${host}`);
    return origin.username || origin.password ? null : origin.origin;
  } catch {
    return null;
  }
}

export function getPortalPublicUrlFromHeaders(headers: HeaderLookup): string {
  const requestOrigin = getPortalRequestOriginFromHeaders(headers);
  if (requestOrigin && !isLocalAbsoluteUrl(requestOrigin)) {
    return requestOrigin;
  }

  const fromEnv = normalizeAbsoluteUrl(process.env.PUBLIC_PORTAL_URL?.trim() ?? "");
  if (fromEnv) {
    return fromEnv;
  }

  if (requestOrigin) {
    return requestOrigin;
  }

  return "http://localhost:3000";
}

export function getPortalPublicUrl(request?: PortalRequestLike): string {
  const requestOrigin = request ? getRequestOrigin(request) : null;
  // PUBLIC_PORTAL_URL reste une configuration de production. Une origine
  // loopback réellement reçue doit toujours gagner, port inclus.
  if (requestOrigin && isLocalAbsoluteUrl(requestOrigin)) {
    return requestOrigin;
  }

  if (requestOrigin) {
    return requestOrigin;
  }

  const fromEnv = normalizeAbsoluteUrl(process.env.PUBLIC_PORTAL_URL?.trim() ?? "");
  if (fromEnv) {
    return fromEnv;
  }

  return "http://localhost:3000";
}
