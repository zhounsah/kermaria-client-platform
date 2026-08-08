export type DemoSectionId =
  | "dashboard"
  | "services"
  | "subscription"
  | "invoices"
  | "storage"
  | "backups"
  | "users"
  | "support"
  | "security"
  | "activity"
  | "profile";

export type DemoService = {
  id: string;
  name: string;
  summary: string;
  status: "active";
  included: string;
};

export type DemoInvoice = {
  reference: string;
  date: string;
  title: string;
  status: "paid";
  amount: string;
};

export type DemoBackupRun = {
  date: string;
  result: "success" | "warning";
  label: string;
  protectedData: string;
  duration: string;
};

export type DemoUser = {
  name: string;
  role: string;
  status: "Actif";
  lastLogin: string;
  services: string[];
};

export type DemoTicket = {
  reference: string;
  subject: string;
  status: "Résolu" | "En cours";
  date: string;
  category: string;
  priority: "Normale" | "Haute";
  messages: { author: string; date: string; text: string }[];
};

export const demoClientSpace = {
  customer: {
    organization: "Association Horizon Bretagne",
    type: "Association",
    pack: "Pack Pro / Association",
    status: "Actif",
    since: "14 janvier 2026",
    reference: "DEMO-ASSO-001",
    address: "12 rue des Horizons, 35000 Rennes",
    phone: "02 99 00 00 00",
    email: "contact@demo.zacharyit.example",
  },
  summary: {
    activeServices: 10,
    users: 6,
    storageUsed: "17,4 Go",
    storageTotal: "32 Go",
    storagePercent: 54,
    backupStatus: "Protégé",
    lastBackup: "Aujourd'hui à 03:12",
    openTickets: 1,
    dueInvoices: 0,
  },
  subscription: {
    plan: "Pack Pro / Association",
    status: "Actif",
    cycle: "Mensuel",
    subscribedAt: "14/01/2026",
    nextBillingAt: "01/09/2026",
    storageIncluded: "32 Go",
    users: "6 actifs",
    monthlyPrice: "39,90 EUR",
    setupFee: "0,00 EUR",
    services: "Tous les services du pack activés",
    history: [
      "14/01/2026 - Souscription initiale validée",
      "01/03/2026 - Extension de stockage incluse activée",
      "01/06/2026 - Vérification annuelle de sécurité planifiée",
    ],
  },
  services: [
    {
      id: "storage",
      name: "Espace de stockage",
      summary: "32 Go de stockage sécurisé inclus pour les documents de l'association.",
      status: "active",
      included: "Actif pour 6 utilisateurs",
    },
    {
      id: "backup",
      name: "Sauvegarde automatique",
      summary: "Sauvegarde quotidienne avec rétention de 31 jours.",
      status: "active",
      included: "Dernière sauvegarde réussie aujourd'hui",
    },
    {
      id: "support",
      name: "Assistance informatique",
      summary: "Support pour les demandes liées aux services du compte.",
      status: "active",
      included: "1 ticket en cours",
    },
    {
      id: "users",
      name: "Gestion des utilisateurs",
      summary: "Création, suivi et rôles des utilisateurs rattachés au compte.",
      status: "active",
      included: "6 utilisateurs actifs",
    },
    {
      id: "vpn",
      name: "Accès distant sécurisé",
      summary: "Accès distant sécurisé pour les membres autorisés.",
      status: "active",
      included: "3 accès autorisés",
    },
    {
      id: "remote-desktop",
      name: "Bureau distant",
      summary: "Accès distant au poste ou à l'environnement géré.",
      status: "active",
      included: "2 sessions récentes",
    },
    {
      id: "nextcloud",
      name: "Accès web aux documents",
      summary: "Consultation des documents partagés depuis le navigateur.",
      status: "active",
      included: "Synchronisation active",
    },
    {
      id: "security",
      name: "Sécurité du compte",
      summary: "Double authentification, sessions et contrôles de connexion.",
      status: "active",
      included: "Aucune alerte",
    },
    {
      id: "restore",
      name: "Demandes de restauration",
      summary: "Demande assistée de restauration depuis une sauvegarde disponible.",
      status: "active",
      included: "Formulaire de demande disponible",
    },
    {
      id: "billing",
      name: "Facturation et abonnement",
      summary: "Factures, échéances et suivi du pack souscrit.",
      status: "active",
      included: "Aucune facture à payer",
    },
  ] satisfies DemoService[],
  invoices: [
    {
      reference: "FAC-DEMO-2026-008",
      date: "01/08/2026",
      title: "Pack Pro / Association",
      status: "paid",
      amount: "39,90 EUR",
    },
    {
      reference: "FAC-DEMO-2026-007",
      date: "01/07/2026",
      title: "Pack Pro / Association",
      status: "paid",
      amount: "39,90 EUR",
    },
    {
      reference: "FAC-DEMO-2026-006",
      date: "01/06/2026",
      title: "Pack Pro / Association",
      status: "paid",
      amount: "39,90 EUR",
    },
    {
      reference: "FAC-DEMO-2026-005",
      date: "01/05/2026",
      title: "Pack Pro / Association",
      status: "paid",
      amount: "39,90 EUR",
    },
  ] satisfies DemoInvoice[],
  storage: {
    used: "17,4 Go",
    total: "32 Go",
    available: "14,6 Go",
    percent: 54,
    categories: [
      { label: "Documents", value: "8,6 Go", percent: 49 },
      { label: "Photos", value: "3,8 Go", percent: 22 },
      { label: "Projets", value: "2,7 Go", percent: 16 },
      { label: "Archives", value: "1,5 Go", percent: 9 },
      { label: "Autres", value: "0,8 Go", percent: 4 },
    ],
    history: [
      { month: "Mars", value: 11.2 },
      { month: "Avril", value: 13.1 },
      { month: "Mai", value: 14.8 },
      { month: "Juin", value: 15.9 },
      { month: "Juillet", value: 16.8 },
      { month: "Août", value: 17.4 },
    ],
    folders: [
      "Administration",
      "Comptabilité / Factures 2026",
      "Événements",
      "Photos adhérents",
      "Archives du bureau",
    ],
  },
  backups: {
    status: "Protégé",
    lastRun: "Aujourd'hui à 03:12",
    lastSuccess: "Aujourd'hui à 03:12",
    protectedData: "17,4 Go",
    retention: "31 jours",
    nextRun: "Cette nuit",
    verification: "Aujourd'hui à 04:00",
    runs: [
      {
        date: "08/08/2026",
        result: "success",
        label: "Réussie",
        protectedData: "17,4 Go",
        duration: "18 min",
      },
      {
        date: "07/08/2026",
        result: "success",
        label: "Réussie",
        protectedData: "17,3 Go",
        duration: "17 min",
      },
      {
        date: "06/08/2026",
        result: "success",
        label: "Réussie",
        protectedData: "17,2 Go",
        duration: "17 min",
      },
      {
        date: "05/08/2026",
        result: "warning",
        label: "Réussie avec avertissement",
        protectedData: "17,2 Go",
        duration: "22 min",
      },
      {
        date: "04/08/2026",
        result: "success",
        label: "Réussie",
        protectedData: "17,1 Go",
        duration: "16 min",
      },
    ],
    restoreRequests: [
      {
        reference: "REST-DEMO-041",
        item: "Comptabilité / Factures 2026",
        wantedDate: "06/08/2026",
        status: "Analyse support",
      },
    ],
  },
  users: [
    {
      name: "Claire Martin",
      role: "Administratrice",
      status: "Actif",
      lastLogin: "Aujourd'hui à 09:14",
      services: ["Stockage", "Accès distant", "Facturation"],
    },
    {
      name: "Lucas Bernard",
      role: "Utilisateur",
      status: "Actif",
      lastLogin: "Hier a 17:42",
      services: ["Stockage", "Accès web"],
    },
    {
      name: "Emma Le Goff",
      role: "Utilisateur",
      status: "Actif",
      lastLogin: "06/08/2026",
      services: ["Stockage", "Bureau distant"],
    },
    {
      name: "Thomas Robert",
      role: "Gestionnaire",
      status: "Actif",
      lastLogin: "05/08/2026",
      services: ["Stockage", "Facturation"],
    },
    {
      name: "Nadia Perrin",
      role: "Utilisateur",
      status: "Actif",
      lastLogin: "04/08/2026",
      services: ["Stockage"],
    },
    {
      name: "Hugo Leclerc",
      role: "Utilisateur",
      status: "Actif",
      lastLogin: "02/08/2026",
      services: ["Stockage", "Accès distant"],
    },
  ] satisfies DemoUser[],
  tickets: [
    {
      reference: "DEMO-1871",
      subject: "Question concernant la restauration d'un fichier",
      status: "En cours",
      date: "08/08/2026",
      category: "Sauvegarde",
      priority: "Normale",
      messages: [
        {
          author: "Claire Martin",
          date: "08/08/2026 09:30",
          text: "Un fichier de comptabilité semble avoir été supprimé par erreur.",
        },
        {
          author: "Support Zachary IT",
          date: "08/08/2026 10:05",
          text: "Demande prise en charge. Vérification de la version du 06/08/2026 en cours.",
        },
      ],
    },
    {
      reference: "DEMO-1842",
      subject: "Demande d'accès pour un nouvel utilisateur",
      status: "Résolu",
      date: "05/08/2026",
      category: "Utilisateurs",
      priority: "Normale",
      messages: [
        {
          author: "Thomas Robert",
          date: "05/08/2026 11:10",
          text: "Merci d'ajouter un accès stockage pour un membre du bureau.",
        },
        {
          author: "Support Zachary IT",
          date: "05/08/2026 14:20",
          text: "Accès créé dans cette démonstration fictive.",
        },
      ],
    },
    {
      reference: "DEMO-1794",
      subject: "Configuration d'un poste",
      status: "Résolu",
      date: "29/07/2026",
      category: "Assistance",
      priority: "Haute",
      messages: [
        {
          author: "Lucas Bernard",
          date: "29/07/2026 08:45",
          text: "Le poste utilisé pour l'accueil doit accéder aux documents partagés.",
        },
        {
          author: "Support Zachary IT",
          date: "29/07/2026 15:35",
          text: "Configuration terminée et contrôlée.",
        },
      ],
    },
  ] satisfies DemoTicket[],
  security: {
    checks: [
      "Mot de passe conforme",
      "Double authentification activée",
      "6 utilisateurs actifs",
      "Aucune connexion suspecte détectée",
      "Dernière vérification : aujourd'hui à 04:00",
    ],
    logins: [
      {
        location: "Rennes, France",
        date: "08/08/2026 09:14",
        result: "Connexion réussie",
        ip: "192.0.2.14",
      },
      {
        location: "Bruz, France",
        date: "07/08/2026 17:42",
        result: "Connexion réussie",
        ip: "198.51.100.42",
      },
      {
        location: "Rennes, France",
        date: "06/08/2026 08:55",
        result: "Connexion réussie",
        ip: "203.0.113.8",
      },
    ],
  },
  activity: [
    {
      time: "Aujourd'hui 09:14",
      text: "Claire Martin s'est connectée",
      type: "Connexion",
    },
    {
      time: "Aujourd'hui 03:12",
      text: "Sauvegarde terminée avec succès",
      type: "Sauvegarde",
    },
    {
      time: "Hier 17:42",
      text: "Lucas Bernard s'est connecté",
      type: "Connexion",
    },
    {
      time: "07/08/2026",
      text: "Facture de juillet enregistrée comme payée",
      type: "Facturation",
    },
    {
      time: "06/08/2026",
      text: "Nouvel utilisateur ajouté",
      type: "Utilisateurs",
    },
  ],
  notifications: [
    {
      title: "Sauvegarde réussie cette nuit",
      message: "Vos données sont protégées avec la sauvegarde du 08/08/2026.",
      tone: "success",
    },
    {
      title: "Nouvelle facture disponible",
      message: "La facture FAC-DEMO-2026-008 est consultable dans la démo.",
      tone: "info",
    },
    {
      title: "Support en cours",
      message: "Votre demande #DEMO-1871 est en cours de traitement.",
      tone: "warning",
    },
  ],
} as const;

export const demoNavigation: { id: DemoSectionId; label: string }[] = [
  { id: "dashboard", label: "Tableau de bord" },
  { id: "services", label: "Services" },
  { id: "subscription", label: "Abonnement" },
  { id: "invoices", label: "Factures" },
  { id: "storage", label: "Stockage" },
  { id: "backups", label: "Sauvegardes" },
  { id: "users", label: "Utilisateurs" },
  { id: "support", label: "Assistance" },
  { id: "security", label: "Sécurité" },
  { id: "activity", label: "Activité" },
  { id: "profile", label: "Profil" },
];

export function normalizeDemoSection(value: string | undefined): DemoSectionId {
  if (!value) {
    return "dashboard";
  }

  return demoNavigation.some((item) => item.id === value)
    ? (value as DemoSectionId)
    : "dashboard";
}

const routeSlugToSection: Record<string, DemoSectionId> = {
  abonnement: "subscription",
  factures: "invoices",
  stockage: "storage",
  sauvegardes: "backups",
  utilisateurs: "users",
  assistance: "support",
  securite: "security",
  activite: "activity",
  profil: "profile",
  services: "services",
};

export function sectionFromDemoRouteSlug(
  slug: string | undefined,
): DemoSectionId {
  return slug ? routeSlugToSection[slug] ?? "dashboard" : "dashboard";
}
