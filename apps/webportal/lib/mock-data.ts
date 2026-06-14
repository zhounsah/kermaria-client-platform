import type {
  ClientProfile,
  InvoiceSummary,
  PortalSummary,
  ServiceCatalogItem,
  ServiceSummary,
  SupportRequestSummary,
} from "@kermaria/shared";

export const mockCustomer: ClientProfile = {
  companyName: "Zachary HOUNSA-HOUNKPA EI - Client démo",
  customerReference: "CLI-DEMO-0042",
  contactName: "Contact de démonstration",
  email: "client.demo@example.invalid",
  phone: "+33 0 00 00 00 00",
  address: "12 rue de la Démonstration",
  city: "44000 Nantes",
  country: "France",
  accountStatus: "active",
};

export const mockServices: ServiceSummary[] = [
  {
    id: "svc-personal-hosting-001",
    reference: "SVC-HDP-001",
    name: "Hébergement dossier personnel",
    type: "personal_hosting",
    status: "active",
    description:
      "Espace d'hébergement fictif pour un dossier personnel, selon le périmètre convenu.",
    startedAt: "2026-01-15",
    scope: "Espace personnel et accès nominatif de démonstration",
    commercialTerms: "Selon devis",
  },
  {
    id: "svc-backup-001",
    reference: "SVC-SAV-004",
    name: "Sauvegarde dossier personnel",
    type: "backup",
    status: "active",
    description:
      "Sauvegarde planifiée avec vérifications prévues, sans garantie absolue de récupération.",
    startedAt: "2026-01-15",
    scope: "Dossier personnel inclus dans la démonstration",
    commercialTerms: "Inclus selon périmètre",
  },
  {
    id: "svc-vpn-001",
    reference: "SVC-VPN-007",
    name: "Accès VPN privé",
    type: "vpn",
    status: "pending",
    description:
      "Accès VPN chiffré en cours de qualification, adapté au besoin exprimé.",
    startedAt: null,
    scope: "Un accès nominatif, sous réserve de validation technique",
    commercialTerms: "Selon devis",
    nextStep: "Vérifications techniques prévues avant toute activation",
  },
  {
    id: "svc-rds-001",
    reference: "SVC-RDS-003",
    name: "Accès bureau distant / RDS",
    type: "rds",
    status: "suspended",
    description:
      "Accès distant fictif suspendu dans la démonstration, sans action sur une infrastructure réelle.",
    startedAt: "2025-10-20",
    scope: "Un environnement distant défini selon le besoin",
    commercialTerms: "Selon devis",
    nextStep: "Une revue du besoin est nécessaire avant toute reprise",
  },
  {
    id: "svc-support-001",
    reference: "SVC-SUP-014",
    name: "Support technique niveau 1",
    type: "support",
    status: "active",
    description:
      "Premier niveau d'assistance et d'orientation sur les services inclus au périmètre.",
    startedAt: "2026-02-01",
    scope: "Diagnostic initial et accompagnement selon périmètre convenu",
    commercialTerms: "Inclus selon périmètre",
  },
];

export const mockInvoices: InvoiceSummary[] = [
  {
    id: "inv-2026-001",
    number: "FACT-DEMO-2026-0042",
    status: "paid",
    issuedAt: "2026-05-03",
    dueAt: "2026-05-17",
    period: "Mai 2026",
    totalAmount: 96,
    currency: "EUR",
  },
  {
    id: "inv-2026-002",
    number: "FACT-DEMO-2026-0036",
    status: "pending",
    issuedAt: "2026-06-03",
    dueAt: "2026-06-17",
    period: "Juin 2026",
    totalAmount: 96,
    currency: "EUR",
  },
  {
    id: "inv-2026-003",
    number: "FACT-DEMO-2026-0030",
    status: "paid",
    issuedAt: "2026-04-03",
    dueAt: "2026-04-17",
    period: "Avril 2026",
    totalAmount: 96,
    currency: "EUR",
  },
];

export const mockSupportRequests: SupportRequestSummary[] = [
  {
    id: "sup-001",
    reference: "SUP-DEMO-2026-018",
    subject: "Vérification d'une sauvegarde planifiée",
    status: "open",
    priority: "normal",
    serviceName: "Sauvegarde dossier personnel",
    createdAt: "2026-06-10T09:30:00Z",
    updatedAt: "2026-06-10T11:15:00Z",
  },
  {
    id: "sup-002",
    reference: "SUP-DEMO-2026-014",
    subject: "Préparation d'un accès VPN privé",
    status: "in_progress",
    priority: "high",
    serviceName: "Accès VPN privé",
    createdAt: "2026-06-05T14:20:00Z",
    updatedAt: "2026-06-11T08:45:00Z",
  },
  {
    id: "sup-003",
    reference: "SUP-DEMO-2026-009",
    subject: "Mise à jour des coordonnées de contact",
    status: "closed",
    priority: "low",
    serviceName: "Compte client",
    createdAt: "2026-05-22T10:00:00Z",
    updatedAt: "2026-05-23T16:30:00Z",
  },
];

export const mockServiceCatalog: ServiceCatalogItem[] = [
  {
    id: "catalog-personal-hosting",
    name: "Hébergement de dossiers personnels",
    category: "Hébergement",
    description:
      "Mise à disposition d'un espace adapté au volume et aux usages convenus.",
    scope: "Dimensionnement et modalités d'accès à définir",
    commercialTerms: "Selon devis",
  },
  {
    id: "catalog-backup",
    name: "Sauvegarde de données",
    category: "Continuité",
    description:
      "Plan de sauvegarde adapté au besoin, avec vérifications prévues. Aucune solution ne supprime tous les risques.",
    scope: "Sources, fréquence et rétention à confirmer",
    commercialTerms: "Selon devis",
  },
  {
    id: "catalog-vpn",
    name: "VPN privé",
    category: "Accès",
    description:
      "Accès VPN chiffré étudié selon les équipements et les usages attendus.",
    scope: "Accès nominatifs et règles réseau à définir",
    commercialTerms: "Selon devis",
  },
  {
    id: "catalog-rds",
    name: "Accès distant / RDS",
    category: "Environnement",
    description:
      "Solution d'accès distant dimensionnée après qualification du besoin.",
    scope: "Utilisateurs, applications et ressources à confirmer",
    commercialTerms: "Selon devis",
  },
  {
    id: "catalog-intervention",
    name: "Intervention ponctuelle",
    category: "Assistance",
    description:
      "Diagnostic ou intervention ciblée sur un besoin technique identifié.",
    scope: "Périmètre et délai convenus avant intervention",
    commercialTerms: "Selon devis",
  },
  {
    id: "catalog-network-advice",
    name: "Conseil réseau et infrastructure",
    category: "Conseil",
    description:
      "Analyse pragmatique et recommandations adaptées à l'environnement existant.",
    scope: "Entretien, état des lieux et recommandations",
    commercialTerms: "Selon devis",
  },
  {
    id: "catalog-documentation",
    name: "Documentation technique simplifiée",
    category: "Documentation",
    description:
      "Documentation lisible des usages, procédures ou éléments techniques convenus.",
    scope: "Sujet et niveau de détail définis ensemble",
    commercialTerms: "Selon devis",
  },
  {
    id: "catalog-migration",
    name: "Migration de données",
    category: "Données",
    description:
      "Préparation et accompagnement d'une migration avec contrôles adaptés au contexte.",
    scope: "Sources, destination, volume et fenêtre à confirmer",
    commercialTerms: "Selon devis",
  },
];

export const mockPortalSummary: PortalSummary = {
  customerReference: mockCustomer.customerReference,
  contactName: mockCustomer.contactName,
  activeServiceCount: mockServices.filter((service) => service.status === "active")
    .length,
  pendingInvoiceCount: mockInvoices.filter(
    (invoice) => invoice.status === "pending",
  ).length,
  pendingInvoiceTotal: mockInvoices
    .filter((invoice) => invoice.status === "pending")
    .reduce((total, invoice) => total + invoice.totalAmount, 0),
  openSupportRequestCount: mockSupportRequests.filter(
    (request) => request.status !== "closed",
  ).length,
  activeServiceRequestCount: 1,
  lastUpdatedAt: "2026-06-11T08:45:00Z",
};
