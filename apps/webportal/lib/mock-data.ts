import type {
  ClientProfile,
  CommercialDocumentDetail,
  CommercialDocumentSummary,
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

import { DEFAULT_DIAGNOSTIC_RECOMMENDATION_CONFIG } from "@/lib/diagnostic-recommendation-config";

const FRANCHISE_BASE_FISCAL = {
  taxRateBasisPoints: null,
  fiscalRegime: "franchise_base" as const,
  fiscalMention: "TVA non applicable, art. 293 B du CGI.",
};

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
      "Sauvegarde quotidienne avec vérifications prévues, sans garantie absolue de récupération.",
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

export const mockCommercialDocuments: CommercialDocumentSummary[] = [
  {
    id: "commercial-doc-mock-001",
    documentType: "quote_draft",
    status: "shared_with_customer",
    title: "Proposition d'accompagnement VPN",
    internalReference: "COM-20260612-0001",
    currency: "EUR",
    subtotalAmountCents: 19400,
    taxAmountCents: 0,
    totalAmountCents: 19400,
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
          label: "Intervention ponctuelle",
          description: "Qualification informative de l'accès VPN envisagé.",
          quantity: 2,
          unitLabel: "heure",
          unitPriceCents: 8500,
          ...FRANCHISE_BASE_FISCAL,
          lineTotalCents: 17000,
          sortOrder: 10,
          createdAt: "2026-06-12T10:00:00Z",
          updatedAt: "2026-06-12T10:00:00Z",
        },
        {
          id: "commercial-line-mock-002",
          label: "Sauvegarde additionnelle",
          description: "Option informative associée à la proposition.",
          quantity: 1,
          unitLabel: "mois",
          unitPriceCents: 2400,
          ...FRANCHISE_BASE_FISCAL,
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
    subject: "Vérification d'une sauvegarde quotidienne",
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

  const componentCount = pack.technicalServiceReferences.length;

  const lines = [
    "## Présentation",
    "",
    pack.description,
    "",
    `Public visé : ${pack.audience}`,
    "",
    "## Composants techniques liés",
    "",
    componentCount > 0
      ? `La composition technique active de cette offre est calculée automatiquement. ${componentCount} composant(s) sont actuellement rattaché(s) et affiché(s) séparément sur la page publique.`
      : "La composition technique active de cette offre est calculée automatiquement et affichée séparément sur la page publique.",
    "",
    "## Pré-requis",
    "",
    "- Un court cadrage reste recommandé pour valider les usages, accès et contraintes techniques.",
    "- Les accès nominatifs et besoins d'accompagnement sont confirmés avant mise en service.",
    "",
    "## Limites",
    "",
    "- Cette fiche décrit le périmètre standard de l'offre et ne remplace pas un devis spécifique.",
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
        versionLabel: "Version du : 03 août 2026",
        bodyMarkdown: [
          "Les présentes Conditions Générales de Vente s'appliquent aux prestations proposées par Zachary IT.",
          "",
          "## Objet",
          "",
          "Les prestations couvertes comprennent notamment l'hébergement de dossiers, la sauvegarde, l'accès distant, le support et les interventions informatiques décrites dans les devis ou propositions commerciales.",
          "",
          "## Sauvegarde, restauration, suppression et localisation",
          "",
          "Les données couvertes par le service de sauvegarde font l'objet d'une sauvegarde automatique quotidienne. Les versions sauvegardées sont conservées pendant 31 jours glissants. Les données créées ou modifiées depuis la dernière sauvegarde réussie peuvent ne pas être récupérables.",
          "",
          "Les caches, paramètres de session, données temporaires et autres éléments techniques reproductibles peuvent faire l'objet de politiques de conservation différentes. Ils ne sont pas assimilés aux fichiers personnels ou aux données métier du Client.",
          "",
          "Les données et leurs sauvegardes sont hébergées sur une infrastructure exploitée en Bretagne, en France. Sauf engagement contractuel spécifique, aucune copie sur un second site géographiquement distinct n'est garantie.",
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
    case "legal:politique-confidentialite":
      return {
        ...entry,
        versionLabel: "Version du : 03 août 2026",
        bodyMarkdown: [
          "La presente politique de confidentialite decrit les traitements de donnees personnelles realises dans le cadre du site et de l'espace client de Zachary HOUNSA-HOUNKPA EI.",
          "",
          "## Donnees collectees",
          "",
          "Les donnees necessaires a la gestion de la relation commerciale et contractuelle peuvent inclure l'identite, les coordonnees, l'historique des demandes, les commandes, les factures et les echanges de support.",
          "",
          "## Finalites",
          "",
          "Ces donnees sont utilisees pour gerer la relation client, assurer l'execution des services, produire les documents commerciaux, traiter les demandes et securiser l'acces a l'espace client.",
          "",
          "Les caracteristiques commerciales ou operationnelles des sauvegardes client relevent des CGV, de l'offre souscrite ou des conditions techniques applicables. Les caches, parametres de session et donnees temporaires peuvent suivre des politiques distinctes et ne sont pas assimiles aux fichiers personnels ou aux donnees metier du client.",
          "",
          "## Cookies et traceurs",
          "",
          "Le site n'utilise pas de traceurs publicitaires ni de solution d'analytique tierce. Seuls les cookies strictement necessaires au fonctionnement du service peuvent etre emis, notamment pour la session authentifiee, la protection CSRF et, si active, la verification hCaptcha.",
          "",
          "## Conservation",
          "",
          "Les donnees sont conservees pendant la duree necessaire a la relation contractuelle, puis pendant les durees legales applicables, notamment pour les obligations comptables et de facturation.",
          "",
          "## Vos droits",
          "",
          "Conformement au RGPD, vous disposez d'un droit d'acces, de rectification, d'effacement, d'opposition, de limitation et, selon les cas, de portabilite. Pour exercer vos droits, vous pouvez contacter **[contact@zachary-it.fr](mailto:contact@zachary-it.fr)**.",
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
          "Adresse e-mail : **[contact@zachary-it.fr](mailto:contact@zachary-it.fr)**",
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
          "Zachary IT est le nom commercial de Zachary HOUNSA-HOUNKPA EI, micro-entreprise de services informatiques basée à Guichen, créée par Zachary HOUNSA-HOUNKPA.",
          "",
          "J'accompagne les particuliers, indépendants et petites structures dans la mise en place de solutions informatiques simples et compréhensibles : assistance, maintenance, sauvegarde, hébergement de dossiers, VPN privé, accès distant et accompagnement réseau.",
          "",
          "Mon objectif est de proposer des services clairs, adaptés aux besoins réels, avec une facturation transparente et une attention particulière portée à la sécurité, aux sauvegardes et à la confidentialité des données.",
          "",
          "Zachary IT s'adresse aux clients qui cherchent un interlocuteur local, accessible et capable d'expliquer les choses simplement, sans vendre une solution inutilement complexe.",
        ].join("\n"),
        ...baseTimestamps,
      };
    case "page:infrastructure":
      return {
        ...entry,
        versionLabel: null,
        bodyMarkdown: [
          "Zachary IT combine selon les besoins des briques exploitées directement et des fournisseurs spécialisés, avec des responsabilités identifiées.",
          "",
          "## Principes d’exploitation",
          "",
          "L’architecture retenue dépend du service, des données, des contraintes de disponibilité et du niveau d’administration attendu.",
          "",
          "## Sauvegarde, supervision et disponibilité",
          "",
          "Une sauvegarde ne remplace pas une architecture haute disponibilité, et une alerte de supervision ne constitue pas à elle seule une garantie d’intervention immédiate.",
          "",
          "## Transparence",
          "",
          "Les fournisseurs importants, les dépendances techniques et les limites du service doivent pouvoir être identifiés clairement.",
        ].join("\n"),
        ...baseTimestamps,
      };

    case "diagnostic:recommendations":
      return {
        ...entry,
        versionLabel: null,
        bodyMarkdown: JSON.stringify(DEFAULT_DIAGNOSTIC_RECOMMENDATION_CONFIG),
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
