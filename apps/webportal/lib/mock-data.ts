import type {
  ClientProfile,
  CommercialDocumentDetail,
  CommercialDocumentSummary,
  CommercialOfferSummary,
  InvoiceSummary,
  ManagedContentDetail,
  ManagedContentKey,
  ManagedContentSummary,
  PublicPackCode,
  PortalSummary,
  ServiceCatalogItem,
  ServiceSummary,
  SupportRequestSummary,
} from "@kermaria/shared";
import {
  getManagedContentRegistry,
  PUBLIC_PACKS,
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

const mockTechnicalCommercialOffers: CommercialOfferSummary[] = [
  {
    id: "offer-admin-001",
    name: "Audit poste de travail",
    description: "Revue informative d'un poste ou environnement ciblé.",
    category: "Audit",
    unitLabel: "forfait",
    priceKind: "ht",
    priceAmountCents: 12000,
    currency: "EUR",
    taxRateBasisPoints: 2000,
    externalReference: null,
    technicalServiceReferences: [],
    provisioningGroupSamAccountNames: [],
    status: "active",
    displayOrder: 10,
    billingCadence: "one_time",
    setupFeeAmountCents: null,
    billingIntervalMonths: null,
    commitmentMonths: null,
    paymentMode: null,
    publicPackCode: null,
    paypalPlanIdSandbox: null,
    paypalPlanIdLive: null,
    stripePriceIdTest: null,
    stripePriceIdLive: null,
    createdAt: "2026-06-01T09:00:00Z",
    updatedAt: "2026-06-01T09:00:00Z",
  },
  {
    id: "offer-admin-002",
    name: "Intervention ponctuelle",
    description:
      "Intervention technique préparée selon le périmètre validé.",
    category: "Assistance",
    unitLabel: "heure",
    priceKind: "ht",
    priceAmountCents: 8500,
    currency: "EUR",
    taxRateBasisPoints: 2000,
    externalReference: null,
    technicalServiceReferences: [],
    provisioningGroupSamAccountNames: [],
    status: "active",
    displayOrder: 20,
    billingCadence: "one_time",
    setupFeeAmountCents: null,
    billingIntervalMonths: null,
    commitmentMonths: null,
    paymentMode: null,
    publicPackCode: null,
    paypalPlanIdSandbox: null,
    paypalPlanIdLive: null,
    stripePriceIdTest: null,
    stripePriceIdLive: null,
    createdAt: "2026-06-01T09:05:00Z",
    updatedAt: "2026-06-01T09:05:00Z",
  },
  {
    id: "offer-admin-003",
    name: "Sauvegarde additionnelle",
    description: "Option informative de sauvegarde supplémentaire.",
    category: "Continuité",
    unitLabel: "mois",
    priceKind: "ht",
    priceAmountCents: 2400,
    currency: "EUR",
    taxRateBasisPoints: 2000,
    externalReference: null,
    technicalServiceReferences: [],
    provisioningGroupSamAccountNames: [],
    status: "active",
    displayOrder: 30,
    billingCadence: "one_time",
    setupFeeAmountCents: null,
    billingIntervalMonths: null,
    commitmentMonths: null,
    paymentMode: null,
    publicPackCode: null,
    paypalPlanIdSandbox: null,
    paypalPlanIdLive: null,
    stripePriceIdTest: null,
    stripePriceIdLive: null,
    createdAt: "2026-06-01T09:10:00Z",
    updatedAt: "2026-06-01T09:10:00Z",
  },
];

const mockCatalogServiceOffers: CommercialOfferSummary[] = [
  createMockCatalogServiceOffer(
    "offer-service-storage-32",
    "Stockage personnel 32 Go",
    "Socle de stockage nominatif pour les fichiers personnels du client.",
    "STOCK-PERSO-32",
    210,
  ),
  createMockCatalogServiceOffer(
    "offer-service-backup",
    "Sauvegarde du stockage personnel",
    "Sauvegarde régulière associée au stockage personnel.",
    "SAVE-PERSO",
    220,
  ),
  createMockCatalogServiceOffer(
    "offer-service-vpn",
    "Accès VPN",
    "Accès VPN nominatif provisionnable via groupe de sécurité AD.",
    "ACCES-VPN",
    230,
    ["GG_VPN"],
  ),
  createMockCatalogServiceOffer(
    "offer-service-supervision",
    "Supervision du service",
    "Supervision et suivi opérationnel du service couvert.",
    "SUPERV-SERVICE",
    240,
  ),
  createMockCatalogServiceOffer(
    "offer-service-support-l1",
    "Support niveau 1",
    "Support de premier niveau associé au service couvert.",
    "SUPPORT-LV1",
    250,
  ),
  createMockCatalogServiceOffer(
    "offer-service-rds",
    "Bureau Windows / RDS",
    "Accès bureau distant Windows provisionnable via groupe de sécurité AD.",
    "ACCES-RDS",
    260,
    ["GG_RDS"],
  ),
  createMockCatalogServiceOffer(
    "offer-service-user-add",
    "Utilisateur supplémentaire",
    "Ajout d'un utilisateur supplémentaire au périmètre du client.",
    "USER-ADD",
    270,
  ),
  createMockCatalogServiceOffer(
    "offer-service-storage-plus",
    "Stockage supplémentaire 32 Go",
    "Extension de stockage complémentaire au service principal.",
    "STOCK-SUP-32",
    280,
  ),
  createMockCatalogServiceOffer(
    "offer-service-doc-tech",
    "Documentation technique",
    "Documentation complémentaire rattachée au service couvert.",
    "DOC-TECH",
    290,
  ),
  createMockCatalogServiceOffer(
    "offer-service-nextcloud",
    "Nextcloud",
    "Accès Nextcloud provisionnable via groupe de sécurité AD.",
    "NEXTCLOUD",
    300,
    ["GG_NextCloud"],
  ),
];

const PUBLIC_PACK_PRICING: Record<
  PublicPackCode,
  { monthlyAmountCents: number; setupFeeAmountCents: number }
> = {
  "pack-dossier-securise": {
    monthlyAmountCents: 900,
    setupFeeAmountCents: 1500,
  },
  "pack-acces-distance": {
    monthlyAmountCents: 1900,
    setupFeeAmountCents: 2500,
  },
  "pack-bureau-windows-distance": {
    monthlyAmountCents: 3500,
    setupFeeAmountCents: 3500,
  },
  "pack-pro-association": {
    monthlyAmountCents: 4900,
    setupFeeAmountCents: 4900,
  },
};

function resolveDiscountMultiplier(commitmentMonths: 1 | 6 | 12) {
  switch (commitmentMonths) {
    case 6:
      return 0.9;
    case 12:
      return 0.8;
    default:
      return 1;
  }
}

function createMockPublicPackOffers(): CommercialOfferSummary[] {
  return PUBLIC_PACKS.flatMap((pack) =>
    pack.variants.map((variant, index) => {
      const pricing = PUBLIC_PACK_PRICING[pack.key];
      const discountedMonthlyAmountCents = Math.round(
        pricing.monthlyAmountCents
          * resolveDiscountMultiplier(variant.commitmentMonths),
      );
      const billingIntervalMonths =
        variant.paymentMode === "upfront" ? variant.commitmentMonths : 1;
      const priceAmountCents =
        variant.paymentMode === "upfront"
          ? discountedMonthlyAmountCents * variant.commitmentMonths
          : discountedMonthlyAmountCents;

      return {
        id:
          `offer-${pack.slug}-${variant.commitmentMonths}-`
          + `${variant.paymentMode}`,
        name: pack.label,
        description: pack.description,
        category: "Pack grand public",
        unitLabel: variant.paymentMode === "upfront" ? "engagement" : "mois",
        priceKind: "ht",
        priceAmountCents,
        currency: "EUR",
        taxRateBasisPoints: 2000,
        externalReference: variant.externalReference,
        technicalServiceReferences: [...pack.technicalServiceReferences],
        provisioningGroupSamAccountNames: [],
        status: "active",
        displayOrder: pack.order + index,
        billingCadence: "monthly",
        setupFeeAmountCents: pricing.setupFeeAmountCents,
        billingIntervalMonths,
        commitmentMonths: variant.commitmentMonths,
        paymentMode: variant.paymentMode,
        publicPackCode: pack.key,
        paypalPlanIdSandbox: null,
        paypalPlanIdLive: null,
        stripePriceIdTest: null,
        stripePriceIdLive: null,
        createdAt: "2026-07-07T08:00:00Z",
        updatedAt: "2026-07-07T08:00:00Z",
      };
    }),
  );
}

export const mockCommercialOffers: CommercialOfferSummary[] = [
  ...mockTechnicalCommercialOffers,
  ...mockCatalogServiceOffers,
  ...createMockPublicPackOffers(),
];

function createMockCatalogServiceOffer(
  id: string,
  name: string,
  description: string,
  externalReference: string,
  displayOrder: number,
  provisioningGroupSamAccountNames: string[] = [],
): CommercialOfferSummary {
  return {
    id,
    name,
    description,
    category: "Service technique",
    unitLabel: "forfait",
    priceKind: "ht",
    priceAmountCents: 0,
    currency: "EUR",
    taxRateBasisPoints: null,
    externalReference,
    technicalServiceReferences: [externalReference],
    provisioningGroupSamAccountNames,
    status: "inactive",
    displayOrder,
    billingCadence: "one_time",
    setupFeeAmountCents: null,
    billingIntervalMonths: null,
    commitmentMonths: null,
    paymentMode: null,
    publicPackCode: null,
    paypalPlanIdSandbox: null,
    paypalPlanIdLive: null,
    stripePriceIdTest: null,
    stripePriceIdLive: null,
    createdAt: "2026-07-14T08:00:00Z",
    updatedAt: "2026-07-14T08:00:00Z",
  };
}

export const mockCommercialDocuments: CommercialDocumentSummary[] = [
  {
    id: "commercial-doc-mock-001",
    documentType: "quote_draft",
    status: "shared_with_customer",
    title: "Proposition d'accompagnement VPN",
    internalReference: "COM-20260612-0001",
    currency: "EUR",
    subtotalAmountCents: 19400,
    taxAmountCents: 3880,
    totalAmountCents: 23280,
    disclaimer: "Document informatif — ne constitue pas une facture officielle.",
    createdAt: "2026-06-12T10:00:00Z",
    updatedAt: "2026-06-12T10:30:00Z",
    sharedAt: "2026-06-12T10:30:00Z",
    serviceRequestId: "service-request-mock-001",
    serviceRequestReference: "SRV-MOCK-ADMIN-001",
    paymentMethod: null,
  },
];

export const mockCommercialDocumentDetails: Record<string, CommercialDocumentDetail> =
  {
    "commercial-doc-mock-001": {
      ...mockCommercialDocuments[0],
      lines: [
        {
          id: "commercial-line-mock-001",
          offerId: "offer-admin-002",
          label: "Intervention ponctuelle",
          description: "Qualification informative de l'accès VPN envisagé.",
          quantity: 2,
          unitLabel: "heure",
          unitPriceCents: 8500,
          taxRateBasisPoints: 2000,
          lineTotalCents: 17000,
          sortOrder: 10,
          createdAt: "2026-06-12T10:00:00Z",
          updatedAt: "2026-06-12T10:00:00Z",
        },
        {
          id: "commercial-line-mock-002",
          offerId: "offer-admin-003",
          label: "Sauvegarde additionnelle",
          description: "Option informative associée à la proposition.",
          quantity: 1,
          unitLabel: "mois",
          unitPriceCents: 2400,
          taxRateBasisPoints: 2000,
          lineTotalCents: 2400,
          sortOrder: 20,
          createdAt: "2026-06-12T10:05:00Z",
          updatedAt: "2026-06-12T10:05:00Z",
        },
      ],
    },
  };

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

function createMockPackSheetBody(packCode: PublicPackCode) {
  const pack = PUBLIC_PACKS.find((item) => item.key === packCode);
  if (!pack) {
    return "## Présentation\n\nContenu indisponible.";
  }

  const componentOffers = pack.technicalServiceReferences
    .map((reference) =>
      mockCommercialOffers.find(
        (offer) => offer.externalReference === reference && offer.status === "active",
      ) ?? null,
    )
    .filter((offer): offer is CommercialOfferSummary => offer !== null);

  const lines = [
    "## Présentation",
    "",
    pack.description,
    "",
    `Public visé : ${pack.audience}`,
    "",
    "## Composants techniques liés",
    "",
    componentOffers.length > 0
      ? `La composition technique active de ce pack est calculée automatiquement. ${componentOffers.length} composant(s) sont actuellement rattaché(s) et affiché(s) séparément sur la page publique.`
      : "La composition technique active de ce pack est calculée automatiquement et affichée séparément sur la page publique.",
    "",
    "## Pré-requis",
    "",
    "- Un court cadrage reste recommandé pour valider les usages, accès et contraintes techniques.",
    "- Les accès nominatifs et besoins d'accompagnement sont confirmés avant mise en service.",
    "",
    "## Limites",
    "",
    "- Cette fiche décrit le périmètre standard du pack et ne remplace pas un devis spécifique.",
    "- Les demandes hors périmètre peuvent donner lieu à une prestation complémentaire.",
    "",
    "## Support",
    "",
    "- Le support inclus suit le périmètre standard affiché sur la vitrine.",
    "- Les changements structurants ou migrations étendues sont qualifiés séparément.",
  ];

  return lines.join("\n");
}

function createMockManagedContentDetail(
  key: ManagedContentKey,
): ManagedContentDetail | null {
  const entry = getManagedContentRegistry().find((item) => item.key === key);
  if (!entry) {
    return null;
  }

  const baseTimestamps = {
    createdAt: "2026-07-07T08:00:00Z",
    updatedAt: "2026-07-07T08:00:00Z",
  };

  switch (key) {
    case "legal:cgv":
      return {
        ...entry,
        versionLabel: "Version du : 07 juillet 2026",
        bodyMarkdown: [
          "Les présentes Conditions Générales de Vente s'appliquent aux prestations proposées par Zachary IT.",
          "",
          "## Objet",
          "",
          "Les prestations couvertes comprennent notamment l'hébergement de dossiers, la sauvegarde, l'accès distant, le support et les interventions informatiques décrites dans les devis ou propositions commerciales.",
          "",
          "## Commandes et exécution",
          "",
          "Toute commande validée implique l'acceptation pleine et entière des CGV. Le périmètre exact reste défini par le devis, la proposition commerciale ou la facture associée.",
          "",
          "## Facturation et paiement",
          "",
          "Les prix sont exprimés en euros. La mention de franchise en base de TVA s'applique lorsque le régime concerné est en vigueur.",
          "",
          "## Données et responsabilité",
          "",
          "Le Client reste responsable des contenus confiés. Zachary IT intervient dans une obligation de moyens et selon le périmètre convenu.",
        ].join("\n"),
        ...baseTimestamps,
      };
    case "legal:mentions-legales":
      return {
        ...entry,
        versionLabel: "Dernière mise à jour : 07 juillet 2026",
        bodyMarkdown: [
          "Le présent site est édité par Zachary HOUNSA-HOUNKPA EI, nom commercial Zachary IT.",
          "",
          "## Éditeur du site",
          "",
          "**Zachary HOUNSA-HOUNKPA EI**",
          "Nom commercial : **Zachary IT**",
          "Adresse professionnelle : **3 Kermaria, 35580 Guichen, France**",
          "Adresse e-mail : **[zhounsah@home.bzh](mailto:zhounsah@home.bzh)**",
          "",
          "## Hébergement",
          "",
          "Le site est hébergé sur une infrastructure administrée par Zachary IT, avec des services tiers possibles pour la couche technique de sécurisation et de diffusion.",
          "",
          "## Propriété intellectuelle",
          "",
          "Les contenus, textes, logos et éléments graphiques restent protégés par les droits applicables.",
        ].join("\n"),
        ...baseTimestamps,
      };
    case "page:a-propos":
      return {
        ...entry,
        versionLabel: null,
        bodyMarkdown: [
          "Zachary IT est une micro-entreprise de services informatiques basée à Guichen, créée par Zachary HOUNSA-HOUNKPA.",
          "",
          "J'accompagne les particuliers, indépendants et petites structures dans la mise en place de solutions informatiques simples et compréhensibles : assistance, maintenance, sauvegarde, hébergement de dossiers, VPN privé, accès distant et accompagnement réseau.",
          "",
          "Mon objectif est de proposer des services clairs, adaptés aux besoins réels, avec une facturation transparente et une attention particulière portée à la sécurité, aux sauvegardes et à la confidentialité des données.",
          "",
          "Zachary IT s'adresse aux clients qui cherchent un interlocuteur local, accessible et capable d'expliquer les choses simplement, sans vendre une solution inutilement complexe.",
        ].join("\n"),
        ...baseTimestamps,
      };
    default:
      return {
        ...entry,
        versionLabel: null,
        bodyMarkdown: createMockPackSheetBody(entry.packCode as PublicPackCode),
        ...baseTimestamps,
      };
  }
}

export const mockManagedContentDetails = new Map<
  ManagedContentKey,
  ManagedContentDetail
>(
  getManagedContentRegistry()
    .map((entry) => createMockManagedContentDetail(entry.key))
    .filter((entry): entry is ManagedContentDetail => entry !== null)
    .map((entry) => [entry.key, entry]),
);

export const mockManagedContentSummaries: ManagedContentSummary[] =
  getManagedContentRegistry()
    .map((entry) => {
      const detail = mockManagedContentDetails.get(entry.key);
      return detail
        ? {
            key: detail.key,
            contentType: detail.contentType,
            title: detail.title,
            publicPath: detail.publicPath,
            versionLabel: detail.versionLabel,
            updatedAt: detail.updatedAt,
          }
        : null;
    })
    .filter((entry): entry is ManagedContentSummary => entry !== null);

export function getMockManagedContent(
  key: ManagedContentKey,
): ManagedContentDetail | null {
  return mockManagedContentDetails.get(key) ?? null;
}
