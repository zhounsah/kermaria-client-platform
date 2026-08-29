import "server-only";

import type {
  AdminAdStatus,
  AdminCommercialDocumentDetail,
  AdminCommercialDocumentSummary,
  AdminActivityOverview,
  ApplicationSettingsSnapshot,
  ConfigurationStatusSnapshot,
  PortalBillingConfiguration,
  AdminAuditLogEntry,
  AdminCustomerDetail,
  AdminCustomerAdWorkspace,
  AdminCustomerSummary,
  AdminOverview,
  AdminServiceRequestDetail,
  AdminServiceRequestSummary,
  AdminSessionSummary,
  AdminSupportRequestDetail,
  AdminSupportRequestSummary,
  BackupIntegrationPayload,
  BackupIntegrationSummary,
  BackupJobDetail,
  BackupJobSummary,
  BackupRestoreRequestPayload,
  BillingV2AdditionalUserSlotSummary,
  AdminClientSolutionPortal,
  ApiError,
  ClientProfile,
  ClientSolution,
  CommercialDocumentDetail,
  CommercialDocumentSummary,
  CommunicationTemplateCollection,
  CorrelationId,
  CustomerAdLinkSummary,
  CustomerAdProvisioningMutationPayload,
  CustomerAdProvisioningMutationResponse,
  DiagnosticConfigurationAdminView,
  BillingV2ConfigurationOverview,
  DemoContentTemplateAdminView,
  IntegrationsOverview,
  RuntimeOverview,
  FiscalPolicyAdminView,
  DiagnosticConfigurationSnapshot,
  DownloadCategory,
  DataSource,
  DownloadResource,
  EditorialContentDetail,
  EditorialContentSummary,
  EditorialListResponse,
  EditorialRedirect,
  EditorialRevisionDetail,
  EditorialRevisionSummary,
  InvoiceSummary,
  InternalSession,
  InternalSessionCreated,
  LoginPayload,
  ManagedContentDetail,
  ManagedContentKey,
  ManagedContentSummary,
  MockSubmissionResponse,
  NotificationReadResponse,
  PendingBillingV2SelectionSummary,
  PortalDownloadCategory,
  PortalSummary,
  PortalNotificationSummary,
  PortalServiceRequestDetail,
  PortalSupportRequestDetail,
  PublicDiagnosticConfigurationResponse,
  PublicSystemSnippets,
  PublicClientSolutionPortal,
  PublicPackCatalogContent,
  RequestMutationResponse,
  ServiceCatalogItem,
  ServiceRequestPayload,
  ServiceRequestSummary,
  ServiceSummary,
  SubscriptionSummary,
  AdminSubscriptionDetail,
  SupportRequestPayload,
  SupportRequestSummary,
  DemoAccountSummary,
  DemoContentTemplateSummary,
  DemoProfileSummary,
  BillingV2PublicCatalog,
  BillingV2PublicQuote,
  BillingV2PublicSelection,
} from "@kermaria/shared";
import {
  createDefaultAdminClientSolutionPortal,
  createDefaultClientSolutionPortal,
  createDefaultPublicPackCatalogContent,
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
  mockCustomer,
  getMockManagedContent,
  mockInvoices,
  mockManagedContentSummaries,
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

/**
 * Catalogue vide volontaire : le webportal ne connait aucun prix Billing V2.
 * Toute valeur tarifaire vient d'API-INTERNAL.
 */
const EMPTY_BILLING_V2_CATALOG: BillingV2PublicCatalog = {
  source: "unavailable",
  currency: "EUR",
  presets: [],
  services: [],
  commitments: [],
};

export type BillingV2AdminRuntimeFlags = {
  newSubscriptionsEnabled: boolean;
  authoritativeCheckoutEnabled: boolean;
  firstRealSubscriptionApproved: boolean;
  providerOutboxEnabled: boolean;
  providerExecutorEnabled: boolean;
  provisioningEnabled: boolean;
};

/**
 * Precondition de lancement : le modele commercial legacy a disparu du schema.
 * Tant qu'une table subsiste, Billing V2 n'est pas la seule autorite et la
 * porte reste fermee.
 */
export type BillingV2AdminLaunchReadiness = {
  legacyBillingSchemaRemoved: boolean;
  verifiedAgainstPersistentSql: boolean;
  remainingLegacyTables: string[];
};

export type BillingV2AdminProviderReadiness = {
  provider: string;
  environment: string;
  providerConfigured: boolean;
  priceMappingsReady: boolean;
  requiredServicePriceCount: number;
  resolvedMappingCount: number;
  missingServicePriceIds: string[];
  ambiguousServicePriceIds: string[];
  readyForCheckout: boolean;
};

export type BillingV2AdminOperationalLimitation = {
  code: string;
  severity: string;
  message: string;
};

export type BillingV2AdminReadinessSnapshot = {
  persistentSqlAvailable: boolean;
  schemaReady: boolean;
  missingSchemaTables: string[];
  runtimeFlags: BillingV2AdminRuntimeFlags;
  launchReadiness: BillingV2AdminLaunchReadiness;
  providers: BillingV2AdminProviderReadiness[];
  operationalLimitations: BillingV2AdminOperationalLimitation[];
  canRequestFirstRealSubscription: boolean;
  reasonCode: string;
  correlationId: string;
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

async function getPublicData<T>(
  path: string,
  localFallback: T,
  unavailableValue: T,
): Promise<PortalDataResult<T>> {
  const correlationId = resolveCorrelationId(null);

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

export function getBackups() {
  return getPortalData<BackupJobSummary[]>(
    "/internal/portal/backups",
    [],
    [],
  );
}

export function getBackup(id: string) {
  return getPortalData<BackupJobDetail | null>(
    `/internal/portal/backups/${encodeURIComponent(id)}`,
    null,
    null,
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

/**
 * Catalogue des formules Billing V2.
 *
 * Aucun prix n'est recopie cote webportal : si API-INTERNAL n'est pas
 * joignable, on renvoie un catalogue VIDE et la page le dit. Un repli local
 * tarifaire ferait du navigateur une seconde autorite financiere, ce que le
 * contrat interdit.
 */
export function getBillingV2FormulesCatalog() {
  return getPublicData<BillingV2PublicCatalog>(
    "/internal/portal/billing-v2/formules",
    EMPTY_BILLING_V2_CATALOG,
    EMPTY_BILLING_V2_CATALOG,
  );
}

/**
 * Devis serveur. La selection ne porte que des codes catalogue ; le montant
 * renvoye est celui calcule par BillingV2PricingEngine.
 */
export async function quoteBillingV2Formule(
  selection: BillingV2PublicSelection,
  correlationId: CorrelationId,
): Promise<BillingV2PublicQuote> {
  const internalApiUrl = getInternalApiUrl();

  if (!internalApiUrl) {
    throw new InternalApiError(unavailableError(correlationId), 503);
  }

  let response: Response;

  try {
    response = await fetch(
      `${internalApiUrl}/internal/portal/billing-v2/formules/devis`,
      {
        method: "POST",
        cache: "no-store",
        signal: AbortSignal.timeout(INTERNAL_API_TIMEOUT_MS),
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
          ...getInternalServiceHeaders(),
          [CORRELATION_HEADER]: correlationId,
        },
        body: JSON.stringify(selection),
      },
    );
  } catch {
    throw new InternalApiError(unavailableError(correlationId), 503);
  }

  if (!response.ok) {
    throw await toInternalApiError(response, correlationId);
  }

  return readInternalJson<BillingV2PublicQuote>(response, correlationId);
}

export function getPublicPackCatalogContent() {
  return getPublicData<PublicPackCatalogContent>(
    "/internal/portal/public-pack-catalog",
    createDefaultPublicPackCatalogContent(),
    createDefaultPublicPackCatalogContent(),
  );
}

export function getPublicClientSolutionPortal() {
  return getPublicData<PublicClientSolutionPortal>(
    "/internal/portal/client-solutions",
    createDefaultClientSolutionPortal(),
    createDefaultClientSolutionPortal(),
  );
}

export function getPublicManagedContent(key: ManagedContentKey) {
  const localFallback = getMockManagedContent(key);

  return getPublicData<ManagedContentDetail | null>(
    `/internal/portal/content/${encodeURIComponent(key)}`,
    localFallback,
    null,
  );
}

export function getPublicSystemSnippets() {
  return getPublicData<PublicSystemSnippets | null>(
    "/internal/public/system-snippets",
    null,
    null,
  );
}

export function getPublicDiagnosticConfiguration() {
  return getPublicData<PublicDiagnosticConfigurationResponse | null>(
    "/internal/public/diagnostic/configuration",
    null,
    null,
  );
}

export function getPublicWikiHome() {
  return getPublicData<EditorialListResponse>(
    "/internal/public/editorial/wiki/home",
    { items: [], categories: [] },
    { items: [], categories: [] },
  );
}

export function searchPublicWiki(query: string) {
  return getPublicData<EditorialContentDetail[]>(
    `/internal/public/editorial/wiki/search?query=${encodeURIComponent(query)}`,
    [],
    [],
  );
}

export function getPublicWikiArticle(slug: string) {
  return getPublicData<EditorialContentDetail | null>(
    `/internal/public/editorial/wiki/articles/${encodeURIComponent(slug)}`,
    null,
    null,
  );
}

export function getPublicSeoPage(slug: string) {
  return getPublicData<EditorialContentDetail | null>(
    `/internal/public/editorial/seo-pages/${encodeURIComponent(slug)}`,
    null,
    null,
  );
}

export function getPublicFaq(scope: string) {
  return getPublicData<EditorialContentDetail[]>(
    `/internal/public/editorial/faq/${encodeURIComponent(scope)}`,
    [],
    [],
  );
}

export function getPublicEditorialSitemap() {
  return getPublicData<EditorialContentSummary[]>(
    "/internal/public/editorial/sitemap",
    [],
    [],
  );
}

export function getEditorialRedirect(oldPath: string) {
  return getPublicData<EditorialRedirect | null>(
    `/internal/public/editorial/redirects?oldPath=${encodeURIComponent(oldPath)}`,
    null,
    null,
  );
}

export function getClientSubscriptions() {
  return getPortalData<SubscriptionSummary[]>(
    "/internal/portal/subscriptions",
    [],
    [],
  );
}

/**
 * Places utilisateur supplementaires d'une souscription Billing V2.
 *
 * Le client n'est pas transmis : l'API le lit dans la session. Une
 * souscription d'une autre organisation renvoie une liste vide, exactement
 * comme une souscription inexistante.
 */
export function getBillingV2AdditionalUsers(subscriptionId: string) {
  return getPortalData<BillingV2AdditionalUserSlotSummary[]>(
    `/internal/portal/billing-v2/subscriptions/${encodeURIComponent(subscriptionId)}/users`,
    [],
    [],
  );
}

export function getClientDownloads() {
  return getPortalData<PortalDownloadCategory[]>(
    "/internal/portal/downloads",
    [],
    [],
  );
}

export function getPendingBillingV2Selection() {
  return getPortalData<PendingBillingV2SelectionSummary | null>(
    "/internal/portal/pending-billing-v2-selection",
    null,
    null,
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

export function getCommercialDocumentInvoice(id: string) {
  return getPortalData<BpceIssuedInvoiceInfo | null>(
    `/internal/portal/commercial-documents/${encodeURIComponent(id)}/invoice`,
    null,
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

export function createBackupRestoreRequest(
  backupJobId: string,
  payload: BackupRestoreRequestPayload,
  correlationId: CorrelationId,
  sessionToken: string,
) {
  return postPortalData(
    `/internal/portal/backups/${encodeURIComponent(backupJobId)}/restore-requests`,
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

export async function mutateInternalPortalPayloadTyped<TResponse, TPayload>(
  path: string,
  payload: TPayload | undefined,
  sessionToken: string,
  correlationId = resolveCorrelationId(null),
) {
  return requestInternalAuth<TResponse>(
    path,
    {
      method: "POST",
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

export async function mutateInternalAdminData<
  TResponse = RequestMutationResponse,
  TPayload = unknown,
>(
  path: string,
  method: "PATCH" | "POST" | "PUT" | "DELETE",
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

export function getAdminBackupIntegrations() {
  return getAdminData<BackupIntegrationSummary[]>(
    "/internal/admin/backups/integrations",
    [],
  );
}

export function upsertInternalBackupIntegration(
  payload: BackupIntegrationPayload,
  sessionToken: string,
  correlationId = resolveCorrelationId(null),
) {
  return mutateInternalAdminData<
    BackupIntegrationSummary,
    BackupIntegrationPayload
  >(
    "/internal/admin/backups/integrations",
    "POST",
    payload,
    sessionToken,
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

export function getAdminApplicationSettings() {
  return getAdminData<ApplicationSettingsSnapshot>(
    "/internal/admin/settings",
    { settings: [], persistent: false },
  );
}

/**
 * Repli d'indisponibilite : aucune version connue, donc `source: "code"`. La
 * page d'administration affiche l'erreur plutot qu'un faux etat vide.
 */
const EMPTY_DIAGNOSTIC_SNAPSHOT: DiagnosticConfigurationSnapshot = {
  state: "draft",
  version: 0,
  source: "code",
  updatedAt: null,
  configuration: null,
};

export function getAdminCommunicationTemplates() {
  return getAdminData<CommunicationTemplateCollection>(
    "/internal/admin/communications",
    {
      emailTemplates: [],
      notificationTemplates: [],
      snippets: [],
      persistent: false,
    },
  );
}

export function getAdminDiagnosticConfiguration() {
  return getAdminData<DiagnosticConfigurationAdminView>(
    "/internal/admin/diagnostic/configuration",
    {
      draft: EMPTY_DIAGNOSTIC_SNAPSHOT,
      published: { ...EMPTY_DIAGNOSTIC_SNAPSHOT, state: "published" },
      draftDiffers: false,
      persistent: false,
    },
  );
}

export function getAdminRuntimeOverview() {
  return getAdminData<RuntimeOverview>(
    "/internal/admin/settings/runtime",
    {
      environment: "",
      version: "",
      configurationPath: null,
      configurationFilePresent: false,
      startedAt: "",
      uptimeSeconds: 0,
      sections: [],
    },
  );
}

export function getAdminIntegrations() {
  return getAdminData<IntegrationsOverview>(
    "/internal/admin/settings/integrations",
    { integrations: [], checkedAt: "" },
  );
}

export function getAdminDemoTemplateConfiguration() {
  return getAdminData<DemoContentTemplateAdminView>(
    "/internal/admin/settings/demo-templates",
    {
      templates: [],
      knownServiceTypes: [],
      revisions: [],
      authority: "code",
      persistent: false,
      commercialTermsLabel: "",
      conversion: {
        environmentVariable: "DEMO_CONVERSION_TARGET_OU_DN",
        targetOrganizationalUnitDn: null,
        configured: false,
        withinAllowedRoots: false,
        allowedRoots: [],
        adIntegrationMode: "disabled",
        classification: "restart_required",
        restartRequired: true,
      },
    },
  );
}

export function getAdminFiscalPolicy() {
  return getAdminData<FiscalPolicyAdminView>(
    "/internal/admin/settings/fiscal-policy",
    { regimes: [], persistent: false },
  );
}

export function getAdminBillingV2Configuration() {
  return getAdminData<BillingV2ConfigurationOverview>(
    "/internal/admin/settings/billing-v2",
    {
      catalog: null,
      readiness: null,
      flags: [],
      reconciliationIntervalSeconds: 0,
      correlationId: "",
    },
  );
}

export function getAdminConfigurationStatus() {
  return getAdminData<ConfigurationStatusSnapshot>("/internal/admin/settings/status", { domains: [] });
}

export function getPortalBillingConfiguration(localFallback: PortalBillingConfiguration) {
  return getPortalData<PortalBillingConfiguration>("/internal/portal/billing-configuration", localFallback, localFallback);
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

export type SignupAdminSummary = {
  id: string;
  status: string;
  companyName: string;
  contactName: string;
  email: string;
  emailVerified: boolean;
  createdAt: string;
  approvedAt: string | null;
  rejectedAt: string | null;
};

export type SignupCustomerData = {
  customerType: string | null;
  displayName: string | null;
  billingEmail: string | null;
  phone: string | null;
  addressLine1: string | null;
  addressLine2: string | null;
  postalCode: string | null;
  city: string | null;
  country: string | null;
};

export type SignupUserData = {
  personalTitle: string | null;
  birthDate: string | null;
  givenName: string | null;
  surname: string | null;
  initials: string | null;
  displayName: string | null;
  email: string | null;
  phone: string | null;
  isPrimaryContact: boolean | null;
};

export type SignupAdminDetail = {
  id: string;
  status: string;
  companyName: string;
  contactName: string;
  email: string;
  phone: string | null;
  message: string | null;
  billingV2Selection: BillingV2PublicSelection | null;
  sourceAddress: string | null;
  rejectedReason: string | null;
  createdAt: string;
  updatedAt: string;
  approvedAt: string | null;
  rejectedAt: string | null;
  customer: SignupCustomerData | null;
  primaryUser: SignupUserData | null;
  accountAccess: SignupAdminAccountAccess | null;
};

export type SignupAdminAccountAccess = {
  customerReference: string | null;
  passwordDefined: boolean;
  passwordSetupExpiresAt: string | null;
  adProvisioningStatus: string | null;
  lastPasswordSyncStatus: string | null;
  koxoExportStatus: string | null;
  samAccountName: string | null;
  userPrincipalName: string | null;
};

export type KoxoExportUser = {
  civilite: string;
  nom: string;
  prenom: string;
  dateNaissance: string;
  identifiantUnique: string;
  groupeSecondaire: string;
  email: string;
};

export type KoxoInvalidUser = {
  identifiantUnique: string | null;
  portalUserId: string;
  fields: string[];
};

export type KoxoExportPayload = {
  schemaVersion: number;
  generatedAt: string;
  userCount: number;
  users: KoxoExportUser[];
};

export type KoxoRunSummary = {
  createdAt: string;
  source: string;
  status: string;
  schemaVersion: number | null;
  userCount: number;
  invalidUserCount: number;
  correlationId: string;
  sourceAddress: string | null;
  summaryMessage: string;
  generatedAt: string | null;
};

export type KoxoAdminDashboard = {
  exportableUserCount: number;
  invalidUserCount: number;
  lastApiCallAt: string | null;
  lastRequestedStatus: string | null;
  schemaVersion: number;
  preview: KoxoExportPayload | null;
  validationErrors: KoxoInvalidUser[];
  lastRun: KoxoRunSummary | null;
};

export function getAdminSignups(status?: string) {
  const suffix = status ? `?status=${encodeURIComponent(status)}` : "";
  return getAdminData<SignupAdminSummary[]>(
    `/internal/admin/signups${suffix}`,
    [],
  );
}

export function getAdminSignup(id: string) {
  return getAdminData<SignupAdminDetail | null>(
    `/internal/admin/signups/${encodeURIComponent(id)}`,
    null,
  );
}

export function getAdminKoxoDashboard() {
  return getAdminData<KoxoAdminDashboard | null>(
    "/internal/admin/koxo",
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

export function getAdminCustomerAdWorkspace(
  customerReference: string,
  subscriptionId?: string | null,
) {
  const query = subscriptionId
    ? `?subscriptionId=${encodeURIComponent(subscriptionId)}`
    : "";

  return getAdminData<AdminCustomerAdWorkspace>(
    `/internal/admin/customers/${encodeURIComponent(customerReference)}/active-directory${query}`,
    {
      customerReference,
      customerName: customerReference,
      adStatus: null,
      links: [],
      linkedUsers: [],
      subscriptionContext: null,
      subscriptions: [],
      managedGroups: [],
      provisioningStatus: "not_required",
      lastResultCode: null,
      services: [],
      groups: [],
      diagnostics: [],
    },
  );
}


// ---------------------------------------------------------------------------
// Administration du catalogue Billing V2/V2.1
//
// Seule autorite commerciale : services, paliers, versions de prix, formules,
// engagements et rattachements provider. Aucun montant n'est calcule ici — le
// webportal affiche ce que la base porte et renvoie une intention de revision,
// jamais un prix resolu.
// ---------------------------------------------------------------------------

export type BillingV2CatalogStatusValue = "active" | "inactive";

export type BillingV2AdminProviderMapping = {
  id: string;
  servicePriceId: string;
  provider: string;
  environment: string;
  externalProductId: string | null;
  externalPriceId: string | null;
  externalPlanId: string | null;
  status: string;
};

export type BillingV2AdminPrice = {
  id: string;
  serviceId: string;
  tierId: string | null;
  priceCode: string;
  priceVersion: number;
  amountCents: number;
  currency: string;
  billingCadence: string;
  chargeTrigger: string;
  taxRateBasisPoints: number | null;
  validFrom: string;
  validUntil: string | null;
  status: string;
  createdByReference: string | null;
  supersedesPriceId: string | null;
  createdAt: string;
  providerMappings: BillingV2AdminProviderMapping[];
};

export type BillingV2AdminTierAttribute = {
  attributeCode: string;
  valueNumeric: number | null;
  valueText: string | null;
  unit: string | null;
};

export type BillingV2AdminTier = {
  id: string;
  serviceId: string;
  code: string;
  name: string;
  publicLabel: string | null;
  description: string | null;
  numericValue: number | null;
  unit: string | null;
  publicSelectable: boolean;
  status: string;
  displayOrder: number;
  attributes: BillingV2AdminTierAttribute[];
  prices: BillingV2AdminPrice[];
};

export type BillingV2AdminService = {
  id: string;
  code: string;
  name: string;
  description: string | null;
  category: string | null;
  billingType: string;
  defaultScopeType: string;
  pricingModel: string;
  mandatoryForSubscription: boolean;
  discountEligible: boolean;
  publicVisible: boolean;
  selfServiceOrderable: boolean;
  status: string;
  displayOrder: number;
  updatedByReference: string | null;
  tiers: BillingV2AdminTier[];
  flatPrices: BillingV2AdminPrice[];
};

export type BillingV2AdminPresetItem = {
  id: string;
  serviceId: string;
  serviceCode: string;
  tierId: string | null;
  tierCode: string | null;
  scopeTemplate: string;
  quantity: number;
  requiredItem: boolean;
  customerEditable: boolean;
  displayOrder: number;
};

export type BillingV2AdminPreset = {
  id: string;
  code: string;
  name: string;
  description: string | null;
  status: string;
  isPublic: boolean;
  displayOrder: number;
  items: BillingV2AdminPresetItem[];
};

export type BillingV2AdminCommitmentPaymentOption = {
  id: string;
  paymentMode: string;
  discountBasisPoints: number;
  status: string;
  displayOrder: number;
};

export type BillingV2AdminCommitment = {
  id: string;
  code: string;
  name: string;
  commitmentMonths: number;
  discountBasisPoints: number | null;
  allowMonthlyPayment: boolean;
  allowUpfrontPayment: boolean;
  status: string;
  displayOrder: number;
  paymentOptions: BillingV2AdminCommitmentPaymentOption[];
};

export type BillingV2AdminCatalogSnapshot = {
  source: string;
  editable: boolean;
  currency: string;
  services: BillingV2AdminService[];
  presets: BillingV2AdminPreset[];
  commitments: BillingV2AdminCommitment[];
};

export type BillingV2AdminCatalogProviderCoverage = {
  provider: string;
  environment: string;
  requiresExternalMapping: boolean;
  currentPriceCount: number;
  mappedPriceCount: number;
  unmappedPriceCodes: string[];
};

export type BillingV2AdminCatalogMutationResponse = {
  code: string;
  message: string;
  id: string | null;
  correlation_id: string;
};

const EMPTY_BILLING_V2_ADMIN_CATALOG: BillingV2AdminCatalogSnapshot = {
  source: "unavailable",
  editable: false,
  currency: "EUR",
  services: [],
  presets: [],
  commitments: [],
};

export function getAdminBillingV2Catalog() {
  return getAdminData<BillingV2AdminCatalogSnapshot>(
    "/internal/admin/billing-v2/catalog",
    EMPTY_BILLING_V2_ADMIN_CATALOG,
  );
}

export function getAdminBillingV2CatalogProviders() {
  return getAdminData<BillingV2AdminCatalogProviderCoverage[]>(
    "/internal/admin/billing-v2/catalog/providers",
    [],
  );
}

export function mutateAdminBillingV2Catalog<TPayload>(
  path: string,
  method: "PATCH" | "POST" | "DELETE",
  payload: TPayload | undefined,
  sessionToken: string,
  correlationId?: CorrelationId,
) {
  return mutateInternalAdminData<
    BillingV2AdminCatalogMutationResponse,
    TPayload
  >(
    `/internal/admin/billing-v2/catalog${path}`,
    method,
    payload,
    sessionToken,
    correlationId,
  );
}

export function getAdminPublicPackCatalogContent() {
  return getAdminData<PublicPackCatalogContent>(
    "/internal/admin/public-pack-catalog",
    createDefaultPublicPackCatalogContent(),
  );
}

export function getAdminManagedContentList() {
  return getAdminData<ManagedContentSummary[]>(
    "/internal/admin/content",
    mockManagedContentSummaries,
  );
}

export function getAdminEditorialList(query = "") {
  const suffix = query ? `?${query}` : "";
  return getAdminData<EditorialListResponse>(
    `/internal/admin/editorial${suffix}`,
    { items: [], categories: [] },
  );
}

export function getAdminDemoProfiles() {
  return getAdminData<DemoProfileSummary[]>(
    "/internal/admin/demo/profiles",
    [],
  );
}

export function getAdminDemoContentTemplates() {
  return getAdminData<DemoContentTemplateSummary[]>(
    "/internal/admin/demo/content-templates",
    [],
  );
}

export function getAdminDemoAccounts() {
  return getAdminData<DemoAccountSummary[]>(
    "/internal/admin/demo/accounts",
    [],
  );
}

export function getAdminManagedContent(key: ManagedContentKey) {
  return getAdminData<ManagedContentDetail | null>(
    `/internal/admin/content/${encodeURIComponent(key)}`,
    getMockManagedContent(key),
  );
}

export function getAdminEditorialContent(id: string) {
  return getAdminData<EditorialContentDetail | null>(
    `/internal/admin/editorial/${encodeURIComponent(id)}`,
    null,
  );
}

export function getAdminEditorialRevisions(id: string) {
  return getAdminData<EditorialRevisionSummary[]>(
    `/internal/admin/editorial/${encodeURIComponent(id)}/revisions`,
    [],
  );
}

export function getAdminEditorialRevision(revisionId: string) {
  return getAdminData<EditorialRevisionDetail | null>(
    `/internal/admin/editorial/revisions/${encodeURIComponent(revisionId)}`,
    null,
  );
}

export function getAdminClientSolutionPortal() {
  return getAdminData<AdminClientSolutionPortal>(
    "/internal/admin/client-solutions",
    createDefaultAdminClientSolutionPortal(),
  );
}

export function getAdminClientSolution(id: string) {
  return getAdminData<ClientSolution | null>(
    `/internal/admin/client-solutions/${encodeURIComponent(id)}`,
    null,
  );
}

export function getAdminDownloadCategories() {
  return getAdminData<DownloadCategory[]>(
    "/internal/admin/download-categories",
    [],
  );
}

export function getAdminDownloads() {
  return getAdminData<DownloadResource[]>(
    "/internal/admin/downloads",
    [],
  );
}

export function getAdminDownload(id: string) {
  return getAdminData<DownloadResource | null>(
    `/internal/admin/downloads/${encodeURIComponent(id)}`,
    null,
  );
}

export function getAdminSubscriptions() {
  return getAdminData<SubscriptionSummary[]>(
    "/internal/admin/subscriptions",
    [],
  );
}

export function getAdminBillingV2Readiness() {
  return getAdminData<BillingV2AdminReadinessSnapshot | null>(
    "/internal/admin/billing-v2/readiness",
    null,
  );
}

export function getAdminBillingV2Subscriptions() {
  return getAdminData<SubscriptionSummary[]>(
    "/internal/admin/billing-v2/subscriptions",
    [],
  );
}

export function getAdminSubscription(id: string) {
  return getAdminData<AdminSubscriptionDetail | null>(
    `/internal/admin/subscriptions/${encodeURIComponent(id)}`,
    null,
  );
}

export function activateAdminCustomerAdService(
  customerReference: string,
  technicalServiceReference: string,
  payload: CustomerAdProvisioningMutationPayload,
  sessionToken: string,
  correlationId = resolveCorrelationId(null),
) {
  return mutateInternalAdminData<
    CustomerAdProvisioningMutationResponse,
    CustomerAdProvisioningMutationPayload
  >(
    `/internal/admin/customers/${encodeURIComponent(customerReference)}/active-directory/services/${encodeURIComponent(technicalServiceReference)}`,
    "POST",
    payload,
    sessionToken,
    correlationId,
  );
}

export function activateAdminCustomerAdGroup(
  customerReference: string,
  groupSamAccountName: string,
  payload: CustomerAdProvisioningMutationPayload,
  sessionToken: string,
  correlationId = resolveCorrelationId(null),
) {
  return mutateInternalAdminData<
    CustomerAdProvisioningMutationResponse,
    CustomerAdProvisioningMutationPayload
  >(
    `/internal/admin/customers/${encodeURIComponent(customerReference)}/active-directory/groups/${encodeURIComponent(groupSamAccountName)}`,
    "POST",
    payload,
    sessionToken,
    correlationId,
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

export type BpceIssuedInvoiceInfo = {
  bpceInvoiceId: string;
  fiscalNumber: string | null;
  status: string;
  issueDate: string;
  totalAmountCents: number;
  currency: string;
  pdfAvailable: boolean;
};

export function getAdminCommercialDocumentInvoice(id: string) {
  return getAdminData<BpceIssuedInvoiceInfo | null>(
    `/internal/admin/commercial-documents/${encodeURIComponent(id)}/invoice`,
    null,
  );
}

export type AdminEmailLogEntry = {
  id: string;
  template: string;
  recipient: string;
  subject: string;
  status: string;
  errorMessage: string | null;
  relatedDocumentId: string | null;
  correlationId: string;
  createdAt: string;
  sentAt: string | null;
};

export function getAdminEmailLog(limit = 100) {
  return getAdminData<AdminEmailLogEntry[]>(
    `/internal/admin/email-log?limit=${encodeURIComponent(String(limit))}`,
    [],
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
