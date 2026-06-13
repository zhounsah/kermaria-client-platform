export type CorrelationId = string & {
  readonly __correlationIdBrand: unique symbol;
};

export interface ApiError {
  code: string;
  message: string;
  correlation_id: CorrelationId;
}

export interface PortalUser {
  displayName: string;
  email: string;
  customerReference: string;
  status: "active" | "disabled" | "pending";
}

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
