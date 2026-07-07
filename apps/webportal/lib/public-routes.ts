import "server-only";

import type { NextRequest } from "next/server";

import {
  isPublicRoute,
  PORTFOLIO_URL,
  PUBLIC_ROUTES,
} from "./public-route-config";

const LOCAL_HOSTNAMES = new Set(["localhost", "127.0.0.1", "::1"]);

type PortalRequestLike = Pick<NextRequest, "headers" | "nextUrl">;

export { isPublicRoute, PORTFOLIO_URL, PUBLIC_ROUTES };

export function isVitrinePublicEnabled(): boolean {
  return process.env.PUBLIC_VITRINE_ENABLED?.trim().toLowerCase() === "true";
}

export function isSignupEnabled(): boolean {
  return process.env.SIGNUP_ENABLED?.trim().toLowerCase() === "true";
}

function normalizeAbsoluteUrl(value: string): string | null {
  try {
    const url = new URL(value);
    if (!["http:", "https:"].includes(url.protocol)) {
      return null;
    }
    return url.toString().replace(/\/+$/, "");
  } catch {
    return null;
  }
}

function isLocalAbsoluteUrl(value: string): boolean {
  try {
    return LOCAL_HOSTNAMES.has(new URL(value).hostname);
  } catch {
    return false;
  }
}

function getRequestOrigin(request: PortalRequestLike): string | null {
  const forwardedProto = request.headers
    .get("x-forwarded-proto")
    ?.split(",")[0]
    ?.trim();
  const forwardedHost = request.headers
    .get("x-forwarded-host")
    ?.split(",")[0]
    ?.trim();
  const host = forwardedHost || request.headers.get("host")?.trim();
  const protocol =
    forwardedProto || request.nextUrl.protocol.replace(/:$/, "") || "https";

  if (host) {
    return normalizeAbsoluteUrl(`${protocol}://${host}`);
  }

  return normalizeAbsoluteUrl(request.nextUrl.origin);
}

export function getPortalPublicUrl(request?: PortalRequestLike): string {
  const requestOrigin = request ? getRequestOrigin(request) : null;
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
