import "server-only";

import type {
  ApiError,
  CatalogConfigurationInput,
  CatalogConfigurationResolution,
  CorrelationId,
} from "@kermaria/shared";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
} from "@/lib/runtime-config";

const INTERNAL_TIMEOUT_MS = 10_000;

export type CatalogConfigurationResult =
  | {
      ok: true;
      data: CatalogConfigurationResolution;
      correlationId: CorrelationId;
    }
  | {
      ok: false;
      status: number;
      error: ApiError;
      correlationId: CorrelationId;
    };

export async function resolveCatalogConfiguration(
  input: CatalogConfigurationInput,
  correlationId: string = resolveCorrelationId(null),
): Promise<CatalogConfigurationResult> {
  let internalApiUrl: string | undefined;
  try {
    internalApiUrl = getInternalApiUrl();
  } catch {
    return unavailable(correlationId);
  }

  if (!internalApiUrl) {
    return unavailable(correlationId);
  }

  try {
    const upstream = await fetch(
      `${internalApiUrl}/internal/portal/configuration/resolve`,
      {
        method: "POST",
        cache: "no-store",
        signal: AbortSignal.timeout(INTERNAL_TIMEOUT_MS),
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
          ...getInternalServiceHeaders(),
          [CORRELATION_HEADER]: correlationId,
        },
        body: JSON.stringify(input),
      },
    );
    const payload = await safeReadJson(upstream);
    const resolvedCorrelationId = resolveCorrelationId(
      upstream.headers.get(CORRELATION_HEADER) ?? correlationId,
    );

    if (!upstream.ok) {
      return {
        ok: false,
        status: upstream.status,
        error: toApiError(payload, resolvedCorrelationId),
        correlationId: resolvedCorrelationId,
      };
    }

    return {
      ok: true,
      data: payload as unknown as CatalogConfigurationResolution,
      correlationId: resolvedCorrelationId,
    };
  } catch {
    return unavailable(correlationId);
  }
}

function unavailable(correlationId: string): CatalogConfigurationResult {
  const resolved = resolveCorrelationId(correlationId);
  return {
    ok: false,
    status: 503,
    error: {
      code: "INTERNAL_API_UNAVAILABLE",
      message:
        "Le simulateur est temporairement indisponible. Reessayez dans quelques instants.",
      correlation_id: resolved,
    },
    correlationId: resolved,
  };
}

function toApiError(
  payload: Record<string, unknown> | null,
  correlationId: CorrelationId,
): ApiError {
  return {
    code:
      typeof payload?.code === "string"
        ? payload.code
        : "CONFIGURATION_RESOLVE_FAILED",
    message:
      typeof payload?.message === "string"
        ? payload.message
        : "La configuration n'a pas pu etre resolue.",
    correlation_id:
      typeof payload?.correlation_id === "string"
        ? resolveCorrelationId(payload.correlation_id)
        : correlationId,
  };
}

async function safeReadJson(
  response: Response,
): Promise<Record<string, unknown> | null> {
  try {
    const contentType = response.headers.get("content-type") ?? "";
    if (!contentType.toLowerCase().includes("application/json")) {
      return null;
    }
    return (await response.json()) as Record<string, unknown>;
  } catch {
    return null;
  }
}
