import "server-only";

import type {
  AdHealthStatus,
  AdminAuditLogEntry,
  AdminCustomerSummary,
  AdminOverview,
  AdminServiceRequestSummary,
  AdminSessionSummary,
  AdminSupportRequestSummary,
  ApiError,
  ClientProfile,
  CorrelationId,
  DataSource,
  InvoiceSummary,
  InternalSession,
  InternalSessionCreated,
  LoginPayload,
  MockSubmissionResponse,
  PortalSummary,
  ServiceCatalogItem,
  ServiceRequestPayload,
  ServiceSummary,
  SupportRequestPayload,
  SupportRequestSummary,
} from "@kermaria/shared";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { readPortalSessionToken } from "@/lib/session-cookie";
import {
  mockCustomer,
  mockInvoices,
  mockPortalSummary,
  mockServiceCatalog,
  mockServices,
  mockSupportRequests,
} from "@/lib/mock-data";

export type PortalDataResult<T> = {
  data: T;
  source: DataSource;
  correlationId: CorrelationId;
  error?: ApiError;
};

class InternalApiError extends Error {
  constructor(
    public readonly apiError: ApiError,
    public readonly status: number,
  ) {
    super(apiError.message);
  }
}

const internalApiUrl = process.env.INTERNAL_API_URL?.replace(/\/+$/, "");
const PORTAL_SESSION_HEADER = "X-Portal-Session";

if (!internalApiUrl && process.env.NODE_ENV !== "production") {
  console.warn(
    "INTERNAL_API_URL absente : fallback mock local réservé au développement.",
  );
}

function isDevelopmentFallbackAllowed() {
  return process.env.NODE_ENV !== "production";
}

function unavailableError(correlationId: CorrelationId): ApiError {
  return {
    code: "INTERNAL_API_UNAVAILABLE",
    message: "Les données de démonstration sont temporairement indisponibles.",
    correlation_id: correlationId,
  };
}

async function getPortalData<T>(
  path: string,
  localFallback: T,
  unavailableValue: T,
): Promise<PortalDataResult<T>> {
  const correlationId = resolveCorrelationId(null);
  const sessionToken = await readPortalSessionToken();

  if (!sessionToken) {
    return {
      data: unavailableValue,
      source: "unavailable",
      correlationId,
      error: {
        code: "SESSION_REQUIRED",
        message: "Une session valide est requise.",
        correlation_id: correlationId,
      },
    };
  }

  // Le fallback local est réservé au développement lorsque l'URL interne
  // n'est pas configurée. Il ne doit pas masquer une panne d'API configurée.
  if (!internalApiUrl) {
    if (isDevelopmentFallbackAllowed()) {
      return {
        data: localFallback,
        source: "local-fallback",
        correlationId,
      };
    }

    return {
      data: unavailableValue,
      source: "unavailable",
      correlationId,
      error: unavailableError(correlationId),
    };
  }

  try {
    const response = await fetch(`${internalApiUrl}${path}`, {
      cache: "no-store",
      headers: {
        Accept: "application/json",
        [CORRELATION_HEADER]: correlationId,
        [PORTAL_SESSION_HEADER]: sessionToken,
      },
    });

    if (!response.ok) {
      throw await toInternalApiError(response, correlationId);
    }

    return {
      data: (await response.json()) as T,
      source:
        response.headers.get("X-Data-Source") === "mariadb"
          ? "api-internal-persistent"
          : "api-internal-mock",
      correlationId: resolveCorrelationId(
        response.headers.get(CORRELATION_HEADER),
      ),
    };
  } catch (error) {
    const apiError =
      error instanceof InternalApiError
        ? error.apiError
        : unavailableError(correlationId);

    return {
      data: unavailableValue,
      source: "unavailable",
      correlationId,
      error: apiError,
    };
  }
}

async function postPortalData<TPayload>(
  path: string,
  payload: TPayload,
  correlationId: CorrelationId,
  sessionToken: string,
): Promise<MockSubmissionResponse> {
  if (!internalApiUrl) {
    if (!isDevelopmentFallbackAllowed()) {
      throw new InternalApiError(
        unavailableError(correlationId),
        503,
      );
    }

    return {
      reference: `LOCAL-MOCK-${crypto.randomUUID().slice(0, 8).toUpperCase()}`,
      status: "mock_received",
      persisted: false,
      message:
        "Demande reçue par le fallback local. Aucune donnée n'a été persistée.",
      correlation_id: correlationId,
    };
  }

  let response: Response;

  try {
    response = await fetch(`${internalApiUrl}${path}`, {
      method: "POST",
      cache: "no-store",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        [CORRELATION_HEADER]: correlationId,
        [PORTAL_SESSION_HEADER]: sessionToken,
      },
      body: JSON.stringify(payload),
    });
  } catch {
    throw new InternalApiError(unavailableError(correlationId), 503);
  }

  if (!response.ok) {
    throw await toInternalApiError(response, correlationId);
  }

  return (await response.json()) as MockSubmissionResponse;
}

async function toInternalApiError(
  response: Response,
  fallbackCorrelationId: CorrelationId,
) {
  try {
    const payload = (await response.json()) as Partial<ApiError>;
    const correlationId = resolveCorrelationId(
      payload.correlation_id
        ?? response.headers.get(CORRELATION_HEADER)
        ?? fallbackCorrelationId,
    );

    return new InternalApiError(
      {
        code: payload.code ?? "INTERNAL_API_ERROR",
        message:
          payload.message
          ?? "La demande n'a pas pu être traitée.",
        correlation_id: correlationId,
      },
      response.status,
    );
  } catch {
    return new InternalApiError(
      {
        code: "INTERNAL_API_ERROR",
        message: "La demande n'a pas pu être traitée.",
        correlation_id: fallbackCorrelationId,
      },
      response.status,
    );
  }
}

export function getPortalSummary() {
  return getPortalData<PortalSummary | null>(
    "/internal/portal/summary",
    mockPortalSummary,
    null,
  );
}

export function getClientProfile() {
  return getPortalData<ClientProfile | null>(
    "/internal/portal/profile",
    mockCustomer,
    null,
  );
}

export function getServices() {
  return getPortalData<ServiceSummary[]>(
    "/internal/portal/services",
    mockServices,
    [],
  );
}

export function getInvoices() {
  return getPortalData<InvoiceSummary[]>(
    "/internal/portal/invoices",
    mockInvoices,
    [],
  );
}

export function getServiceCatalog() {
  return getPortalData<ServiceCatalogItem[]>(
    "/internal/portal/service-catalog",
    mockServiceCatalog,
    [],
  );
}

export function getSupportRequests() {
  return getPortalData<SupportRequestSummary[]>(
    "/internal/portal/support-requests",
    mockSupportRequests,
    [],
  );
}

export function getAdHealth() {
  const fallback: AdHealthStatus = {
    mode: "disabled",
    status: "disabled",
    configurationValid: true,
    operationsEnabled: false,
  };

  return getPortalData<AdHealthStatus>(
    "/internal/ad/health",
    fallback,
    fallback,
  );
}

export function createSupportRequest(
  payload: SupportRequestPayload,
  correlationId: CorrelationId,
  sessionToken: string,
) {
  return postPortalData(
    "/internal/portal/support-requests",
    payload,
    correlationId,
    sessionToken,
  );
}

export function createServiceRequest(
  payload: ServiceRequestPayload,
  correlationId: CorrelationId,
  sessionToken: string,
) {
  return postPortalData(
    "/internal/portal/service-requests",
    payload,
    correlationId,
    sessionToken,
  );
}

export async function createInternalSession(
  payload: LoginPayload,
  correlationId: CorrelationId,
  userAgent: string | null,
) {
  return requestInternalAuth<InternalSessionCreated>(
    "/internal/auth/sessions",
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...(userAgent ? { "User-Agent": userAgent.slice(0, 500) } : {}),
      },
      body: JSON.stringify(payload),
    },
    correlationId,
  );
}

export async function getInternalSession(
  sessionToken: string,
  correlationId = resolveCorrelationId(null),
) {
  return requestInternalAuth<InternalSession>(
    "/internal/auth/session",
    {
      method: "GET",
      headers: {
        [PORTAL_SESSION_HEADER]: sessionToken,
      },
    },
    correlationId,
  );
}

export async function revokeInternalSession(
  sessionToken: string,
  correlationId: CorrelationId,
) {
  await requestInternalAuth<void>(
    "/internal/auth/sessions/current",
    {
      method: "DELETE",
      headers: {
        [PORTAL_SESSION_HEADER]: sessionToken,
      },
    },
    correlationId,
  );
}

export async function revokeOtherInternalSessions(
  sessionToken: string,
  correlationId: CorrelationId,
) {
  return requestInternalAuth<{ revokedCount: number }>(
    "/internal/auth/sessions/revoke-others",
    {
      method: "POST",
      headers: {
        [PORTAL_SESSION_HEADER]: sessionToken,
      },
    },
    correlationId,
  );
}

export async function getInternalAdminData<T>(
  path: string,
  sessionToken: string,
  correlationId = resolveCorrelationId(null),
) {
  return requestInternalAuth<T>(
    path,
    {
      method: "GET",
      headers: {
        [PORTAL_SESSION_HEADER]: sessionToken,
      },
    },
    correlationId,
  );
}

async function getAdminData<T>(
  path: string,
  unavailableValue: T,
): Promise<PortalDataResult<T>> {
  const correlationId = resolveCorrelationId(null);
  const sessionToken = await readPortalSessionToken();

  if (!sessionToken) {
    return {
      data: unavailableValue,
      source: "unavailable",
      correlationId,
      error: {
        code: "SESSION_REQUIRED",
        message: "Une session administrateur valide est requise.",
        correlation_id: correlationId,
      },
    };
  }

  try {
    if (!internalApiUrl) {
      throw new InternalApiError(unavailableError(correlationId), 503);
    }

    const response = await fetch(`${internalApiUrl}${path}`, {
      cache: "no-store",
      headers: {
        Accept: "application/json",
        [CORRELATION_HEADER]: correlationId,
        [PORTAL_SESSION_HEADER]: sessionToken,
      },
    });

    if (!response.ok) {
      throw await toInternalApiError(response, correlationId);
    }

    return {
      data: (await response.json()) as T,
      source:
        response.headers.get("X-Data-Source") === "mariadb"
          ? "api-internal-persistent"
          : "api-internal-mock",
      correlationId: resolveCorrelationId(
        response.headers.get(CORRELATION_HEADER),
      ),
    };
  } catch (error) {
    return {
      data: unavailableValue,
      source: "unavailable",
      correlationId,
      error:
        error instanceof InternalApiError
          ? error.apiError
          : unavailableError(correlationId),
    };
  }
}

export function getAdminOverview() {
  return getAdminData<AdminOverview | null>(
    "/internal/admin/overview",
    null,
  );
}

export function getAdminCustomers() {
  return getAdminData<AdminCustomerSummary[]>(
    "/internal/admin/customers",
    [],
  );
}

export function getAdminSupportRequests() {
  return getAdminData<AdminSupportRequestSummary[]>(
    "/internal/admin/support-requests",
    [],
  );
}

export function getAdminServiceRequests() {
  return getAdminData<AdminServiceRequestSummary[]>(
    "/internal/admin/service-requests",
    [],
  );
}

export function getAdminSessions() {
  return getAdminData<AdminSessionSummary[]>(
    "/internal/admin/sessions",
    [],
  );
}

export function getAdminAuditLogs() {
  return getAdminData<AdminAuditLogEntry[]>(
    "/internal/admin/audit-logs",
    [],
  );
}

async function requestInternalAuth<T>(
  path: string,
  init: RequestInit,
  correlationId: CorrelationId,
): Promise<T> {
  if (!internalApiUrl) {
    throw new InternalApiError(unavailableError(correlationId), 503);
  }

  let response: Response;

  try {
    response = await fetch(`${internalApiUrl}${path}`, {
      ...init,
      cache: "no-store",
      headers: {
        Accept: "application/json",
        [CORRELATION_HEADER]: correlationId,
        ...init.headers,
      },
    });
  } catch {
    throw new InternalApiError(unavailableError(correlationId), 503);
  }

  if (!response.ok) {
    throw await toInternalApiError(response, correlationId);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export function getInternalApiError(error: unknown) {
  if (error instanceof InternalApiError) {
    return {
      error: error.apiError,
      status: error.status,
    };
  }

  const correlationId = resolveCorrelationId(null);

  return {
    error: unavailableError(correlationId),
    status: 503,
  };
}

export function resolveDataSource(sources: DataSource[]): DataSource {
  if (sources.includes("unavailable")) {
    return "unavailable";
  }

  if (sources.every((source) => source === "api-internal-persistent")) {
    return "api-internal-persistent";
  }

  if (sources.every((source) => source === "api-internal-mock")) {
    return "api-internal-mock";
  }

  return "local-fallback";
}
