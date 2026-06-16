import "server-only";

import type {
  AdminAdStatus,
  AdminCommercialDocumentDetail,
  AdminCommercialDocumentSummary,
  AdminActivityOverview,
  AdminAuditLogEntry,
  AdminCustomerDetail,
  AdminCustomerSummary,
  AdminOverview,
  AdminServiceRequestDetail,
  AdminServiceRequestSummary,
  AdminSessionSummary,
  AdminSupportRequestDetail,
  AdminSupportRequestSummary,
  ApiError,
  ClientProfile,
  CommercialDocumentDetail,
  CommercialDocumentSummary,
  CommercialOfferSummary,
  CorrelationId,
  CustomerAdLinkSummary,
  DataSource,
  InvoiceSummary,
  InternalSession,
  InternalSessionCreated,
  LoginPayload,
  MockSubmissionResponse,
  NotificationReadResponse,
  PortalSummary,
  PortalNotificationSummary,
  PortalServiceRequestDetail,
  PortalSupportRequestDetail,
  RequestMutationResponse,
  ServiceCatalogItem,
  ServiceRequestPayload,
  ServiceRequestSummary,
  ServiceSummary,
  SupportRequestPayload,
  SupportRequestSummary,
} from "@kermaria/shared";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
} from "@/lib/runtime-config";
import { logBffFailure } from "@/lib/bff-observability";
import { readPortalSessionToken } from "@/lib/session-cookie";
import {
  mockCommercialDocumentDetails,
  mockCommercialDocuments,
  mockCommercialOffers,
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

const PORTAL_SESSION_HEADER = "X-Portal-Session";
const INTERNAL_API_TIMEOUT_MS = 10000;

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

  let internalApiUrl: string | undefined;
  try {
    internalApiUrl = getInternalApiUrl();
  } catch {
    return {
      data: unavailableValue,
      source: "unavailable",
      correlationId,
      error: unavailableError(correlationId),
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
      signal: AbortSignal.timeout(INTERNAL_API_TIMEOUT_MS),
      headers: {
        Accept: "application/json",
        ...getInternalServiceHeaders(),
        [CORRELATION_HEADER]: correlationId,
        [PORTAL_SESSION_HEADER]: sessionToken,
      },
    });

    if (!response.ok) {
      throw await toInternalApiError(response, correlationId);
    }

    return {
      data: await readInternalJson<T>(response, correlationId),
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
  const internalApiUrl = getInternalApiUrl();

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
      signal: AbortSignal.timeout(INTERNAL_API_TIMEOUT_MS),
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        ...getInternalServiceHeaders(),
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

  return readInternalJson<MockSubmissionResponse>(
    response,
    correlationId,
  );
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

export function getCommercialCatalog() {
  return getPortalData<CommercialOfferSummary[]>(
    "/internal/portal/catalog",
    mockCommercialOffers,
    [],
  );
}

export function getCommercialDocuments() {
  return getPortalData<CommercialDocumentSummary[]>(
    "/internal/portal/commercial-documents",
    mockCommercialDocuments,
    [],
  );
}

export function getCommercialDocument(id: string) {
  return getPortalData<CommercialDocumentDetail | null>(
    `/internal/portal/commercial-documents/${encodeURIComponent(id)}`,
    mockCommercialDocumentDetails[id] ?? null,
    null,
  );
}

export function getSupportRequests() {
  return getPortalData<SupportRequestSummary[]>(
    "/internal/portal/support-requests",
    mockSupportRequests,
    [],
  );
}

export function getServiceRequests() {
  return getPortalData<ServiceRequestSummary[]>(
    "/internal/portal/service-requests",
    [],
    [],
  );
}

export function getNotifications() {
  return getPortalData<PortalNotificationSummary[]>(
    "/internal/portal/notifications",
    [],
    [],
  );
}

export function getSupportRequest(id: string) {
  return getPortalData<PortalSupportRequestDetail | null>(
    `/internal/portal/support-requests/${encodeURIComponent(id)}`,
    null,
    null,
  );
}

export function getServiceRequest(id: string) {
  return getPortalData<PortalServiceRequestDetail | null>(
    `/internal/portal/service-requests/${encodeURIComponent(id)}`,
    null,
    null,
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

export async function getInternalPortalData<T>(
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

export async function mutateInternalPortalData(
  path: string,
  sessionToken: string,
  correlationId = resolveCorrelationId(null),
) {
  return requestInternalAuth<NotificationReadResponse>(
    path,
    {
      method: "POST",
      headers: {
        [PORTAL_SESSION_HEADER]: sessionToken,
      },
    },
    correlationId,
  );
}

export async function mutateInternalPortalPayload<TPayload>(
  path: string,
  payload: TPayload,
  sessionToken: string,
  correlationId = resolveCorrelationId(null),
) {
  return requestInternalAuth<RequestMutationResponse>(
    path,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        [PORTAL_SESSION_HEADER]: sessionToken,
      },
      body: JSON.stringify(payload),
    },
    correlationId,
  );
}

export async function mutateInternalAdminData<
  TResponse = RequestMutationResponse,
  TPayload = unknown,
>(
  path: string,
  method: "PATCH" | "POST" | "DELETE",
  payload: TPayload | undefined,
  sessionToken: string,
  correlationId = resolveCorrelationId(null),
) {
  return requestInternalAuth<TResponse>(
    path,
    {
      method,
      headers: {
        [PORTAL_SESSION_HEADER]: sessionToken,
        ...(payload === undefined
          ? {}
          : { "Content-Type": "application/json" }),
      },
      ...(payload === undefined
        ? {}
        : { body: JSON.stringify(payload) }),
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

  let internalApiUrl: string | undefined;
  try {
    internalApiUrl = getInternalApiUrl();
  } catch {
    return {
      data: unavailableValue,
      source: "unavailable",
      correlationId,
      error: unavailableError(correlationId),
    };
  }

  try {
    if (!internalApiUrl) {
      throw new InternalApiError(unavailableError(correlationId), 503);
    }

    const response = await fetch(`${internalApiUrl}${path}`, {
      cache: "no-store",
      signal: AbortSignal.timeout(INTERNAL_API_TIMEOUT_MS),
      headers: {
        Accept: "application/json",
        ...getInternalServiceHeaders(),
        [CORRELATION_HEADER]: correlationId,
        [PORTAL_SESSION_HEADER]: sessionToken,
      },
    });

    if (!response.ok) {
      throw await toInternalApiError(response, correlationId);
    }

    return {
      data: await readInternalJson<T>(response, correlationId),
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

export function getAdminActivity() {
  return getAdminData<AdminActivityOverview | null>(
    "/internal/admin/activity",
    null,
  );
}

export function getAdminCustomers() {
  return getAdminData<AdminCustomerSummary[]>(
    "/internal/admin/customers",
    [],
  );
}

export function getAdminCustomer(customerReference: string) {
  return getAdminData<AdminCustomerDetail | null>(
    `/internal/admin/customers/${encodeURIComponent(customerReference)}`,
    null,
  );
}

export function getAdminAdStatus() {
  return getAdminData<AdminAdStatus | null>(
    "/internal/admin/ad/status",
    null,
  );
}

export function getAdminCustomerAdLinks(customerReference: string) {
  return getAdminData<CustomerAdLinkSummary[]>(
    `/internal/admin/customers/${encodeURIComponent(customerReference)}/ad-links`,
    [],
  );
}

export function getAdminCatalog() {
  return getAdminData<CommercialOfferSummary[]>(
    "/internal/admin/catalog",
    [],
  );
}

export function getAdminCommercialDocuments() {
  return getAdminData<AdminCommercialDocumentSummary[]>(
    "/internal/admin/commercial-documents",
    [],
  );
}

export function getAdminCommercialDocument(id: string) {
  return getAdminData<AdminCommercialDocumentDetail | null>(
    `/internal/admin/commercial-documents/${encodeURIComponent(id)}`,
    null,
  );
}

export function getAdminSupportRequests() {
  return getAdminData<AdminSupportRequestSummary[]>(
    "/internal/admin/support-requests",
    [],
  );
}

export function getAdminSupportRequestsFiltered(query: string) {
  return getAdminData<AdminSupportRequestSummary[]>(
    `/internal/admin/support-requests${query}`,
    [],
  );
}

export function getAdminServiceRequests() {
  return getAdminData<AdminServiceRequestSummary[]>(
    "/internal/admin/service-requests",
    [],
  );
}

export function getAdminServiceRequestsFiltered(query: string) {
  return getAdminData<AdminServiceRequestSummary[]>(
    `/internal/admin/service-requests${query}`,
    [],
  );
}

export function getAdminSupportRequest(id: string) {
  return getAdminData<AdminSupportRequestDetail | null>(
    `/internal/admin/support-requests/${encodeURIComponent(id)}`,
    null,
  );
}

export function getAdminServiceRequest(id: string) {
  return getAdminData<AdminServiceRequestDetail | null>(
    `/internal/admin/service-requests/${encodeURIComponent(id)}`,
    null,
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
  const internalApiUrl = getInternalApiUrl();

  if (!internalApiUrl) {
    throw new InternalApiError(unavailableError(correlationId), 503);
  }

  let response: Response;

  try {
    response = await fetch(`${internalApiUrl}${path}`, {
      ...init,
      cache: "no-store",
      signal: init.signal ?? AbortSignal.timeout(INTERNAL_API_TIMEOUT_MS),
      headers: {
        Accept: "application/json",
        ...getInternalServiceHeaders(),
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

  return readInternalJson<T>(response, correlationId);
}

async function readInternalJson<T>(
  response: Response,
  correlationId: CorrelationId,
): Promise<T> {
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.toLowerCase().includes("application/json")) {
    throw invalidInternalResponse(correlationId);
  }

  try {
    return (await response.json()) as T;
  } catch {
    throw invalidInternalResponse(correlationId);
  }
}

function invalidInternalResponse(correlationId: CorrelationId) {
  return new InternalApiError(
    {
      code: "INVALID_INTERNAL_RESPONSE",
      message: "Le service interne a retourné une réponse inutilisable.",
      correlation_id: correlationId,
    },
    502,
  );
}

export async function checkInternalApiReadiness(
  correlationId: CorrelationId,
) {
  const internalApiUrl = getInternalApiUrl();

  if (!internalApiUrl) {
    return false;
  }

  try {
    const response = await fetch(`${internalApiUrl}/health/ready`, {
      cache: "no-store",
      headers: {
        Accept: "application/json",
        ...getInternalServiceHeaders(),
        [CORRELATION_HEADER]: correlationId,
      },
      signal: AbortSignal.timeout(5000),
    });

    return response.ok;
  } catch {
    return false;
  }
}

export function getInternalApiError(error: unknown) {
  if (error instanceof InternalApiError) {
    const failure = {
      error: error.apiError,
      status: error.status,
    };
    logInternalApiFailure(
      error,
      failure.status,
      failure.error.code,
      failure.error.correlation_id,
    );
    return failure;
  }

  const correlationId = resolveCorrelationId(null);
  const failure = {
    error: unavailableError(correlationId),
    status: 503,
  };
  logInternalApiFailure(
    error,
    failure.status,
    failure.error.code,
    failure.error.correlation_id,
  );

  return failure;
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

function logInternalApiFailure(
  error: unknown,
  status: number,
  code: string,
  correlationId: CorrelationId,
) {
  if (
    status < 500
    && code !== "INTERNAL_API_UNAVAILABLE"
    && code !== "INVALID_INTERNAL_RESPONSE"
  ) {
    return;
  }

  logBffFailure({
    category:
      error instanceof InternalApiError
        ? "internal_api_response"
        : "internal_api_transport",
    code,
    correlation_id: correlationId,
    operation: "internal-api.request",
    status,
    surface: "webportal-bff",
  });
}
