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
  adMode: "disabled" | "mock" | "read_only" | "controlled_write";
  adOperationsEnabled: boolean;
}

export interface AdminActivityOverview {
  supportToHandleCount: number;
  serviceToHandleCount: number;
  recentClientReplyCount: number;
  waitingForCustomerCount: number;
  activeRequestCount: number;
  recentActivities: AdminActivityItem[];
}

export interface AdminActivityItem {
  requestType: RequestType;
  requestId: string;
  reference: string;
  customerReference: string;
  customerName: string;
  subject: string;
  status: SupportRequestStatus | ServiceRequestStatus;
  authorType: "admin" | "client";
  authorLabel: string;
  occurredAt: string;
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

export interface AdminCustomerDetail {
  identity: ClientProfile;
  createdAt: string;
  lastActivityAt: string;
  portalUserCount: number;
  activePortalUserCount: number;
  activeSessionCount: number;
  activeServiceCount: number;
  pendingInvoiceCount: number;
  openSupportRequestCount: number;
  activeServiceRequestCount: number;
  sharedCommercialDocumentCount: number;
  services: ServiceSummary[];
  invoices: InvoiceSummary[];
  supportRequests: AdminSupportRequestSummary[];
  serviceRequests: AdminServiceRequestSummary[];
  commercialDocuments: AdminCommercialDocumentSummary[];
  recentActivity: AdminActivityItem[];
  recentAuditLogs: AdminAuditLogEntry[];
}

export interface AdminSupportRequestSummary {
  id: string;
  reference: string;
  customerReference: string;
  customerName: string;
  serviceName: string;
  priority: string;
  status: SupportRequestStatus;
  subject: string;
  createdAt: string;
  updatedAt: string;
  hasRecentClientReply: boolean;
  requiresAttention: boolean;
}

export interface AdminServiceRequestSummary {
  id: string;
  reference: string;
  customerReference: string;
  customerName: string;
  catalogItemName: string;
  subject: string;
  descriptionPreview: string;
  status: ServiceRequestStatus;
  persisted: boolean;
  createdAt: string;
  updatedAt: string;
  hasRecentClientReply: boolean;
  requiresAttention: boolean;
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
  activeServiceRequestCount: number;
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

export type CommercialOfferStatus = "active" | "inactive";

export type CommercialOfferBillingCadence = "one_time" | "monthly";

export interface CommercialOfferSummary {
  id: string;
  name: string;
  description: string;
  category: string;
  unitLabel: string;
  priceKind: "ht";
  priceAmountCents: number;
  currency: "EUR";
  taxRateBasisPoints: number | null;
  externalReference: string | null;
  status: CommercialOfferStatus;
  displayOrder: number;
  billingCadence: CommercialOfferBillingCadence;
  paypalPlanId: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CommercialOfferPayload {
  name: string;
  description: string;
  category: string;
  unitLabel: string;
  priceAmountCents: number;
  status: CommercialOfferStatus;
  displayOrder: number;
  billingCadence: CommercialOfferBillingCadence;
  paypalPlanId: string | null;
}

export type CommercialDocumentType =
  | "quote_draft"
  | "billing_draft"
  | "informational_invoice";

export type CommercialDocumentStatus =
  | "draft"
  | "pending_review"
  | "shared_with_customer"
  | "cancelled"
  | "issued"
  | "paid";

export interface CommercialDocumentLine {
  id: string;
  offerId: string | null;
  label: string;
  description: string;
  quantity: number;
  unitLabel: string;
  unitPriceCents: number;
  taxRateBasisPoints: number | null;
  lineTotalCents: number;
  sortOrder: number;
  createdAt: string;
  updatedAt: string;
}

export interface CommercialDocumentSummary {
  id: string;
  documentType: CommercialDocumentType;
  status: CommercialDocumentStatus;
  title: string;
  internalReference: string;
  currency: "EUR";
  subtotalAmountCents: number;
  taxAmountCents: number;
  totalAmountCents: number;
  disclaimer: string;
  createdAt: string;
  updatedAt: string;
  sharedAt: string | null;
  serviceRequestId: string | null;
  serviceRequestReference: string | null;
}

export interface CommercialDocumentDetail extends CommercialDocumentSummary {
  lines: CommercialDocumentLine[];
}

export interface AdminCommercialDocumentSummary
  extends CommercialDocumentSummary {
  customerReference: string;
  customerName: string;
}

export interface AdminCommercialDocumentDetail
  extends AdminCommercialDocumentSummary {
  createdByDisplayName: string;
  lines: CommercialDocumentLine[];
}

export interface CommercialDocumentPayload {
  customerReference: string;
  documentType: CommercialDocumentType;
  title: string;
  currency: "EUR";
  serviceRequestId: string | null;
  disclaimer: string;
  status?: Extract<CommercialDocumentStatus, "draft" | "pending_review">;
}

export interface CommercialDocumentLinePayload {
  offerId: string | null;
  label: string;
  description: string;
  quantity: number;
  unitLabel: string;
  unitPriceCents: number;
  taxRateBasisPoints: number | null;
  sortOrder: number;
}

export interface CommercialOfferMutationResponse {
  id: string;
  status: CommercialOfferStatus;
  changed: boolean;
  correlation_id: CorrelationId;
}

export interface CommercialDocumentMutationResponse {
  id: string;
  internalReference: string;
  status: CommercialDocumentStatus;
  changed: boolean;
  correlation_id: CorrelationId;
}

export interface CommercialDocumentLineMutationResponse {
  id: string;
  documentId: string;
  changed: boolean;
  correlation_id: CorrelationId;
}

export interface SupportRequestSummary {
  id: string;
  reference: string;
  subject: string;
  status: SupportRequestStatus;
  priority: "low" | "normal" | "high";
  serviceName: string;
  createdAt: string;
  updatedAt: string;
}

export type SupportRequestStatus =
  | "open"
  | "in_progress"
  | "waiting_for_customer"
  | "resolved"
  | "closed"
  | "cancelled";

export type ServiceRequestStatus =
  | "received"
  | "under_review"
  | "accepted"
  | "rejected"
  | "cancelled"
  | "completed";

export type RequestType = "support" | "service";

export interface ServiceRequestSummary {
  id: string;
  reference: string;
  catalogItemName: string;
  subject: string;
  status: ServiceRequestStatus;
  createdAt: string;
  updatedAt: string;
}

export interface RequestEventSummary {
  eventType:
    | "created"
    | "status_changed"
    | "internal_note_added"
    | "public_message_added";
  oldStatus: string | null;
  newStatus: string | null;
  occurredAt: string;
}

export interface PublicRequestMessage {
  id: string;
  message: string;
  authorLabel: string;
  authorType: "admin" | "client";
  createdAt: string;
}

export type PortalNotificationType =
  | "support_status_changed"
  | "service_status_changed"
  | "support_public_message"
  | "service_public_message";

export interface PortalNotificationSummary {
  id: string;
  notificationType: PortalNotificationType;
  title: string;
  message: string;
  linkUrl: string | null;
  isRead: boolean;
  readAt: string | null;
  createdAt: string;
}

export interface NotificationReadResponse {
  updatedCount: number;
  correlation_id: CorrelationId;
}

export interface InternalRequestNote {
  id: string;
  note: string;
  authorDisplayName: string;
  createdAt: string;
}

export interface PortalSupportRequestDetail extends SupportRequestSummary {
  description: string;
  events: RequestEventSummary[];
  publicMessages: PublicRequestMessage[];
}

export interface PortalServiceRequestDetail extends ServiceRequestSummary {
  description: string;
  events: RequestEventSummary[];
  publicMessages: PublicRequestMessage[];
}

export interface AdminSupportRequestDetail
  extends AdminSupportRequestSummary {
  description: string;
  events: RequestEventSummary[];
  internalNotes: InternalRequestNote[];
  publicMessages: PublicRequestMessage[];
}

export interface AdminServiceRequestDetail {
  id: string;
  reference: string;
  customerReference: string;
  customerName: string;
  catalogItemName: string;
  subject: string;
  description: string;
  status: ServiceRequestStatus;
  persisted: boolean;
  createdAt: string;
  updatedAt: string;
  events: RequestEventSummary[];
  internalNotes: InternalRequestNote[];
  publicMessages: PublicRequestMessage[];
}

export interface RequestStatusPayload {
  status: SupportRequestStatus | ServiceRequestStatus;
}

export interface RequestTextPayload {
  text: string;
}

export interface RequestMutationResponse {
  id: string;
  reference: string;
  status: string;
  changed: boolean;
  correlation_id: CorrelationId;
}

export type AdMode =
  | "disabled"
  | "mock"
  | "read_only"
  | "controlled_write";

export type AdObjectType = "user" | "group";

export interface AdminAdStatus {
  mode: AdMode;
  status:
    | "disabled"
    | "mock"
    | "ready"
    | "configuration_invalid"
    | "unreachable";
  configurationValid: boolean;
  readsEnabled: boolean;
  writesEnabled: boolean;
  domain: string | null;
  clientsOuDn: string | null;
  connectTimeoutMs: number;
  queryTimeoutMs: number;
  maxResults: number;
}

export interface AdDirectoryObjectSummary {
  objectGuid: string;
  objectSid: string;
  objectType: AdObjectType;
  samAccountName: string;
  userPrincipalName: string | null;
  displayName: string;
  distinguishedName: string;
  customerReference: string;
  isDisabled: boolean;
}

export interface CustomerAdLinkSummary {
  id: string;
  customerReference: string;
  objectGuid: string;
  objectSid: string;
  objectType: AdObjectType;
  samAccountName: string;
  userPrincipalName: string | null;
  displayName: string;
  distinguishedName: string;
  linkedAt: string;
  linkedBy: string | null;
}

export interface CustomerAdLinkPayload {
  distinguishedName: string;
}

export interface AdUserCreatePayload {
  samAccountName: string;
  displayName: string;
  givenName: string | null;
  surname: string | null;
  userPrincipalName: string | null;
  description: string | null;
}

export interface AdGroupCreatePayload {
  samAccountName: string;
  displayName: string;
  description: string | null;
}

export interface AdGroupMemberPayload {
  userSamAccountName: string;
}

export interface AdMutationResponse {
  code: string;
  message: string;
  mode: AdMode;
  changed: boolean;
  correlation_id: CorrelationId;
  object: AdDirectoryObjectSummary | null;
  link_id?: string | null;
}

export interface AdLinkMutationResponse {
  id: string;
  code: string;
  message: string;
  changed: boolean;
  correlation_id: CorrelationId;
  object: AdDirectoryObjectSummary | null;
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
