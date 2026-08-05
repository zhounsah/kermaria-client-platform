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
  customerId: string;
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
  type: string;
  status: "active" | "pending" | "suspended";
  description: string;
  startedAt: string | null;
  scope: string;
  commercialTerms: string;
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

export type CommercialOfferPaymentMode = "monthly" | "upfront";

export type SubscriptionStatus =
  | "pending_approval"
  | "pending_payment"
  | "pending_activation"
  | "pending_cancellation"
  | "active"
  | "suspended"
  | "cancelled"
  | "expired";

export type PaymentRail = "paypal" | "stripe" | "billing";

export interface SubscriptionSummary {
  id: string;
  customerId: string;
  customerReference: string;
  customerName: string;
  commercialOfferId: string;
  offerName: string;
  offerExternalReference: string | null;
  publicPackCode: PublicPackCode | null;
  rail: PaymentRail;
  paypalPlanId: string | null;
  paypalSubscriptionId: string | null;
  stripePriceId: string | null;
  stripeSubscriptionId: string | null;
  status: SubscriptionStatus;
  priceAmountCents: number;
  setupFeeAmountCents: number;
  billingIntervalMonths: number;
  commitmentMonths: number;
  paymentMode: CommercialOfferPaymentMode;
  paidCyclesCount: number;
  commitmentEndsAt: string | null;
  cancelRequestedAt: string | null;
  cancelAtTermEnd: boolean;
  currency: string;
  startedAt: string | null;
  nextBillingAt: string | null;
  cancelledAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface SubscriptionCreatePayload {
  offerId: string;
  rail: PaymentRail;
  paypalSubscriptionId?: string;
  stripeSubscriptionId?: string;
}

export type SubscriptionProvisioningStatus =
  | "not_configured"
  | "not_required"
  | "ready"
  | "succeeded"
  | "failed";

export interface SubscriptionProvisioningTargetUserSummary {
  samAccountName: string;
  displayName: string;
  userPrincipalName: string | null;
}

export interface SubscriptionProvisioningReconcilePayload {
  targetUserSamAccountNames?: string[] | null;
}

export interface SubscriptionProvisioningActionSummary {
  id: string;
  actionType: string;
  status: string;
  resultCode: string | null;
  changed: boolean;
  correlationId: string;
  targetReference: string;
  requestedAt: string;
  startedAt: string | null;
  completedAt: string | null;
}

export interface SubscriptionProvisioningSummary {
  status: SubscriptionProvisioningStatus;
  mappedGroups: string[];
  reconciledGroups: string[];
  targetUsers: SubscriptionProvisioningTargetUserSummary[];
  canRetry: boolean;
  lastResultCode: string | null;
  recentActions: SubscriptionProvisioningActionSummary[];
}

export type ManualProvisioningOperation = "activate" | "remove";

export type ProvisionableServiceStatus =
  | "active"
  | "partial"
  | "inactive"
  | "blocked";

export type AdProvisioningDiagnosticTargetType =
  | "none"
  | "user"
  | "group"
  | "user_and_group";

export interface AdProvisioningDiagnostic {
  code: string;
  message: string;
  targetType: AdProvisioningDiagnosticTargetType;
  allowedRoots: string[];
  affectedUserDistinguishedNames: string[];
  affectedGroupDistinguishedNames: string[];
  linkedUserReferences: string[];
}

export interface AdminCustomerAdSubscriptionContext {
  id: string;
  offerName: string;
  offerExternalReference: string | null;
  publicPackCode: PublicPackCode | null;
  status: SubscriptionStatus;
  mappedGroups: string[];
  coveredServiceTechnicalReferences: string[];
}

export interface ProvisionableServiceSummary {
  technicalServiceReference: string;
  label: string;
  groupSamAccountNames: string[];
  subscriptionIds: string[];
  coveredSubscriptionIds: string[];
  isCoveredByActiveSubscription: boolean;
  isManualEligible: boolean;
  isOverrideRequired: boolean;
  currentStatus: ProvisionableServiceStatus;
  diagnostics: AdProvisioningDiagnostic[];
}

export interface ProvisionableGroupSummary {
  groupSamAccountName: string;
  label: string;
  technicalServiceReferences: string[];
  subscriptionIds: string[];
  coveredSubscriptionIds: string[];
  isCoveredByActiveSubscription: boolean;
  isManualEligible: boolean;
  isOverrideRequired: boolean;
  currentStatus: ProvisionableServiceStatus;
  diagnostics: AdProvisioningDiagnostic[];
}

export interface AdminCustomerAdWorkspace {
  customerReference: string;
  customerName: string;
  adStatus: AdminAdStatus | null;
  links: CustomerAdLinkSummary[];
  linkedUsers: SubscriptionProvisioningTargetUserSummary[];
  subscriptionContext: AdminCustomerAdSubscriptionContext | null;
  subscriptions: AdminCustomerAdSubscriptionContext[];
  managedGroups: string[];
  provisioningStatus: SubscriptionProvisioningStatus | "mixed";
  lastResultCode: string | null;
  services: ProvisionableServiceSummary[];
  groups: ProvisionableGroupSummary[];
  diagnostics: AdProvisioningDiagnostic[];
}

export interface CustomerAdProvisioningMutationPayload {
  operation: ManualProvisioningOperation;
  targetUserSamAccountNames?: string[] | null;
  override?: boolean;
  subscriptionId?: string | null;
}

export interface CustomerAdProvisioningMutationResponse {
  code: string;
  message: string;
  changed: boolean;
  correlation_id: CorrelationId;
  workspace: AdminCustomerAdWorkspace;
}

export interface AdminSubscriptionDetail {
  subscription: SubscriptionSummary;
  documents: CommercialDocumentSummary[];
  provisioning: SubscriptionProvisioningSummary;
}

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
  technicalServiceReferences: string[];
  provisioningGroupSamAccountNames: string[];
  status: CommercialOfferStatus;
  displayOrder: number;
  billingCadence: CommercialOfferBillingCadence;
  setupFeeAmountCents: number | null;
  billingIntervalMonths: number | null;
  commitmentMonths: number | null;
  paymentMode: CommercialOfferPaymentMode | null;
  publicPackCode: PublicPackCode | null;
  paypalPlanIdSandbox: string | null;
  paypalPlanIdLive: string | null;
  stripePriceIdTest: string | null;
  stripePriceIdLive: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CommercialOfferPayload {
  name: string;
  description: string;
  category: string;
  unitLabel: string;
  priceAmountCents: number;
  externalReference: string | null;
  technicalServiceReferences: string[];
  provisioningGroupSamAccountNames: string[];
  status: CommercialOfferStatus;
  displayOrder: number;
  billingCadence: CommercialOfferBillingCadence;
  setupFeeAmountCents: number | null;
  billingIntervalMonths: number | null;
  commitmentMonths: number | null;
  paymentMode: CommercialOfferPaymentMode | null;
  publicPackCode: PublicPackCode | null;
  paypalPlanIdSandbox: string | null;
  paypalPlanIdLive: string | null;
  stripePriceIdTest: string | null;
  stripePriceIdLive: string | null;
}

export type PublicPackCode =
  | "pack-dossier-securise"
  | "pack-acces-distance"
  | "pack-bureau-windows-distance"
  | "pack-pro-association";

export type PublicPackCommitmentMonths = 1 | 6 | 12;

export type ManagedContentType = "legal" | "pack_sheet" | "page";

export type ManagedContentKey =
  | "legal:cgv"
  | "legal:politique-confidentialite"
  | "legal:mentions-legales"
  | "page:a-propos"
  | `pack-sheet:${PublicPackCode}`;

export interface ManagedContentSummary {
  key: ManagedContentKey;
  contentType: ManagedContentType;
  title: string;
  publicPath: string;
  versionLabel: string | null;
  updatedAt: string | null;
}

export interface ManagedContentDetail extends ManagedContentSummary {
  bodyMarkdown: string;
  createdAt: string | null;
}

export interface ManagedContentPayload {
  bodyMarkdown: string;
  versionLabel: string | null;
}

export interface ManagedContentMutationResponse {
  key: ManagedContentKey;
  changed: boolean;
  updatedAt: string;
  correlation_id: CorrelationId;
}

export type DownloadStatus = "active" | "inactive";

export type DownloadResourceType =
  | "software"
  | "script"
  | "rdp"
  | "document"
  | "tool"
  | "other";

export type DownloadSourceKind = "internal_file" | "external_url";

export type DownloadVisibilityMode = "all_clients" | "targeted";

export type DownloadVisibilityTargetType =
  | "public_pack_code"
  | "offer_external_reference"
  | "service_type"
  | "provisioning_group";

export type DownloadServiceType = ServiceSummary["type"];

export interface DownloadCategory {
  id: string;
  slug: string;
  title: string;
  description: string | null;
  status: DownloadStatus;
  displayOrder: number;
  resourceCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface DownloadVisibilityRule {
  id: string;
  resourceId: string;
  targetType: DownloadVisibilityTargetType;
  targetValue: string;
}

export interface DownloadResource {
  id: string;
  categoryId: string;
  categoryTitle: string;
  title: string;
  shortDescription: string;
  resourceType: DownloadResourceType;
  sourceKind: DownloadSourceKind;
  visibilityMode: DownloadVisibilityMode;
  status: DownloadStatus;
  externalUrl: string | null;
  versionLabel: string | null;
  installationInstructions: string | null;
  displayOrder: number;
  hasInternalFile: boolean;
  fileOriginalName: string | null;
  fileContentType: string | null;
  fileSizeBytes: number | null;
  fileExtension: string | null;
  createdAt: string;
  updatedAt: string;
  rules: DownloadVisibilityRule[];
}

export interface PortalDownloadItem {
  id: string;
  title: string;
  shortDescription: string;
  resourceType: DownloadResourceType;
  versionLabel: string | null;
  updatedAt: string | null;
  installationInstructions: string | null;
}

export interface PortalDownloadCategory {
  id: string;
  slug: string;
  title: string;
  description: string | null;
  items: PortalDownloadItem[];
}

export interface DownloadCategoryPayload {
  slug: string;
  title: string;
  description: string | null;
  status: DownloadStatus;
  displayOrder: number;
}

export interface DownloadVisibilityRulePayload {
  targetType: DownloadVisibilityTargetType;
  targetValue: string;
}

export interface DownloadResourcePayload {
  categoryId: string;
  title: string;
  shortDescription: string;
  resourceType: DownloadResourceType;
  sourceKind: DownloadSourceKind;
  visibilityMode: DownloadVisibilityMode;
  status: DownloadStatus;
  externalUrl: string | null;
  versionLabel: string | null;
  installationInstructions: string | null;
  displayOrder: number;
  visibilityRules: DownloadVisibilityRulePayload[];
}

export interface DownloadCategoryMutationResponse {
  id: string;
  changed: boolean;
  updatedAt: string;
  correlation_id: CorrelationId;
}

export interface DownloadResourceMutationResponse {
  id: string;
  changed: boolean;
  updatedAt: string;
  correlation_id: CorrelationId;
}

export type ClientSolutionStatus = "published" | "draft";

export interface ClientSolutionPortalSettings {
  eyebrow: string | null;
  title: string;
  description: string | null;
  footerNote: string | null;
  updatedAt: string | null;
}

export interface PublicClientSolution {
  id: string;
  slug: string;
  title: string;
  tagline: string | null;
  targetUrl: string;
  opensInNewTab: boolean;
  hasLogo: boolean;
  logoUpdatedAt: string | null;
  displayOrder: number;
}

export interface PublicClientSolutionPortal {
  settings: ClientSolutionPortalSettings;
  solutions: PublicClientSolution[];
}

export interface ClientSolution {
  id: string;
  slug: string;
  title: string;
  tagline: string | null;
  targetUrl: string;
  opensInNewTab: boolean;
  status: ClientSolutionStatus;
  displayOrder: number;
  hasLogo: boolean;
  logoOriginalName: string | null;
  logoContentType: string | null;
  logoSizeBytes: number | null;
  logoUpdatedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface AdminClientSolutionPortal {
  settings: ClientSolutionPortalSettings;
  solutions: ClientSolution[];
}

export interface ClientSolutionPayload {
  slug: string | null;
  title: string;
  tagline: string | null;
  targetUrl: string;
  opensInNewTab: boolean;
  status: ClientSolutionStatus;
  displayOrder: number;
}

export interface ClientSolutionPortalSettingsPayload {
  eyebrow: string | null;
  title: string;
  description: string | null;
  footerNote: string | null;
}

export interface ClientSolutionMutationResponse {
  id: string;
  changed: boolean;
  updatedAt: string;
  correlation_id: CorrelationId;
}

export interface ClientSolutionPortalMutationResponse {
  changed: boolean;
  updatedAt: string;
  correlation_id: CorrelationId;
}

export interface ManagedContentRegistryEntry {
  key: ManagedContentKey;
  contentType: ManagedContentType;
  title: string;
  publicPath: string;
  sortOrder: number;
  packCode: PublicPackCode | null;
}

export interface SignupPackSelectionSnapshot {
  packKey: PublicPackCode;
  packLabel: string;
  offerId: string;
  offerExternalReference: string;
  commitmentMonths: PublicPackCommitmentMonths;
  paymentMode: CommercialOfferPaymentMode;
  billingIntervalMonths: number;
  discountPercent: number;
  monthlyPriceAmountCents: number;
  billingPriceAmountCents: number;
  setupFeeAmountCents: number;
  firstChargeAmountCents: number;
  currency: "EUR";
}

export interface PendingPackSelectionSummary {
  signupId: string;
  status: string;
  approvedAt: string | null;
  createdAt: string;
  snapshot: SignupPackSelectionSnapshot;
}

export interface PublicPackVariantManifest {
  commitmentMonths: PublicPackCommitmentMonths;
  paymentMode: CommercialOfferPaymentMode;
  externalReference: string;
}

export interface PublicPackManifest {
  key: PublicPackCode;
  slug: string;
  label: string;
  shortLabel: string;
  headline: string;
  audience: string;
  description: string;
  highlights: readonly string[];
  included: readonly string[];
  technicalServiceReferences: readonly string[];
  provisioningGroupSamAccountNames: readonly string[];
  order: number;
  variants: readonly PublicPackVariantManifest[];
}

export type PublicPackComparisonValueKind = "included" | "excluded" | "text";

export interface PublicPackComparisonValue {
  kind: PublicPackComparisonValueKind;
  text: string | null;
}

export interface PublicPackPresentation {
  packCode: PublicPackCode;
  label: string;
  shortLabel: string;
  headline: string;
  audience: string;
  description: string;
  highlights: readonly string[];
  included: readonly string[];
  highlightLabel: string | null;
  displayOrder: number;
}

export interface PublicPackComparisonRow {
  id: string;
  label: string;
  sortOrder: number;
  values: Record<PublicPackCode, PublicPackComparisonValue>;
}

export interface PublicPackCatalogContent {
  pageEyebrow: string;
  pageTitle: string;
  pageDescription: string;
  comparisonColumnLabel: string;
  footnotePrimary: string;
  footnoteSecondary: string;
  packs: readonly PublicPackPresentation[];
  comparisonRows: readonly PublicPackComparisonRow[];
  updatedAt: string | null;
}

export interface PublicPackCatalogContentPayload {
  pageEyebrow: string;
  pageTitle: string;
  pageDescription: string;
  comparisonColumnLabel: string;
  footnotePrimary: string;
  footnoteSecondary: string;
  packs: readonly PublicPackPresentation[];
  comparisonRows: readonly PublicPackComparisonRow[];
}

export interface PublicPackCatalogMutationResponse {
  changed: boolean;
  updatedAt: string;
  correlation_id: CorrelationId;
}

export const CLIENT_SOLUTION_STATUSES = [
  "published",
  "draft",
] as const satisfies readonly ClientSolutionStatus[];

export const CLIENT_SOLUTION_LOGO_CONTENT_TYPES = [
  "image/png",
  "image/jpeg",
  "image/webp",
  "image/svg+xml",
] as const;

export const CLIENT_SOLUTION_LOGO_MAX_SIZE_BYTES = 512 * 1024;

export const DOWNLOAD_RESOURCE_TYPES = [
  "software",
  "script",
  "rdp",
  "document",
  "tool",
  "other",
] as const satisfies readonly DownloadResourceType[];

export const DOWNLOAD_SOURCE_KINDS = [
  "internal_file",
  "external_url",
] as const satisfies readonly DownloadSourceKind[];

export const DOWNLOAD_VISIBILITY_MODES = [
  "all_clients",
  "targeted",
] as const satisfies readonly DownloadVisibilityMode[];

export const DOWNLOAD_VISIBILITY_TARGET_TYPES = [
  "public_pack_code",
  "offer_external_reference",
  "service_type",
  "provisioning_group",
] as const satisfies readonly DownloadVisibilityTargetType[];

export const DOWNLOAD_SERVICE_TYPES = [
  "personal_hosting",
  "storage",
  "backup",
  "vpn",
  "rds",
  "support",
  "cloud",
  "documentation",
  "monitoring",
  "user",
  "other",
] as const satisfies readonly DownloadServiceType[];

export interface ResolvedPublicPackVariant {
  offer: CommercialOfferSummary;
  externalReference: string;
  commitmentMonths: PublicPackCommitmentMonths;
  paymentMode: CommercialOfferPaymentMode;
  billingIntervalMonths: number;
  discountPercent: number;
  monthlyPriceAmountCents: number;
  billingPriceAmountCents: number;
  setupFeeAmountCents: number;
  firstChargeAmountCents: number;
  currency: "EUR";
}

export interface ResolvedPublicPackManifest extends PublicPackManifest {
  variantsByCommitment: Record<
    PublicPackCommitmentMonths,
    {
      monthly: ResolvedPublicPackVariant;
      upfront: ResolvedPublicPackVariant | null;
    }
  >;
}

export const PUBLIC_BACKUP_POLICY_DETAILS_PATH = "/cgv";

export interface PublicPackBackupPolicySummary {
  included: boolean;
  summary: string;
  detailsHref: string;
  detailsLabel: string;
}

const PUBLIC_PACK_VARIANTS_BY_TERM: ReadonlyArray<PublicPackVariantManifest> = [
  {
    commitmentMonths: 1,
    paymentMode: "monthly",
    externalReference: "PACK-DOSSIER-1M-MENS",
  },
  {
    commitmentMonths: 6,
    paymentMode: "monthly",
    externalReference: "PACK-DOSSIER-6M-MENS",
  },
  {
    commitmentMonths: 6,
    paymentMode: "upfront",
    externalReference: "PACK-DOSSIER-6M-COMPT",
  },
  {
    commitmentMonths: 12,
    paymentMode: "monthly",
    externalReference: "PACK-DOSSIER-12M-MENS",
  },
  {
    commitmentMonths: 12,
    paymentMode: "upfront",
    externalReference: "PACK-DOSSIER-12M-COMPT",
  },
] as const;

function withVariantPrefix(
  prefix: string,
): ReadonlyArray<PublicPackVariantManifest> {
  return PUBLIC_PACK_VARIANTS_BY_TERM.map((variant) => ({
    ...variant,
    externalReference: variant.externalReference.replace("PACK-DOSSIER", prefix),
  }));
}

export const PUBLIC_PACKS: ReadonlyArray<PublicPackManifest> = [
  {
    key: "pack-dossier-securise",
    slug: "dossier-securise",
    label: "Pack Dossier Sécurisé",
    shortLabel: "Dossier Sécurisé",
    headline: "Une copie distante pour garder vos documents importants à portée de main.",
    audience:
      "Pour un particulier, un indépendant ou une petite structure qui veut un dossier de secours numérique simple.",
    description:
      "Un espace documentaire distant pour conserver vos fichiers importants avec sauvegarde planifiée, sans jargon technique inutile.",
    highlights: [
      "Dossier de secours numérique 32 Go",
      "Accès à distance aux documents",
      "Sauvegarde régulière",
      "Support de base",
    ],
    included: [
      "32 Go de stockage personnel",
      "Copie distante de vos documents importants",
      "Sauvegardes planifiées",
      "Aide de base en cas de besoin",
    ],
    technicalServiceReferences: ["STOCK-PERSO-32", "SAVE-PERSO"],
    provisioningGroupSamAccountNames: [],
    order: 10,
    variants: withVariantPrefix("PACK-DOSSIER"),
  },
  {
    key: "pack-acces-distance",
    slug: "acces-distance",
    label: "Pack Accès à Distance",
    shortLabel: "Accès à Distance",
    headline: "Retrouvez vos fichiers et votre accès privé même loin de vos équipements.",
    audience:
      "Pour une personne qui veut un dossier distant avec un accès plus encadré et un meilleur confort de reprise.",
    description:
      "La base du dossier de secours numérique, enrichie d'un accès VPN personnel et d'une supervision légère.",
    highlights: [
      "Tout le pack Dossier Sécurisé",
      "Accès VPN personnel",
      "Supervision du service",
      "Support niveau 1",
    ],
    included: [
      "Stockage personnel, sauvegarde et accès distant",
      "VPN personnel pour se connecter",
      "Supervision du service",
      "Support niveau 1",
    ],
    technicalServiceReferences: [
      "STOCK-PERSO-32",
      "SAVE-PERSO",
      "ACCES-VPN",
      "SUPERV-SERVICE",
      "SUPPORT-LV1",
    ],
    provisioningGroupSamAccountNames: ["GG_VPN"],
    order: 20,
    variants: withVariantPrefix("PACK-ACCES"),
  },
  {
    key: "pack-bureau-windows-distance",
    slug: "bureau-windows-distance",
    label: "Pack Bureau Windows à Distance",
    shortLabel: "Bureau Windows",
    headline: "Un environnement Windows distant pour continuer à travailler plus facilement.",
    audience:
      "Pour retrouver un bureau Windows complet depuis l'extérieur et limiter les ruptures d'usage.",
    description:
      "Un bureau Windows à distance avec accès VPN, stockage, sauvegarde et suivi du service pour reprendre plus sereinement.",
    highlights: [
      "Bureau Windows à distance",
      "Accès VPN personnel",
      "Stockage 32 Go et sauvegarde",
      "Supervision et support niveau 1",
    ],
    included: [
      "Accès à un bureau Windows distant",
      "VPN personnel inclus",
      "32 Go de stockage et sauvegardes",
      "Supervision et support niveau 1",
    ],
    technicalServiceReferences: [
      "ACCES-RDS",
      "ACCES-VPN",
      "STOCK-PERSO-32",
      "SAVE-PERSO",
      "SUPERV-SERVICE",
      "SUPPORT-LV1",
    ],
    provisioningGroupSamAccountNames: ["GG_VPN", "GG_RDS"],
    order: 30,
    variants: withVariantPrefix("PACK-BUREAU"),
  },
  {
    key: "pack-pro-association",
    slug: "pro-association",
    label: "Pack Pro / Association",
    shortLabel: "Pro / Association",
    headline: "Une base plus complète pour sécuriser la continuité d'une petite structure.",
    audience:
      "Pour une petite équipe qui veut une offre plus large, avec sauvegarde, accès et cadre documentaire simplifié.",
    description:
      "Une formule plus complète pour une petite structure ou une association, avec plus de capacité et des repères utiles à la reprise.",
    highlights: [
      "2 utilisateurs et 64 Go de stockage",
      "Accès VPN personnel",
      "Sauvegarde et supervision",
      "Support niveau 1 et documentation utile à la reprise",
    ],
    included: [
      "Base de stockage, capacité additionnelle et repères documentaires",
      "VPN personnel",
      "Sauvegarde et supervision",
      "Support niveau 1 et documentation",
    ],
    technicalServiceReferences: [
      "USER-ADD",
      "STOCK-PERSO-32",
      "STOCK-SUP-32",
      "ACCES-VPN",
      "SAVE-PERSO",
      "SUPERV-SERVICE",
      "SUPPORT-LV1",
      "DOC-TECH",
    ],
    provisioningGroupSamAccountNames: ["GG_VPN"],
    order: 40,
    variants: withVariantPrefix("PACK-PRO"),
  },
] as const;

export function getPublicPackDiscountPercent(
  commitmentMonths: PublicPackCommitmentMonths,
): number {
  switch (commitmentMonths) {
    case 6:
      return 10;
    case 12:
      return 20;
    default:
      return 0;
  }
}

export function getPublicPackManifest(
  packKey: PublicPackCode,
): PublicPackManifest | null {
  return PUBLIC_PACKS.find((pack) => pack.key === packKey) ?? null;
}

export function getPublicPackManifestBySlug(
  slug: string,
): PublicPackManifest | null {
  return PUBLIC_PACKS.find((pack) => pack.slug === slug) ?? null;
}

export function buildPackSheetContentKey(
  packCode: PublicPackCode,
): ManagedContentKey {
  return `pack-sheet:${packCode}`;
}

export function buildPackSheetPublicPath(packCode: PublicPackCode): string {
  const pack = getPublicPackManifest(packCode);
  return pack ? `/offres/${pack.slug}` : "/offres";
}

export function isManagedContentKey(value: unknown): value is ManagedContentKey {
  return typeof value === "string"
    && (value === "legal:cgv"
      || value === "legal:politique-confidentialite"
      || value === "legal:mentions-legales"
      || value === "page:a-propos"
      || PUBLIC_PACKS.some(
        (pack) => value === buildPackSheetContentKey(pack.key),
      ));
}

export function getManagedContentRegistry(): readonly ManagedContentRegistryEntry[] {
  return [
    {
      key: "legal:cgv",
      contentType: "legal",
      title: "Conditions générales de vente",
      publicPath: "/cgv",
      sortOrder: 10,
      packCode: null,
    },
    {
      key: "legal:politique-confidentialite",
      contentType: "legal",
      title: "Politique de confidentialité",
      publicPath: "/politique-confidentialite",
      sortOrder: 15,
      packCode: null,
    },
    {
      key: "legal:mentions-legales",
      contentType: "legal",
      title: "Mentions légales",
      publicPath: "/mentions-legales",
      sortOrder: 20,
      packCode: null,
    },
    {
      key: "page:a-propos",
      contentType: "page",
      title: "À propos de Zachary IT",
      publicPath: "/a-propos",
      sortOrder: 30,
      packCode: null,
    },
    ...PUBLIC_PACKS.map((pack) => ({
      key: buildPackSheetContentKey(pack.key),
      contentType: "pack_sheet" as const,
      title: `Fiche technique - ${pack.label}`,
      publicPath: `/offres/${pack.slug}`,
      sortOrder: 100 + pack.order,
      packCode: pack.key,
    })),
  ];
}

export function getManagedContentEntry(
  key: ManagedContentKey,
): ManagedContentRegistryEntry | null {
  return getManagedContentRegistry().find((entry) => entry.key === key) ?? null;
}

export function resolvePublicPackVariantFromCatalog(
  catalog: readonly CommercialOfferSummary[],
  packKey: PublicPackCode,
  commitmentMonths: PublicPackCommitmentMonths,
  paymentMode: CommercialOfferPaymentMode,
): ResolvedPublicPackVariant | null {
  const pack = getPublicPackManifest(packKey);
  if (!pack) {
    return null;
  }

  const variant = pack.variants.find(
    (candidate) =>
      candidate.commitmentMonths === commitmentMonths
      && candidate.paymentMode === paymentMode,
  );
  if (!variant) {
    return null;
  }

  const offer = catalog.find(
    (candidate) => candidate.externalReference === variant.externalReference,
  );
  if (!offer) {
    return null;
  }

  const billingIntervalMonths =
    offer.billingIntervalMonths
    ?? (paymentMode === "upfront" ? commitmentMonths : 1);
  const setupFeeAmountCents = offer.setupFeeAmountCents ?? 0;
  const billingPriceAmountCents = offer.priceAmountCents;
  const monthlyPriceAmountCents =
    billingIntervalMonths > 1
      ? Math.round(billingPriceAmountCents / commitmentMonths)
      : billingPriceAmountCents;

  return {
    offer,
    externalReference: variant.externalReference,
    commitmentMonths,
    paymentMode,
    billingIntervalMonths,
    discountPercent: getPublicPackDiscountPercent(commitmentMonths),
    monthlyPriceAmountCents,
    billingPriceAmountCents,
    setupFeeAmountCents,
    firstChargeAmountCents: billingPriceAmountCents + setupFeeAmountCents,
    currency: "EUR",
  };
}

export function resolvePublicPackCatalog(
  catalog: readonly CommercialOfferSummary[],
): ResolvedPublicPackManifest[] {
  return PUBLIC_PACKS.flatMap((pack) => {
    const monthly1 = resolvePublicPackVariantFromCatalog(
      catalog,
      pack.key,
      1,
      "monthly",
    );
    const monthly6 = resolvePublicPackVariantFromCatalog(
      catalog,
      pack.key,
      6,
      "monthly",
    );
    const monthly12 = resolvePublicPackVariantFromCatalog(
      catalog,
      pack.key,
      12,
      "monthly",
    );

    // If the billable catalog is not fully seeded yet, hide the incomplete
    // public pack instead of crashing the whole vitrine/signup flow.
    if (!monthly1 || !monthly6 || !monthly12) {
      return [];
    }

    return [{
      ...pack,
      variantsByCommitment: {
        1: {
          monthly: monthly1,
          upfront: null,
        },
        6: {
          monthly: monthly6,
          upfront:
            resolvePublicPackVariantFromCatalog(
              catalog,
              pack.key,
              6,
              "upfront",
            ),
        },
        12: {
          monthly: monthly12,
          upfront:
            resolvePublicPackVariantFromCatalog(
              catalog,
              pack.key,
              12,
              "upfront",
            ),
        },
      },
    }];
  }).sort((left, right) => left.order - right.order);
}

export function createSignupPackSelectionSnapshot(
  variant: ResolvedPublicPackVariant,
): SignupPackSelectionSnapshot {
  const packKey = variant.offer.publicPackCode ?? inferPublicPackCode(variant);
  const manifest = getPublicPackManifest(packKey);

  return {
    packKey,
    packLabel: manifest?.label ?? variant.offer.name,
    offerId: variant.offer.id,
    offerExternalReference: variant.externalReference,
    commitmentMonths: variant.commitmentMonths,
    paymentMode: variant.paymentMode,
    billingIntervalMonths: variant.billingIntervalMonths,
    discountPercent: variant.discountPercent,
    monthlyPriceAmountCents: variant.monthlyPriceAmountCents,
    billingPriceAmountCents: variant.billingPriceAmountCents,
    setupFeeAmountCents: variant.setupFeeAmountCents,
    firstChargeAmountCents: variant.firstChargeAmountCents,
    currency: variant.currency,
  };
}

function inferPublicPackCode(
  variant: ResolvedPublicPackVariant,
): PublicPackCode {
  return (
    variant.offer.publicPackCode
    ?? PUBLIC_PACKS.find((pack) =>
      pack.variants.some(
        (candidate) => candidate.externalReference === variant.externalReference,
      ),
    )?.key
    ?? "pack-dossier-securise"
  );
}

function createComparisonValue(
  kind: PublicPackComparisonValueKind,
  text: string | null = null,
): PublicPackComparisonValue {
  return { kind, text };
}

export const DEFAULT_PUBLIC_PACK_CATALOG_CONTENT: PublicPackCatalogContentPayload = {
  pageEyebrow: "Catalogue packs",
  pageTitle: "Des packs simples pour sauvegarder, stocker et reprendre plus vite",
  pageDescription:
    "Comparez les packs destinés à protéger vos documents importants, vos sauvegardes et la continuité d'une petite activité sans devoir arbitrer des briques techniques internes.",
  comparisonColumnLabel: "Repères utiles",
  footnotePrimary:
    "Les tarifs affichés sont hors taxes et correspondent au catalogue public actuel. Le détail technique reste géré en interne pour le provisionnement, le support et l'explication du cadre retenu.",
  footnoteSecondary:
    "Besoin d'un dossier de secours numérique ou d'un accompagnement spécifique ? Passez par le formulaire de contact.",
  packs: PUBLIC_PACKS.map((pack) => ({
    packCode: pack.key,
    label: pack.label,
    shortLabel: pack.shortLabel,
    headline: pack.headline,
    audience: pack.audience,
    description: pack.description,
    highlights: [...pack.highlights],
    included: [...pack.included],
    highlightLabel: null,
    displayOrder: pack.order,
  })),
  comparisonRows: [
    {
      id: "storage",
      label: "Espace documentaire distant inclus",
      sortOrder: 10,
      values: {
        "pack-dossier-securise": createComparisonValue("text", "32 Go"),
        "pack-acces-distance": createComparisonValue("text", "32 Go"),
        "pack-bureau-windows-distance": createComparisonValue("text", "32 Go"),
        "pack-pro-association": createComparisonValue("text", "64 Go"),
      },
    },
    {
      id: "remote-files",
      label: "Accès distant aux documents",
      sortOrder: 20,
      values: {
        "pack-dossier-securise": createComparisonValue("included"),
        "pack-acces-distance": createComparisonValue("included"),
        "pack-bureau-windows-distance": createComparisonValue("included"),
        "pack-pro-association": createComparisonValue("included"),
      },
    },
    {
      id: "vpn",
      label: "Accès VPN personnel",
      sortOrder: 30,
      values: {
        "pack-dossier-securise": createComparisonValue("excluded"),
        "pack-acces-distance": createComparisonValue("included"),
        "pack-bureau-windows-distance": createComparisonValue("included"),
        "pack-pro-association": createComparisonValue("included"),
      },
    },
    {
      id: "backup",
      label: "Sauvegarde planifiée",
      sortOrder: 40,
      values: {
        "pack-dossier-securise": createComparisonValue("included"),
        "pack-acces-distance": createComparisonValue("included"),
        "pack-bureau-windows-distance": createComparisonValue("included"),
        "pack-pro-association": createComparisonValue("included"),
      },
    },
    {
      id: "supervision",
      label: "Supervision du service",
      sortOrder: 50,
      values: {
        "pack-dossier-securise": createComparisonValue("excluded"),
        "pack-acces-distance": createComparisonValue("included"),
        "pack-bureau-windows-distance": createComparisonValue("included"),
        "pack-pro-association": createComparisonValue("included"),
      },
    },
    {
      id: "windows-desktop",
      label: "Bureau Windows à distance",
      sortOrder: 60,
      values: {
        "pack-dossier-securise": createComparisonValue("excluded"),
        "pack-acces-distance": createComparisonValue("excluded"),
        "pack-bureau-windows-distance": createComparisonValue("included"),
        "pack-pro-association": createComparisonValue("excluded"),
      },
    },
    {
      id: "support",
      label: "Support inclus",
      sortOrder: 70,
      values: {
        "pack-dossier-securise": createComparisonValue("text", "Base"),
        "pack-acces-distance": createComparisonValue("text", "Niveau 1"),
        "pack-bureau-windows-distance": createComparisonValue("text", "Niveau 1"),
        "pack-pro-association": createComparisonValue("text", "Niveau 1"),
      },
    },
    {
      id: "users",
      label: "Utilisateurs inclus",
      sortOrder: 80,
      values: {
        "pack-dossier-securise": createComparisonValue("text", "1"),
        "pack-acces-distance": createComparisonValue("text", "1"),
        "pack-bureau-windows-distance": createComparisonValue("text", "1"),
        "pack-pro-association": createComparisonValue("text", "2"),
      },
    },
    {
      id: "documentation",
      label: "Documentation simplifiée",
      sortOrder: 90,
      values: {
        "pack-dossier-securise": createComparisonValue("excluded"),
        "pack-acces-distance": createComparisonValue("excluded"),
        "pack-bureau-windows-distance": createComparisonValue("excluded"),
        "pack-pro-association": createComparisonValue("included"),
      },
    },
  ],
};

export function createDefaultPublicPackCatalogContentPayload(): PublicPackCatalogContentPayload {
  return JSON.parse(
    JSON.stringify(DEFAULT_PUBLIC_PACK_CATALOG_CONTENT),
  ) as PublicPackCatalogContentPayload;
}

export function createDefaultPublicPackCatalogContent(): PublicPackCatalogContent {
  return {
    ...createDefaultPublicPackCatalogContentPayload(),
    updatedAt: null,
  };
}

export function createDefaultClientSolutionPortalSettings(): ClientSolutionPortalSettings {
  return {
    eyebrow: "Portail de services",
    title: "Accéder à mes solutions",
    description:
      "Retrouvez ici les accès directs aux services mis à votre disposition. "
      + "Cliquez sur une tuile pour ouvrir le service correspondant.",
    footerNote: null,
    updatedAt: null,
  };
}

export function createDefaultClientSolutionPortal(): PublicClientSolutionPortal {
  return {
    settings: createDefaultClientSolutionPortalSettings(),
    solutions: [],
  };
}

export function createDefaultAdminClientSolutionPortal(): AdminClientSolutionPortal {
  return {
    settings: createDefaultClientSolutionPortalSettings(),
    solutions: [],
  };
}

export function packIncludesBackup(
  pack: Pick<PublicPackManifest, "technicalServiceReferences">,
): boolean {
  return pack.technicalServiceReferences.includes("SAVE-PERSO");
}

export function getPublicPackBackupPolicySummary(
  pack: Pick<PublicPackManifest, "technicalServiceReferences">,
): PublicPackBackupPolicySummary {
  if (packIncludesBackup(pack)) {
    return {
      included: true,
      summary:
        "Les données couvertes par le service de sauvegarde font l'objet d'une sauvegarde automatique quotidienne. Les versions sauvegardées sont conservées pendant 31 jours glissants. Les données créées ou modifiées depuis la dernière sauvegarde réussie peuvent ne pas être récupérables.",
      detailsHref: PUBLIC_BACKUP_POLICY_DETAILS_PATH,
      detailsLabel: "Voir les conditions détaillées",
    };
  }

  return {
    included: false,
    summary:
      "Sauvegarde disponible en option. Sans option active, la récupération des données après suppression, altération ou défaillance n'est pas garantie.",
    detailsHref: PUBLIC_BACKUP_POLICY_DETAILS_PATH,
    detailsLabel: "Voir les conditions détaillées",
  };
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
  paymentMethod: PaymentRail | "manual" | null;
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
  allowedRoots: string[];
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

export interface AdUserRenamePayload {
  newSamAccountName: string;
  newDisplayName: string;
  newUserPrincipalName: string | null;
}

export type AdUserMoveContainer = "Users" | "Disabled";

export interface AdUserMovePayload {
  targetCustomerReference: string;
  targetContainer: AdUserMoveContainer;
}

export interface PortalPasswordChangePayload {
  currentPassword: string;
  newPassword: string;
}

/** Champs du profil que le client peut corriger lui-meme depuis le portail.
 *  L'organisation, la reference client, l'e-mail (identifiant de connexion)
 *  et le statut restent geres cote back-office. */
export interface PortalProfileUpdatePayload {
  contactName: string;
  phone: string;
  address: string;
  city: string;
  country: string;
}

export interface PortalProfileUpdateResponse {
  code: string;
  message: string;
  profile: ClientProfile;
  correlation_id: CorrelationId;
}

export interface PortalPasswordChangeResponse {
  code: string;
  message: string;
  mode: AdMode;
  correlation_id: CorrelationId;
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

// V0.35 — Panier / commande groupee a la carte.
// Le client compose lui-meme un panier d'options a la carte (offres
// one-shot uniquement) ; la confirmation materialise un unique document
// commercial multi-lignes regle via les rails existants (Stripe / PayPal /
// virement).

export interface CartItem {
  offerId: string;
  name: string;
  description: string;
  category: string;
  unitLabel: string;
  unitPriceCents: number;
  taxRateBasisPoints: number | null;
  quantity: number;
  lineTotalCents: number;
}

export interface CartSummary {
  items: CartItem[];
  itemCount: number;
  subtotalCents: number;
  currency: "EUR";
}

export interface CartAddPayload {
  offerId: string;
  quantity?: number;
}

export interface CartMutationResponse {
  cart: CartSummary;
  correlation_id: CorrelationId;
}

export interface CartConfirmResponse {
  documentId: string;
  itemCount: number;
  totalAmountCents: number;
  correlation_id: CorrelationId;
}

export interface RecurringCheckoutItem {
  offerId: string;
  name: string;
  description: string;
  category: string;
  unitLabel: string;
  publicPackCode: PublicPackCode | null;
  priceAmountCents: number;
  setupFeeAmountCents: number;
  firstChargeAmountCents: number;
  billingIntervalMonths: number;
  commitmentMonths: number;
  paymentMode: CommercialOfferPaymentMode;
  currency: "EUR";
}

export interface CheckoutBucket<TItem> {
  items: TItem[];
  itemCount: number;
  subtotalCents: number;
  currency: "EUR";
}

export interface CheckoutSummary {
  cart: CartSummary;
  recurring: CheckoutBucket<RecurringCheckoutItem>;
  totalItemCount: number;
  hasMixedCheckout: boolean;
}

export interface CheckoutRecurringAddPayload {
  offerId: string;
}

export interface CheckoutRecurringMutationResponse {
  recurring: CheckoutBucket<RecurringCheckoutItem>;
  correlation_id: CorrelationId;
}

export interface CheckoutRecurringConfirmResponse {
  documentId: string;
  itemCount: number;
  totalAmountCents: number;
  subscriptionIds: string[];
  correlation_id: CorrelationId;
}

export type DemoKind = "showcase" | "trial";

export interface DemoCapabilities {
  emailMode: string;
  bpceMode: string;
  paymentMode: string;
  adProvisioningMode: string;
  adGroups: string[];
  storageQuotaGo: number | null;
  rdsSessionMode: string;
}

export interface DemoProfileSummary {
  key: string;
  label: string;
  kind: DemoKind;
  contentTemplateKey: string | null;
  lifetimeDays: number;
  status: string;
  capabilities: DemoCapabilities;
}

export interface DemoContentTemplateSummary {
  key: string;
  label: string;
  serviceNames: string[];
}

export interface DemoAccountSummary {
  customerReference: string;
  displayName: string;
  kind: string;
  profileKey: string | null;
  serviceCount: number;
  createdAt: string;
  expiresAt: string | null;
  /**
   * Renseigné quand le balayage d'expiration a révoqué l'accès réel de cet essai
   * (retrait des groupes GG_DEMO_* + désactivation AD) avant purge (V1.1 Lot 3).
   */
  revokedAt: string | null;
}

/** Résultat d'un balayage du cycle de vie des comptes de démo (V1.1 Lot 3). */
export interface DemoLifecycleSweepResult {
  revokedCount: number;
  purgedCount: number;
  skippedReferences: string[];
  revokeFailures: string[];
  /**
   * Essais dont l'accès réel a été appliqué lors de cette passe, l'identité AD
   * n'ayant pas encore existé au moment de la création (chaîne KoXo).
   */
  reprovisionedCount: number;
}

/** Conversion d'un compte d'essai en client réel (V1.1 Lot 4). */
export interface DemoConversionRequest {
  /**
   * Offre réelle dont les groupes AD remplacent les `GG_DEMO_*`. Facultative :
   * sans elle, la conversion se contente de retirer l'accès de démonstration.
   */
  offerExternalReference?: string | null;
}

export interface DemoConversionResult {
  converted: boolean;
  /** Vrai si le compte avait déjà été converti : l'opération est idempotente. */
  alreadyConverted: boolean;
  resultCode: string;
  customerReference: string;
  demoGroupsRemoved: string[];
  realGroupsGranted: string[];
  identityMoved: boolean;
}

export interface DemoAccountCreateRequest {
  profileKey: string;
  displayName: string;
  email: string;
  initialPassword: string;
  userDisplayName?: string | null;
  lifetimeDaysOverride?: number | null;
  selectedServiceNames?: string[] | null;
  /** `madame` | `monsieur` — seules valeurs acceptees par l'export KoXo. */
  personalTitle?: string | null;
  givenName?: string | null;
  surname?: string | null;
  /** Format `yyyy-MM-dd`. */
  birthDate?: string | null;
}

export interface DemoProfilePayload {
  key: string;
  label: string;
  kind: DemoKind;
  contentTemplateKey?: string | null;
  emailMode?: string | null;
  bpceMode?: string | null;
  paymentMode?: string | null;
  adProvisioningMode?: string | null;
  adGroups?: string[] | null;
  storageQuotaGo?: number | null;
  rdsSessionMode?: string | null;
  lifetimeDays?: number | null;
  status?: string | null;
}

export interface DemoAccountCreatedResponse {
  customerReference: string;
  email: string;
  kind: string;
  expiresAt: string | null;
}
