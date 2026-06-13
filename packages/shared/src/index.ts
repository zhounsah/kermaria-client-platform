export type CorrelationId = string & {
  readonly __correlationIdBrand: unique symbol;
};

export interface ApiError {
  code: string;
  message: string;
  correlation_id: CorrelationId;
}

export type UserRole = "client_user" | "internal_admin";

export interface AuthenticatedUser {
  displayName: string;
  email: string;
  customerReference: string | null;
  status: "active" | "disabled" | "pending";
  role: UserRole;
  lastLoginAt: string | null;
}

export type PortalUser = AuthenticatedUser;

export interface LoginPayload {
  email: string;
  password: string;
}

export interface InternalSessionCreated {
  sessionToken: string;
  user: PortalUser;
  expiresAt: string;
}

export interface InternalSession {
  user: PortalUser;
  expiresAt: string;
}

export type AuthState =
  | {
      authenticated: true;
      user: PortalUser;
      expiresAt: string;
    }
  | {
      authenticated: false;
    };

export type AuthMeResponse = AuthState;

export interface AdminAuditLogEntry {
  occurredAt: string;
  actor: string;
  action: string;
  outcome: string;
  reasonCode: string | null;
  customerReference: string | null;
  correlationId: string;
  sourceAddress: string | null;
}

export interface AdminOverview {
  customerCount: number;
  activeUserCount: number;
  activeSessionCount: number;
  openSupportRequestCount: number;
  recentServiceRequestCount: number;
  recentAudits: AdminAuditLogEntry[];
  adMode: "disabled" | "mock" | "test" | "enabled";
  adOperationsEnabled: false;
}

export interface AdminCustomerSummary {
  customerReference: string;
  displayName: string;
  status: string;
  serviceCount: number;
  openSupportRequestCount: number;
  createdAt: string;
  lastActivityAt: string;
}

export interface AdminSupportRequestSummary {
  reference: string;
  customerReference: string;
  customerName: string;
  serviceName: string;
  priority: string;
  status: string;
  subject: string;
  createdAt: string;
  updatedAt: string;
}

export interface AdminServiceRequestSummary {
  reference: string;
  customerReference: string;
  customerName: string;
  catalogItemName: string;
  subject: string;
  descriptionPreview: string;
  status: string;
  persisted: boolean;
  createdAt: string;
}

export interface AdminSessionSummary {
  userDisplayName: string;
  userEmail: string;
  role: UserRole;
  customerReference: string | null;
  createdAt: string;
  expiresAt: string;
  lastSeenAt: string | null;
  sourceAddress: string | null;
  userAgent: string | null;
  status: "active" | "revoked" | "expired";
}

export const SERVICE_NAMES = {
  webportal: "WEBPORTAL",
  apiInternal: "API-INTERNAL",
} as const;

export type DataSource =
  | "api-internal-persistent"
  | "api-internal-mock"
  | "local-fallback"
  | "unavailable";

export interface ClientProfile {
  companyName: string;
  customerReference: string;
  contactName: string;
  email: string;
  phone: string;
  address: string;
  city: string;
  country: string;
  accountStatus: "active" | "pending";
}

export interface PortalSummary {
  customerReference: string;
  contactName: string;
  activeServiceCount: number;
  pendingInvoiceCount: number;
  pendingInvoiceTotal: number;
  openSupportRequestCount: number;
  lastUpdatedAt: string;
}

export interface ServiceSummary {
  id: string;
  reference: string;
  name: string;
  type:
    | "personal_hosting"
    | "backup"
    | "vpn"
    | "rds"
    | "support";
  status: "active" | "pending" | "suspended";
  description: string;
  startedAt: string | null;
  scope: string;
  commercialTerms: "Selon devis" | "Inclus selon périmètre";
  nextStep?: string;
}

export interface InvoiceSummary {
  id: string;
  number: string;
  status: "paid" | "pending" | "overdue";
  issuedAt: string;
  dueAt: string;
  period: string;
  totalAmount: number;
  currency: "EUR";
}

export interface SupportRequestSummary {
  id: string;
  reference: string;
  subject: string;
  status: "open" | "in_progress" | "closed";
  priority: "low" | "normal" | "high";
  serviceName: string;
  createdAt: string;
  updatedAt: string;
}

export interface ServiceCatalogItem {
  id: string;
  name: string;
  category: string;
  description: string;
  scope: string;
  commercialTerms: "Selon devis" | "Inclus selon périmètre";
}

export interface ServiceRequestPayload {
  catalogItemId: string;
  subject: string;
  description: string;
}

export interface SupportRequestPayload {
  serviceId: string;
  priority: "low" | "normal" | "high";
  subject: string;
  description: string;
}

export interface MockSubmissionResponse {
  reference: string;
  status: "mock_received" | "received";
  persisted: boolean;
  message: string;
  correlation_id: CorrelationId;
}

export interface AdHealthStatus {
  mode: "disabled" | "mock" | "test" | "enabled";
  status:
    | "disabled"
    | "mock"
    | "configuration_valid"
    | "configuration_invalid"
    | "validation_required";
  configurationValid: boolean;
  operationsEnabled: false;
}
