import "server-only";

import type { NextRequest } from "next/server";

import {
  type PortalArea,
  getPortalArea,
  getPortalFamilyCookieDomain,
  isPublicRoute,
  PORTFOLIO_URL,
  PUBLIC_SITE_URL,
  PUBLIC_ROUTES,
  resolvePortalAreaUrl,
  resolvePortalRoleUrl,
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

/**
 * Une seule URL explicitement configuree est traitee comme le monohost local
 * du portail. Cette exception n'existe que dans l'environnement applicatif
 * Development et ne possede aucun repli vers une URL de production.
 */
export function getDevelopmentLocalPortalOrigin(): string | null {
  if (process.env.APP_ENV?.trim().toLowerCase() !== "development") {
    return null;
  }

  const value = process.env.PUBLIC_PORTAL_URL?.trim();
  if (!value) {
    return null;
  }

  try {
    const url = new URL(value);
    if (
      (url.protocol !== "http:" && url.protocol !== "https:")
      || url.username
      || url.password
      || url.pathname !== "/"
      || url.search
      || url.hash
    ) {
      return null;
    }
    return url.origin;
  } catch {
    return null;
  }
}

export function getPortalAreaForRequest(
  origin: string | null | undefined,
): PortalArea | null {
  return getPortalArea(origin, getDevelopmentLocalPortalOrigin());
}

export function resolvePortalAreaUrlForRequest(
  origin: string | null | undefined,
  area: PortalArea,
  pathname = "/",
): string | null {
  return resolvePortalAreaUrl(
    origin,
    area,
    pathname,
    getDevelopmentLocalPortalOrigin(),
  );
}

export function resolvePortalRoleUrlForRequest(
  origin: string | null | undefined,
  role: string | null | undefined,
  pathname?: string,
): string | null {
  return resolvePortalRoleUrl(
    origin,
    role,
    pathname,
    getDevelopmentLocalPortalOrigin(),
  );
}

export function getPortalFamilyCookieDomainForRequest(
  hostname: string,
): string | null {
  return getPortalFamilyCookieDomain(
    hostname,
    getDevelopmentLocalPortalOrigin(),
  );
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
