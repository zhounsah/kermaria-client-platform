import "server-only";

import type { ApiError } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalAdminData,
  getInternalApiError,
  getInternalSession,
  mutateInternalAdminData,
} from "@/lib/internal-api";
import { getSessionCookieName } from "@/lib/session-config";

const supportStatuses = new Set([
  "open",
  "in_progress",
  "waiting_for_customer",
  "resolved",
  "closed",
  "cancelled",
]);
const serviceStatuses = new Set([
  "received",
  "under_review",
  "accepted",
  "rejected",
  "cancelled",
  "completed",
]);
const priorities = new Set(["low", "normal", "high"]);
const orders = new Set(["newest", "oldest", "status"]);
const attentionFilters = new Set(["to_handle", "client_reply"]);

type AdminRequestType = "support" | "service";

export async function handleAdminGet<T>(
  request: NextRequest,
  internalPath: string,
) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const sessionToken = request.cookies.get(getSessionCookieName())?.value;

  if (!sessionToken) {
    return controlledAdminError(
      401,
      "UNAUTHORIZED",
      "Une session valide est requise.",
      correlationId,
    );
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "internal_admin") {
      return controlledAdminError(
        403,
        "ACCESS_DENIED",
        "L'accès à cette ressource est refusé.",
        correlationId,
      );
    }

    const data = await getInternalAdminData<T>(
      internalPath,
      sessionToken,
      correlationId,
    );
    const response = NextResponse.json(data);
    response.headers.set(CORRELATION_HEADER, correlationId);
    return response;
  } catch (error) {
    const failure = getInternalApiError(error);
    const response = NextResponse.json(failure.error, {
      status: failure.status,
    });
    response.headers.set(
      CORRELATION_HEADER,
      failure.error.correlation_id,
    );
    return response;
  }
}

export async function handleAdminMutation<TPayload>(
  request: NextRequest,
  internalPath: string,
  method: "PATCH" | "POST",
  payload: TPayload,
) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const sessionToken = request.cookies.get(getSessionCookieName())?.value;

  if (!sessionToken) {
    return controlledAdminError(
      401,
      "UNAUTHORIZED",
      "Une session valide est requise.",
      correlationId,
    );
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "internal_admin") {
      return controlledAdminError(
        403,
        "ACCESS_DENIED",
        "L'accès à cette ressource est refusé.",
        correlationId,
      );
    }

    const data = await mutateInternalAdminData(
      internalPath,
      method,
      payload,
      sessionToken,
      correlationId,
    );
    const response = NextResponse.json(data);
    response.headers.set(CORRELATION_HEADER, data.correlation_id);
    return response;
  } catch (error) {
    const failure = getInternalApiError(error);
    const response = NextResponse.json(failure.error, {
      status: failure.status,
    });
    response.headers.set(
      CORRELATION_HEADER,
      failure.error.correlation_id,
    );
    return response;
  }
}

export function controlledAdminError(
  status: number,
  code: string,
  message: string,
  correlationId: ApiError["correlation_id"],
) {
  const response = NextResponse.json(
    {
      code,
      message,
      correlation_id: correlationId,
    } satisfies ApiError,
    { status },
  );
  response.headers.set(CORRELATION_HEADER, correlationId);
  return response;
}

export function buildAdminRequestListPath(
  request: NextRequest,
  requestType: AdminRequestType,
) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const statuses =
    requestType === "support" ? supportStatuses : serviceStatuses;
  const normalized = new URLSearchParams();
  const allowedKeys = new Set(["status", "priority", "order", "attention"]);

  for (const key of request.nextUrl.searchParams.keys()) {
    if (!allowedKeys.has(key)) {
      return {
        response: controlledAdminError(
          400,
          "INVALID_REQUEST",
          "Les filtres demandés sont invalides.",
          correlationId,
        ),
      };
    }
  }

  const status = request.nextUrl.searchParams.get("status")?.trim();
  if (status && !statuses.has(status)) {
    return invalidAdminFilter(correlationId);
  }
  if (status) normalized.set("status", status);

  const priority = request.nextUrl.searchParams.get("priority")?.trim();
  if (
    priority
    && (requestType !== "support" || !priorities.has(priority))
  ) {
    return invalidAdminFilter(correlationId);
  }
  if (priority) normalized.set("priority", priority);

  const order =
    request.nextUrl.searchParams.get("order")?.trim() || "newest";
  if (!orders.has(order)) {
    return invalidAdminFilter(correlationId);
  }
  normalized.set("order", order);

  const attention = request.nextUrl.searchParams.get("attention")?.trim();
  if (attention && !attentionFilters.has(attention)) {
    return invalidAdminFilter(correlationId);
  }
  if (attention) normalized.set("attention", attention);

  const resource = requestType === "support"
    ? "support-requests"
    : "service-requests";
  return {
    path: `/internal/admin/${resource}?${normalized.toString()}`,
  };
}

function invalidAdminFilter(correlationId: ApiError["correlation_id"]) {
  return {
    response: controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Les filtres demandés sont invalides.",
      correlationId,
    ),
  };
}
