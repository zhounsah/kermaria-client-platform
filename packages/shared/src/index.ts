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

export type BackupProtectionStatus =
  | "protected"
  | "warning"
  | "critical"
  | "unknown";

export type BackupRunResult =
  | "success"
  | "warning"
  | "failed"
  | "running"
  | "unknown";

export interface BackupRunSummary {
  id: string;
  startedAt: string;
  finishedAt: string | null;
  result: BackupRunResult;
  resultLabel: string;
  protectedBytes: number | null;
  durationSeconds: number | null;
  publicMessage: string | null;
}

export interface BackupJobSummary {
  id: string;
  serviceId: string;
  serviceName: string;
  provider: "veeam";
  status: string;
  protectionStatus: BackupProtectionStatus;
  protectionStatusLabel: string;
  lastRunAt: string | null;
  lastSuccessAt: string | null;
  lastResult: BackupRunResult | null;
  lastResultLabel: string | null;
  protectedBytes: number | null;
  durationSeconds: number | null;
  retentionDays: number | null;
  nextRunAt: string | null;
  lastErrorPublic: string | null;
  collectedAt: string | null;
  lastVerifiedAt: string | null;
  verificationStatus: string | null;
}

export interface BackupJobDetail {
  job: BackupJobSummary;
  runs: BackupRunSummary[];
}

export interface BackupRestoreRequestPayload {
  itemPath: string;
  desiredRestoreAt?: string;
  description: string;
  priority: "low" | "normal" | "high";
}

export interface BackupIntegrationSummary {
  id: string;
  provider: "veeam";
  externalJobId: string;
  customerId: string;
  customerReference: string;
  customerName: string;
  serviceId: string;
  serviceName: string;
  enabled: boolean;
  expectedIntervalMinutes: number;
  criticalAfterMinutes: number;
  staleAfterMinutes: number;
  lastCollectedAt: string | null;
  lastCollectionStatus: string | null;
  lastCollectionMessage: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface BackupIntegrationPayload {
  id?: string;
  provider: "veeam";
  externalJobId: string;
  customerId: string;
  serviceId: string;
  enabled: boolean;
  expectedIntervalMinutes: number;
  criticalAfterMinutes: number;
  staleAfterMinutes: number;
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

/**
 * Statut d'une entite de catalogue Billing V2 : service, palier, formule,
 * engagement, rattachement provider.
 */
export type BillingV2CatalogStatus = "active" | "inactive";

/** Cadence d'une composante tarifaire Billing V2. */
export type BillingV2BillingCadence = "one_time" | "monthly";

/**
 * Declencheur d'une composante tarifaire. Une meme combinaison
 * service/palier peut porter un tarif a la souscription initiale et un autre
 * lors d'un changement de configuration.
 */
export type BillingV2ChargeTrigger =
  | "initial_subscription"
  | "subscription_change";

export type BillingV2PaymentMode = "monthly" | "upfront";

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
  /**
   * Formule Billing V2 d'origine. `null` pour une souscription directe : elle
   * n'est rattachee a aucune formule, et forger un identifiant pour combler ce
   * vide reintroduirait une fausse offre.
   */
  presetId: string | null;
  label: string;
  presetCode: string | null;
  rail: PaymentRail;
  paypalPlanId: string | null;
  paypalSubscriptionId: string | null;
  stripePriceId: string | null;
  stripeSubscriptionId: string | null;
  status: SubscriptionStatus;
  priceAmountCents: number;
  setupFeeAmountCents: number;
  taxRateBasisPoints: number | null;
  fiscalRegime: FiscalRegime;
  fiscalMention: string;
  billingIntervalMonths: number;
  commitmentMonths: number;
  paymentMode: BillingV2PaymentMode;
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
  /**
   * Conserve pour la lisibilite des journaux et des ecrans d'exploitation. Il
   * n'existe plus qu'un seul systeme de facturation.
   */
  billingSystem?: "billing_v2";
  /** Places USER-ADDITIONAL vendues sur cette souscription, et places pourvues. */
  additionalUserSlotsCount?: number;
  assignedAdditionalUsersCount?: number;
}

/**
 * Etats d'une place utilisateur supplementaire, tels que l'espace client les
 * voit. Volontairement plus grossiers que le cycle de vie interne : le client
 * n'a pas a connaitre l'etat de l'annuaire.
 */
export type BillingV2AdditionalUserSlotStatus =
  | "available"
  | "invited"
  | "activating"
  | "active"
  | "attention"
  | "disabled";

/**
 * Place utilisateur supplementaire presentee a l'espace client.
 *
 * Ne porte aucune donnee technique : ni identifiant KoXo, ni objectGUID, ni
 * code d'echec, ni identifiant d'utilisateur portail.
 */
export interface BillingV2AdditionalUserSlotSummary {
  id: string;
  displayName: string | null;
  email: string | null;
  status: BillingV2AdditionalUserSlotStatus;
  canAssign: boolean;
  canResendInvitation: boolean;
}

/**
 * Personne a installer sur une place.
 *
 * Ni client, ni acteur, ni identifiant d'utilisateur portail : ces valeurs
 * sont resolues par le serveur a partir de la session. Les accepter du
 * navigateur permettrait d'equiper la place d'une autre organisation.
 */
export interface BillingV2AdditionalUserAssignPayload {
  email: string;
  displayName: string;
  personalTitle?: string | null;
  givenName?: string | null;
  surname?: string | null;
  /** Jour civil ISO `yyyy-MM-dd`. Une date de naissance n'a pas de fuseau. */
  birthDate?: string | null;
  initials?: string | null;
  phone?: string | null;
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
  /** Libelle commercial de la souscription, tel que la projection V2 le rend. */
  label: string;
  /** Formule d'origine. `null` pour une souscription directe, sans formule. */
  presetCode: string | null;
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

export type FiscalRegime = "franchise_base" | "standard";

export type PublicPackCode =
  | "pack-dossier-securise"
  | "pack-acces-distance"
  | "pack-bureau-windows-distance"
  | "pack-pro-association";

export type PublicPackCommitmentMonths = 1 | 6 | 12;

export type ManagedContentType = "diagnostic_config" | "legal" | "pack_sheet" | "page" | "storefront_page";

export type StorefrontContentKey =
  | "storefront:services"
  | "storefront:tarifs"
  | "storefront:cloud-hebergement"
  | "storefront:domaines-messagerie"
  | "storefront:reseau-securite"
  | "storefront:support-it"
  | "storefront:vps"
  | "storefront:infogerance-vps"
  | "storefront:hebergement-web"
  | "storefront:maintenance-linux"
  | "storefront:maintenance-wordpress"
  | "storefront:sauvegarde-externalisee"
  | "storefront:supervision-informatique"
  | "storefront:supervision-nas"
  | "storefront:vpn-entreprise"
  | "storefront:bureau-windows-distance"
  | "storefront:unifi"
  | "storefront:firewall"
  | "storefront:cloudflare-waf"
  | "storefront:gestion-dns-domaines"
  | "storefront:messagerie-professionnelle";

export type ManagedContentKey =
  | "legal:cgv"
  | "legal:politique-confidentialite"
  | "legal:mentions-legales"
  | "page:a-propos"
  | "page:infrastructure"
  | "diagnostic:recommendations"
  | StorefrontContentKey
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

export type EditorialContentType = "wiki_article" | "seo_page" | "faq";

export type EditorialContentStatus =
  | "draft"
  | "published"
  | "archived"
  | "scheduled";

export interface EditorialCategory {
  id: string;
  contentType: EditorialContentType;
  name: string;
  slug: string;
  description: string | null;
  sortOrder: number;
  createdAt: string;
  updatedAt: string;
}

export interface EditorialContentSummary {
  id: string;
  contentType: EditorialContentType;
  title: string;
  slug: string;
  summary: string | null;
  categoryId: string | null;
  categoryName: string | null;
  categorySortOrder: number | null;
  status: EditorialContentStatus;
  sortOrder: number;
  noIndex: boolean;
  faqScopes: string[];
  publishedAt: string | null;
  updatedAt: string;
  publicPath: string | null;
}

export interface EditorialContentDetail extends EditorialContentSummary {
  bodyMarkdown: string;
  seoTitle: string | null;
  seoDescription: string | null;
  canonicalUrl: string | null;
  createdAt: string;
  createdByUserId: string | null;
  updatedByUserId: string | null;
}

export interface EditorialContentPayload {
  contentType: EditorialContentType;
  title: string;
  slug: string;
  summary: string | null;
  bodyMarkdown: string;
  categoryId: string | null;
  status: EditorialContentStatus;
  seoTitle: string | null;
  seoDescription: string | null;
  canonicalUrl: string | null;
  noIndex: boolean;
  sortOrder: number;
  faqScopes: string[];
}

export interface EditorialCategoryPayload {
  contentType: EditorialContentType;
  name: string;
  slug: string;
  description: string | null;
  sortOrder: number;
}

export interface EditorialRevisionSummary {
  id: string;
  contentId: string;
  versionNumber: number;
  action: string;
  createdAt: string;
  createdByUserId: string | null;
}

export interface EditorialRevisionDetail extends EditorialRevisionSummary {
  snapshot: EditorialContentDetail;
}

export interface EditorialRedirect {
  id: string;
  contentType: EditorialContentType;
  oldPath: string;
  newPath: string;
  createdAt: string;
}

export interface EditorialListResponse {
  items: EditorialContentSummary[];
  categories: EditorialCategory[];
}

export interface EditorialMutationResponse {
  id: string;
  changed: boolean;
  updatedAt: string;
  correlation_id: CorrelationId;
}

export interface EditorialMarkdownImportResult {
  title: string | null;
  slug: string | null;
  description: string | null;
  bodyMarkdown: string;
  warnings: string[];
}

export interface PublicWikiHome {
  categories: EditorialCategory[];
  recentArticles: EditorialContentSummary[];
}

export interface PublicFaqScope {
  scope: string;
  items: EditorialContentDetail[];
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
  // `billing_v2_offer_presets.code`
  | "preset_code"
  // `billing_v2_services.code`
  | "service_code"
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

export interface PendingBillingV2SelectionSummary {
  signupId: string;
  status: string;
  approvedAt: string | null;
  createdAt: string;
  selection: BillingV2PublicSelection;
}


export const DIAGNOSTIC_RECOMMENDATION_PROFILE_IDS = [
  "simple_backup",
  "vpn_access",
  "windows_desktop",
  "team_or_structure",
  "team_windows_desktop",
] as const;

export type DiagnosticRecommendationProfileId =
  typeof DIAGNOSTIC_RECOMMENDATION_PROFILE_IDS[number];

export interface DiagnosticRecommendationRuleConfig {
  profileId: DiagnosticRecommendationProfileId;
  presetCode: string | null;
}

export interface DiagnosticRecommendationConfig {
  schemaVersion: 1;
  rules: readonly DiagnosticRecommendationRuleConfig[];
}

export type DiagnosticCustomerType =
  | "individual"
  | "business"
  | "association"
  | "other";

export type DiagnosticRecoveryImportance = "low" | "normal" | "high";

export type DiagnosticDataKind =
  | "personal_documents"
  | "business_documents"
  | "photos"
  | "association_data"
  | "work_files"
  | "other_important_files";

export type DiagnosticBackupFrequency =
  | "daily"
  | "weekly"
  | "monthly"
  | "rarely"
  | "unknown";

export type DiagnosticRestoreTestRecency =
  | "less_than_3_months"
  | "less_than_12_months"
  | "more_than_12_months"
  | "never"
  | "unknown";

export type DiagnosticContinuityPlan = "yes" | "partial" | "no" | "unknown";

export interface DiagnosticAnswers {
  customerType: DiagnosticCustomerType;
  users: number | null;
  dataKinds: readonly DiagnosticDataKind[];
  estimatedStorageGb: number | "above_public_max" | null;
  needsRemoteFiles: boolean | null;
  needsVpn: boolean | null;
  needsWindowsDesktop: boolean | null;
  recoveryImportance: DiagnosticRecoveryImportance;
  backupFrequency: DiagnosticBackupFrequency;
  restoreTestRecency: DiagnosticRestoreTestRecency;
  continuityPlan: DiagnosticContinuityPlan;
}

export type DiagnosticRecommendationReasonCode =
  | "simple_backup"
  | "needs_remote_files"
  | "needs_vpn"
  | "needs_windows_desktop"
  | "team_or_structure"
  | "association_context"
  | "storage_within_pack"
  | "strong_recovery_need";

export type DiagnosticRecommendationWarningCode =
  | "storage_unknown"
  | "backup_frequency_unknown"
  | "storage_requires_quote"
  | "users_require_quote"
  | "other_structure_requires_review"
  | "no_recent_restore_test"
  | "no_continuity_plan";

export type DiagnosticRecommendationStatus =
  | "standard"
  | "requires_quote";

export interface DiagnosticRecommendation {
  status: DiagnosticRecommendationStatus;
  reasons: readonly DiagnosticRecommendationReasonCode[];
  warnings: readonly DiagnosticRecommendationWarningCode[];
  suggestedOptions: readonly string[];
  selection: BillingV2PublicSelection | null;
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
  "preset_code",
  "service_code",
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

export const PUBLIC_BACKUP_POLICY_DETAILS_PATH = "/cgv";

export interface PublicPackBackupPolicySummary {
  included: boolean;
  summary: string;
  detailsHref: string;
  detailsLabel: string;
}

export type PublicPackAudienceScope =
  | "individual"
  | "business"
  | "association";

export interface PublicPackCapabilities {
  includedUsers: number;
  includedStorageGb: number;
  supportsRemoteFiles: boolean;
  supportsVpn: boolean;
  supportsWindowsDesktop: boolean;
  supportsBackup: boolean;
  audienceScopes: readonly PublicPackAudienceScope[];
}

/**
 * Fiche editoriale d'une formule publique.
 *
 * Ce manifeste ne porte **aucun** prix, aucune reference d'offre et aucune
 * variante d'engagement. Le tarif, les paliers et les engagements viennent
 * exclusivement du catalogue Billing V2, seule autorite commerciale. Ce qui
 * reste ici est ce que Billing V2 ne sait pas dire : un slug d'URL, un titre
 * de vitrine, une accroche et un argumentaire.
 *
 * `key` est aussi le code de la formule V2 (`billing_v2_offer_presets.code`) :
 * la fiche `/offres/{slug}` et le configurateur `/formules/{key}` decrivent
 * donc le meme objet.
 */
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
  capabilities: PublicPackCapabilities;
  order: number;
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
      "Un espace documentaire distant pour conserver vos fichiers importants avec sauvegarde quotidienne, sans jargon technique inutile.",
    highlights: [
      "Dossier de secours numérique 32 Go",
      "Accès à distance aux documents",
      "Sauvegarde régulière",
      "Support de base",
    ],
    included: [
      "32 Go de stockage personnel",
      "Copie distante de vos documents importants",
      "Sauvegardes quotidiennes",
      "Aide de base en cas de besoin",
    ],
    technicalServiceReferences: ["STORAGE-PERSONAL", "BACKUP-PERSONAL"],
    provisioningGroupSamAccountNames: [],
    capabilities: {
      includedUsers: 1,
      includedStorageGb: 32,
      supportsRemoteFiles: true,
      supportsVpn: false,
      supportsWindowsDesktop: false,
      supportsBackup: true,
      audienceScopes: ["individual", "business"],
    },
    order: 10,
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
      "STORAGE-PERSONAL",
      "BACKUP-PERSONAL",
      "VPN-ACCESS",
      "MONITORING-INTERNAL",
      "SUPPORT-STANDARD",
    ],
    provisioningGroupSamAccountNames: ["GG_VPN"],
    capabilities: {
      includedUsers: 1,
      includedStorageGb: 32,
      supportsRemoteFiles: true,
      supportsVpn: true,
      supportsWindowsDesktop: false,
      supportsBackup: true,
      audienceScopes: ["individual", "business"],
    },
    order: 20,
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
      "RDS-ACCESS",
      "VPN-ACCESS",
      "STORAGE-PERSONAL",
      "BACKUP-PERSONAL",
      "MONITORING-INTERNAL",
      "SUPPORT-STANDARD",
    ],
    provisioningGroupSamAccountNames: ["GG_VPN", "GG_RDS"],
    capabilities: {
      includedUsers: 1,
      includedStorageGb: 32,
      supportsRemoteFiles: true,
      supportsVpn: true,
      supportsWindowsDesktop: true,
      supportsBackup: true,
      audienceScopes: ["individual", "business"],
    },
    order: 30,
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
      "USER-ADDITIONAL",
      "STORAGE-PERSONAL",
      "STORAGE-SHARED",
      "VPN-ACCESS",
      "BACKUP-PERSONAL",
      "MONITORING-INTERNAL",
      "SUPPORT-STANDARD",
    ],
    provisioningGroupSamAccountNames: ["GG_VPN"],
    capabilities: {
      includedUsers: 2,
      includedStorageGb: 64,
      supportsRemoteFiles: true,
      supportsVpn: true,
      supportsWindowsDesktop: false,
      supportsBackup: true,
      audienceScopes: ["business", "association"],
    },
    order: 40,
  },
] as const;

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
      || value === "page:infrastructure"
      || value === "diagnostic:recommendations"
      || value.startsWith("storefront:")
        && STOREFRONT_CONTENT_REGISTRY.some((entry) => entry.key === value)
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
    {
      key: "page:infrastructure",
      contentType: "page",
      title: "Infrastructure et exploitation des services Zachary IT",
      publicPath: "/infrastructure",
      sortOrder: 35,
      packCode: null,
    },
    {
      key: "diagnostic:recommendations",
      contentType: "diagnostic_config",
      title: "Diagnostic - Règles de recommandation",
      publicPath: "/diagnostic",
      sortOrder: 37,
      packCode: null,
    },
    ...STOREFRONT_CONTENT_REGISTRY,
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

const STOREFRONT_CONTENT_REGISTRY: readonly ManagedContentRegistryEntry[] = [
  ["storefront:services", "Pages principales", "Catalogue des services", "/services", 40],
  ["storefront:tarifs", "Pages principales", "Tarifs Zachary IT", "/tarifs", 45],
  ["storefront:cloud-hebergement", "Catégories services", "Cloud & Hébergement", "/services/cloud-hebergement", 50],
  ["storefront:domaines-messagerie", "Catégories services", "Domaines & Messagerie", "/services/domaines-messagerie", 51],
  ["storefront:reseau-securite", "Catégories services", "Réseau & Sécurité", "/services/reseau-securite", 52],
  ["storefront:support-it", "Catégories services", "Support & IT", "/services/support-it", 53],
  ["storefront:vps", "Pages services SEO", "VPS", "/services/vps", 60],
  ["storefront:infogerance-vps", "Pages services SEO", "Infogérance VPS", "/services/infogerance-vps", 61],
  ["storefront:hebergement-web", "Pages services SEO", "Hébergement web", "/services/hebergement-web", 62],
  ["storefront:maintenance-linux", "Pages services SEO", "Maintenance Linux", "/services/maintenance-linux", 63],
  ["storefront:maintenance-wordpress", "Pages services SEO", "Maintenance WordPress", "/services/maintenance-wordpress", 64],
  ["storefront:sauvegarde-externalisee", "Pages services SEO", "Sauvegarde externalisée", "/services/sauvegarde-externalisee", 65],
  ["storefront:supervision-informatique", "Pages services SEO", "Supervision informatique", "/services/supervision-informatique", 66],
  ["storefront:supervision-nas", "Pages services SEO", "Supervision NAS", "/services/supervision-nas", 67],
  ["storefront:vpn-entreprise", "Pages services SEO", "VPN entreprise", "/services/vpn-entreprise", 68],
  ["storefront:bureau-windows-distance", "Pages services SEO", "Bureau Windows à distance", "/services/bureau-windows-distance", 69],
  ["storefront:unifi", "Pages services SEO", "UniFi", "/services/unifi", 70],
  ["storefront:firewall", "Pages services SEO", "Firewall", "/services/firewall", 71],
  ["storefront:cloudflare-waf", "Pages services SEO", "Cloudflare WAF", "/services/cloudflare-waf", 72],
  ["storefront:gestion-dns-domaines", "Pages services SEO", "Gestion DNS et domaines", "/services/gestion-dns-domaines", 73],
  ["storefront:messagerie-professionnelle", "Pages services SEO", "Messagerie professionnelle", "/services/messagerie-professionnelle", 74],
].map(([key, group, title, publicPath, sortOrder]) => ({
  key: key as StorefrontContentKey,
  contentType: "storefront_page" as const,
  // Le groupe fait partie du titre admin afin que la liste reste lisible sans
  // introduire un second registre ou une arborescence modifiable.
  title: `${group} — ${title}`,
  publicPath: publicPath as string,
  sortOrder: sortOrder as number,
  packCode: null,
}));

export function getManagedContentEntry(
  key: ManagedContentKey,
): ManagedContentRegistryEntry | null {
  return getManagedContentRegistry().find((entry) => entry.key === key) ?? null;
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
      label: "Sauvegarde quotidienne",
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
  return pack.technicalServiceReferences.includes("BACKUP-PERSONAL");
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
  /**
   * Une ligne est auto-portante : libelle, quantite, prix unitaire et taux
   * lui appartiennent. Elle ne pointe aucun catalogue, sinon reediter une
   * piece apres une revision tarifaire en changerait le montant affiche.
   */
  label: string;
  description: string;
  quantity: number;
  unitLabel: string;
  unitPriceCents: number;
  taxRateBasisPoints: number | null;
  fiscalRegime: FiscalRegime;
  fiscalMention: string;
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
  label: string;
  description: string;
  quantity: number;
  unitLabel: string;
  unitPriceCents: number;
  taxRateBasisPoints: number | null;
  sortOrder: number;
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
   * Codes `billing_v2_services.code` dont les groupes AD remplacent les
   * `GG_DEMO_*`. La topologie « quel service pilote quels groupes » est lue
   * dans `billing_v2_provisioning_rules` côté API-INTERNAL : le portail ne
   * nomme que des services, jamais des groupes. Facultatif : sans codes, la
   * conversion se contente de retirer l'accès de démonstration.
   */
  serviceCodes?: string[] | null;
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

/**
 * Conception commerciale Billing V2 — projection publique.
 *
 * Aucun de ces contrats ne transporte un montant DEPUIS le navigateur : la
 * selection ne porte que des codes catalogue, et le devis est le resultat
 * renvoye par le serveur. Le front affiche, il ne calcule pas.
 */
/**
 * Composante tarifaire applicable a un service ou a un palier.
 *
 * Un meme couple (service, palier) peut en porter plusieurs simultanement :
 * un abonnement mensuel ET des frais de mise en service ponctuels, par
 * exemple. C'est cette liste qui decide des lignes facturables, jamais une
 * pretendue « cadence du service ».
 */
export interface BillingV2PublicPriceComponent {
  billingCadence: "monthly" | "one_time";
  chargeTrigger: "initial_subscription" | "subscription_change";
  amountCents: number;
  currency: string;
  discountEligible: boolean;
  servicePriceId: string | null;
  priceCode: string | null;
}

export interface BillingV2PublicTier {
  code: string;
  label: string;
  description: string | null;
  numericValue: number | null;
  /** Somme des composantes mensuelles. Affichage seulement. */
  monthlyAmountCents: number;
  publicSelectable: boolean;
  priceComponents: BillingV2PublicPriceComponent[] | null;
}

export interface BillingV2PublicService {
  code: string;
  name: string;
  category: string;
  scopeType: string;
  /** Somme des composantes mensuelles sans palier. Affichage seulement. */
  flatMonthlyAmountCents: number | null;
  tiers: BillingV2PublicTier[];
  discountEligible: boolean;
  publicVisible: boolean;
  selfServiceOrderable: boolean;
  /** Metadonnee commerciale : sans autorite sur les lignes tarifaires. */
  billingType: string;
  flatPriceComponents: BillingV2PublicPriceComponent[] | null;
}

export interface BillingV2PublicPresetItem {
  serviceCode: string;
  tierCode: string | null;
  scopeTemplate: string;
  quantity: number;
  amountCents: number;
  customerEditable: boolean;
}

export interface BillingV2PublicPreset {
  code: string;
  name: string;
  description: string;
  displayOrder: number;
  items: BillingV2PublicPresetItem[];
  /** Total mensuel de la configuration recommandee, calcule cote serveur. */
  baselineMonthlyAmountCents: number;
}

/** `monthly` = paye au mois ; `upfront` = paye en une fois. */
export type BillingV2PublicPaymentMode = "monthly" | "upfront";

export interface BillingV2PublicPaymentOption {
  paymentMode: BillingV2PublicPaymentMode;
  discountBasisPoints: number;
}

export interface BillingV2PublicCommitment {
  code: string;
  name: string;
  months: number;
  /**
   * La remise depend du couple (duree, mode de reglement) : six mois payes au
   * mois et six mois payes comptant sont deux options distinctes.
   */
  paymentOptions: BillingV2PublicPaymentOption[];
}

export interface BillingV2PublicCatalog {
  source: string;
  currency: string;
  presets: BillingV2PublicPreset[];
  services: BillingV2PublicService[];
  commitments: BillingV2PublicCommitment[];
}

/**
 * Deux formes, aucune n'etant un cas particulier de l'autre :
 * formule (`presetCode`) ou composants choisis directement (`components`).
 * `commitmentCode` est nul quand le produit n'engage a rien — typiquement un
 * achat ponctuel.
 */
export interface BillingV2PublicSelection {
  presetCode: string | null;
  commitmentCode: string | null;
  paymentMode: BillingV2PublicPaymentMode;
  storagePersonalTierCode: string;
  backupPersonal: boolean;
  storageSharedTierCode: string | null;
  backupShared: boolean;
  vpnTierCode: string | null;
  remoteDesktop: boolean;
  additionalUsers: number;
  supportPlus: boolean;
  /** V2.1 : intention generique sans montant, tier/provider/scope client. */
  components?: BillingV2PublicSelectionComponent[];
}

export interface BillingV2PublicSelectionComponent {
  serviceCode: string;
  tierCode: string | null;
  quantity: number;
}

export interface BillingV2PublicQuoteLine {
  serviceCode: string;
  tierCode: string | null;
  label: string;
  detail: string | null;
  quantity: number;
  unitAmountCents: number;
  amountCents: number;
  discountEligible: boolean;
  billingCadence: "monthly" | "one_time";
}

export interface BillingV2PublicQuote {
  presetCode: string | null;
  commitmentCode: string | null;
  commitmentMonths: number;
  paymentMode: BillingV2PublicPaymentMode;
  discountBasisPoints: number;
  currency: string;
  monthlyBeforeDiscountCents: number;
  monthlyDiscountCents: number;
  /** En comptant, equivalent mensuel derive du total serveur. */
  monthlyAfterDiscountCents: number;
  oneTimeCents: number;
  /** Montant reellement preleve a la souscription. */
  totalDueNowCents: number;
  commitmentTotalBeforeDiscountCents: number;
  commitmentTotalAfterDiscountCents: number;
  commitmentSavingsCents: number;
  lines: BillingV2PublicQuoteLine[];
  matchesPresetBaseline: boolean;
  checkoutAvailable: boolean;
  /** `native` : souscription V2, avec ou sans formule d'origine. */
  checkoutMode: string;
  checkoutReasonCode: string;
}

export type ApplicationSettingValue = string | number | boolean;

export interface ApplicationSettingItem {
  key: string;
  category: string;
  label: string;
  description: string;
  valueType: "bool" | "int" | "string" | "email" | "url" | "enum" | "json";
  value: ApplicationSettingValue;
  classification: "dynamic" | "restart_required" | "secret" | "code_invariant";
  risk: "low" | "medium" | "high" | "critical";
  editable: boolean;
  restartRequired: boolean;
  sensitive: boolean;
  source: "default" | "database" | "env" | "json";
  version: number;
  updatedAt: string | null;
}

export interface ApplicationSettingsSnapshot {
  settings: ApplicationSettingItem[];
  persistent: boolean;
}

export interface ApplicationSettingUpdatePayload {
  value: ApplicationSettingValue;
  expectedVersion: number;
}

export interface ApplicationSettingMutationResponse {
  code: string;
  message: string;
  setting: ApplicationSettingItem | null;
  correlationId: string;
}

export interface ConfigurationStatusFact { label: string; value: string; sensitive: boolean; }
export interface ConfigurationStatusDomain { key: string; label: string; state: "healthy" | "warning" | "info"; facts: ConfigurationStatusFact[]; warning: string | null; }
export interface ConfigurationStatusSnapshot { domains: ConfigurationStatusDomain[]; }
export interface PortalBillingConfiguration { iban: string | null; bic: string | null; paypalUrl: string | null; transferLabel: string; }

// --- Messages et communications (Centre de configuration, section 8) --------
// Contrats non sensibles : aucune adresse serveur, aucun secret. Les gabarits
// sont administrables mais leurs cles et variables restent fermees cote code.

export interface CommunicationTemplateVariable {
  name: string;
  description: string;
}

export interface CommunicationTemplateRevisionItem {
  key: string;
  version: number;
  outcome: string;
  actorUserId: string | null;
  correlationId: string;
  createdAt: string;
}

export interface EmailTemplateItem {
  key: string;
  displayName: string;
  description: string;
  subject: string;
  body: string;
  enabled: boolean;
  /** `code` : gabarit integre au code ; `database` : gabarit administre. */
  source: "code" | "database";
  customized: boolean;
  version: number;
  updatedAt: string | null;
  defaultSubject: string;
  defaultBody: string;
  testSendSupported: boolean;
  variables: CommunicationTemplateVariable[];
}

export interface NotificationTemplateItem {
  key: string;
  displayName: string;
  description: string;
  title: string;
  message: string;
  enabled: boolean;
  /** `code` : gabarit integre au code ; `database` : gabarit administre. */
  source: "code" | "database";
  customized: boolean;
  version: number;
  updatedAt: string | null;
  defaultTitle: string;
  defaultMessage: string;
  variables: CommunicationTemplateVariable[];
}

export interface SystemSnippetItem {
  key: string;
  displayName: string;
  description: string;
  body: string;
  /** `code` : gabarit integre au code ; `database` : gabarit administre. */
  source: "code" | "database";
  customized: boolean;
  version: number;
  updatedAt: string | null;
  defaultBody: string;
  maxLength: number;
}

export interface CommunicationTemplateCollection {
  emailTemplates: EmailTemplateItem[];
  notificationTemplates: NotificationTemplateItem[];
  snippets: SystemSnippetItem[];
  persistent: boolean;
}

export interface EmailTemplateUpdatePayload {
  subject: string;
  body: string;
  enabled: boolean;
  expectedVersion: number;
}

export interface NotificationTemplateUpdatePayload {
  title: string;
  message: string;
  enabled: boolean;
  expectedVersion: number;
}

export interface SystemSnippetUpdatePayload {
  body: string;
  expectedVersion: number;
}

export interface CommunicationTemplateRestorePayload {
  expectedVersion: number;
}

export interface EmailTemplatePreviewPayload {
  subject: string;
  body: string;
}

export interface EmailTemplatePreviewResponse {
  code: string;
  message: string;
  subject: string | null;
  body: string | null;
  correlationId: string;
}

export interface EmailTemplateTestPayload {
  recipient: string;
}

export interface EmailTemplateMutationResponse {
  code: string;
  message: string;
  template: EmailTemplateItem | null;
  correlationId: string;
}

export interface NotificationTemplateMutationResponse {
  code: string;
  message: string;
  template: NotificationTemplateItem | null;
  correlationId: string;
}

export interface SystemSnippetMutationResponse {
  code: string;
  message: string;
  snippet: SystemSnippetItem | null;
  correlationId: string;
}

export interface CommunicationTemplateSimpleResponse {
  code: string;
  message: string;
  correlationId: string;
}

export interface CommunicationTemplateRevisionsResponse {
  revisions: CommunicationTemplateRevisionItem[];
}

/** Portee d'un historique de gabarit. */
export type CommunicationTemplateScope = "email" | "notification" | "snippet";

/** Textes systeme publics, exposes sans authentification. */
export interface PublicSystemSnippets {
  snippets: Record<string, string>;
}

// --- Diagnostic administrable (Centre de configuration, section 9) ----------
// DSL declarative et fermee : aucun script, aucune expression arbitraire. Les
// operateurs disponibles sont definis ici et interpretes par le code.

export const DIAGNOSTIC_CONDITION_OPERATORS = [
  /** La reponse simple vaut exactement `values[0]`. */
  "equals",
  /** La reponse simple n'appartient pas a `values` (non repondu = vrai). */
  "not_equals",
  /** La reponse simple appartient a `values`. */
  "one_of",
  /** La reponse multiple contient au moins une valeur de `values`. */
  "includes",
  /** La reponse multiple vaut exactement l'ensemble `values`. */
  "only",
  /** La question a recu une reponse non vide. */
  "answered",
] as const;

export type DiagnosticConditionOperator =
  (typeof DIAGNOSTIC_CONDITION_OPERATORS)[number];

export interface DiagnosticConditionConfig {
  questionId: string;
  operator: DiagnosticConditionOperator;
  values: string[];
}

export interface DiagnosticQuestionOptionConfig {
  value: string;
  label: string;
  /** Option qui vide les autres choix d'une question multiple. */
  exclusive: boolean;
}

export interface DiagnosticQuestionVisibilityConfig {
  questionId: string;
  values: string[];
}

export interface DiagnosticQuestionConfig {
  id: string;
  legend: string;
  summaryLabel: string;
  mode: "single" | "multi";
  hint: string | null;
  when: DiagnosticQuestionVisibilityConfig | null;
  options: DiagnosticQuestionOptionConfig[];
}

/** Regle de texte de resultat. La premiere regle satisfaite gagne. */
export interface DiagnosticGuidanceRuleConfig {
  /** Identifiant stable affiche par le simulateur, ex. `DIA-BACKUP-STD`. */
  id: string;
  when: DiagnosticConditionConfig[];
  title: string;
  body: string;
  points: string[];
}

/**
 * Traduction declarative des reponses vers les besoins Billing V2. Le
 * diagnostic ne calcule jamais de prix : il ne produit qu'une intention.
 */
export interface DiagnosticBillingMappingConfig {
  /** Toutes ces conditions doivent tenir, sinon cadrage/devis. */
  requireAll: DiagnosticConditionConfig[];
  usersQuestionId: string | null;
  structureQuestionId: string | null;
  storageQuestionId: string | null;
  restoreTestQuestionId: string | null;
  needsRemoteFilesWhen: DiagnosticConditionConfig[] | null;
  needsVpnWhen: DiagnosticConditionConfig[] | null;
  needsWindowsDesktopWhen: DiagnosticConditionConfig[] | null;
  individualDataKind: string;
  organisationDataKind: string;
}

export interface DiagnosticContextConfig {
  id: string;
  label: string;
  eyebrow: string;
  title: string;
  intro: string;
  contactSubject: string;
  formulaEligible: boolean;
  questions: DiagnosticQuestionConfig[];
  /** Ordonnee ; la derniere regle doit etre inconditionnelle. */
  guidance: DiagnosticGuidanceRuleConfig[];
  billingMapping: DiagnosticBillingMappingConfig | null;
}

export interface DiagnosticConfiguration {
  schemaVersion: 1;
  contexts: DiagnosticContextConfig[];
}

export type DiagnosticConfigurationState = "draft" | "published";

export interface DiagnosticConfigurationRevisionItem {
  state: DiagnosticConfigurationState;
  version: number;
  outcome: string;
  actorUserId: string | null;
  correlationId: string;
  createdAt: string;
}

export interface DiagnosticConfigurationSnapshot {
  state: DiagnosticConfigurationState;
  version: number;
  /** `code` quand aucune version n'est enregistree. */
  source: "code" | "database";
  updatedAt: string | null;
  /**
   * `null` tant qu'aucune version n'est enregistree : le WebPortal retombe
   * alors sur la configuration integree a son code. L'API ne duplique pas
   * cette valeur par defaut, ce qui evite deux sources de verite.
   */
  configuration: DiagnosticConfiguration | null;
}

/** Version publiee exposee au parcours public. */
export interface PublicDiagnosticConfigurationResponse {
  version: number;
  source: "code" | "database";
  updatedAt: string | null;
  configuration: DiagnosticConfiguration | null;
}

export interface DiagnosticConfigurationAdminView {
  draft: DiagnosticConfigurationSnapshot;
  published: DiagnosticConfigurationSnapshot;
  /** Vrai quand le brouillon differe de la version publiee. */
  draftDiffers: boolean;
  persistent: boolean;
}

export interface DiagnosticConfigurationUpdatePayload {
  configuration: DiagnosticConfiguration;
  expectedVersion: number;
}

export interface DiagnosticConfigurationPublishPayload {
  expectedDraftVersion: number;
  expectedPublishedVersion: number;
}

export interface DiagnosticConfigurationMutationResponse {
  code: string;
  message: string;
  /** Erreurs de validation renvoyees par le registre ferme cote API. */
  errors: string[];
  view: DiagnosticConfigurationAdminView | null;
  correlationId: string;
}

export interface DiagnosticConfigurationRevisionsResponse {
  revisions: DiagnosticConfigurationRevisionItem[];
}
