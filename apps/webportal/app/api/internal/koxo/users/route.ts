import { timingSafeEqual } from "node:crypto";

import { NextRequest, NextResponse } from "next/server";

import { logBffFailure } from "@/lib/bff-observability";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
  getKoxoExportAllowedIps,
  getKoxoExportApiToken,
  shouldRequireKoxoExportHttps,
} from "@/lib/runtime-config";

const LOCAL_HOSTNAMES = new Set(["localhost", "127.0.0.1", "::1"]);
const INTERNAL_TIMEOUT_MS = 10_000;
const SOURCE_ADDRESS_HEADER = "X-Koxo-Source-Address";

export async function GET(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  let expectedToken: string;
  let internalApiUrl: string | undefined;
  try {
    expectedToken = getKoxoExportApiToken();
    internalApiUrl = getInternalApiUrl();
  } catch (error) {
    logBffFailure({
      category: "koxo",
      code: "KOXO_EXPORT_MISCONFIGURED",
      correlation_id: correlationId,
      operation: "koxo.export.config",
      status: 503,
      surface: "webportal-bff",
      detail: error instanceof Error ? error.message : undefined,
    });
    return NextResponse.json(
      {
        code: "KOXO_EXPORT_MISCONFIGURED",
        message: "La route d'export KoXo n'est pas configurée.",
        correlation_id: correlationId,
      },
      { status: 503 },
    );
  }

  if (!internalApiUrl) {
    return NextResponse.json(
      {
        code: "INTERNAL_API_UNAVAILABLE",
        message: "L'API interne KoXo est indisponible.",
        correlation_id: correlationId,
      },
      { status: 503 },
    );
  }

  if (shouldRequireKoxoExportHttps() && !isHttpsRequest(request)) {
    return NextResponse.json(
      {
        code: "HTTPS_REQUIRED",
        message: "L'export KoXo exige HTTPS hors environnement local.",
        correlation_id: correlationId,
      },
      { status: 400 },
    );
  }

  const sourceAddress = getSourceAddress(request);
  const normalizedSourceAddress = normalizeIpAddress(sourceAddress);
  const allowedIps = getKoxoExportAllowedIps().map(normalizeIpAddress);
  if (
    allowedIps.length > 0
    && (!normalizedSourceAddress || !allowedIps.includes(normalizedSourceAddress))
  ) {
    logBffFailure({
      category: "koxo",
      code: "KOXO_EXPORT_IP_FORBIDDEN",
      correlation_id: correlationId,
      operation: "koxo.export.allowlist",
      status: 403,
      surface: "webportal-bff",
      detail: normalizedSourceAddress ?? sourceAddress ?? "unknown",
    });
    return NextResponse.json(
      {
        code: "KOXO_EXPORT_IP_FORBIDDEN",
        message: "L'adresse source n'est pas autorisée.",
        correlation_id: correlationId,
      },
      { status: 403 },
    );
  }

  const providedToken = readBearerToken(request);
  if (!providedToken || !matchesSecret(providedToken, expectedToken)) {
    logBffFailure({
      category: "koxo",
      code: "KOXO_EXPORT_AUTH_REQUIRED",
      correlation_id: correlationId,
      operation: "koxo.export.auth",
      status: 401,
      surface: "webportal-bff",
    });
    return NextResponse.json(
      {
        code: "KOXO_EXPORT_AUTH_REQUIRED",
        message: "Un jeton bearer valide est requis.",
        correlation_id: correlationId,
      },
      { status: 401 },
    );
  }

  try {
    const upstream = await fetch(`${internalApiUrl}/internal/koxo/users`, {
      method: "GET",
      cache: "no-store",
      signal: AbortSignal.timeout(INTERNAL_TIMEOUT_MS),
      headers: {
        Accept: "application/json",
        ...getInternalServiceHeaders(),
        [CORRELATION_HEADER]: correlationId,
        ...(normalizedSourceAddress
          ? { [SOURCE_ADDRESS_HEADER]: normalizedSourceAddress }
          : {}),
      },
    });

    const payloadText = await upstream.text();
    const contentType = upstream.headers.get("content-type") ?? "";
    if (!contentType.toLowerCase().includes("application/json")) {
      throw new Error("INVALID_INTERNAL_RESPONSE");
    }

    return new NextResponse(payloadText, {
      status: upstream.status,
      headers: {
        "Cache-Control": "no-store",
        "Content-Type": "application/json; charset=utf-8",
        [CORRELATION_HEADER]:
          upstream.headers.get(CORRELATION_HEADER) ?? correlationId,
      },
    });
  } catch {
    logBffFailure({
      category: "koxo",
      code: "KOXO_EXPORT_UNAVAILABLE",
      correlation_id: correlationId,
      operation: "koxo.export.proxy",
      status: 502,
      surface: "webportal-bff",
    });
    return NextResponse.json(
      {
        code: "KOXO_EXPORT_UNAVAILABLE",
        message: "L'export KoXo n'a pas pu être récupéré.",
        correlation_id: correlationId,
      },
      { status: 502 },
    );
  }
}

function readBearerToken(request: NextRequest) {
  const authorization = request.headers.get("authorization")?.trim() ?? "";
  if (!authorization.startsWith("Bearer ")) {
    return null;
  }

  const token = authorization.slice("Bearer ".length).trim();
  return token.length > 0 ? token : null;
}

function matchesSecret(left: string, right: string) {
  const leftBuffer = Buffer.from(left, "utf8");
  const rightBuffer = Buffer.from(right, "utf8");
  return leftBuffer.length === rightBuffer.length
    && timingSafeEqual(leftBuffer, rightBuffer);
}

function isHttpsRequest(request: NextRequest) {
  if (LOCAL_HOSTNAMES.has(request.nextUrl.hostname)) {
    return true;
  }

  const forwardedProto = request.headers
    .get("x-forwarded-proto")
    ?.split(",")[0]
    ?.trim();
  if (forwardedProto) {
    return forwardedProto === "https";
  }

  return request.nextUrl.protocol === "https:";
}

function getSourceAddress(request: NextRequest) {
  const forwardedFor = request.headers.get("x-forwarded-for");
  if (forwardedFor) {
    const first = forwardedFor.split(",")[0]?.trim();
    if (first) {
      return first;
    }
  }

  const realIp = request.headers.get("x-real-ip")?.trim();
  return realIp || null;
}

function normalizeIpAddress(value: string | null) {
  if (!value) {
    return null;
  }

  const normalized = value.trim().toLowerCase();
  if (normalized.startsWith("::ffff:")) {
    return normalized.slice("::ffff:".length);
  }

  return normalized;
}
