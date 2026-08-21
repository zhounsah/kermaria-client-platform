export type ServiceIconKey =
  | "cloud"
  | "mail"
  | "shield"
  | "headphones";

export type ServiceCallToAction = {
  href: string;
  label: string;
};

export type PublicService = {
  title: string;
  description: string;
  details: string[];
  cta: ServiceCallToAction;
};

export type ServiceCategory = {
  slug: "cloud-hebergement" | "domaines-messagerie" | "reseau-securite" | "support-it";
  title: string;
  shortTitle: string;
  menuSummary: string;
  description: string;
  audience: string;
  icon: ServiceIconKey;
  intro: string;
  problems: string[];
  services: PublicService[];
  highlights: string[];
  cta: ServiceCallToAction;
};

export const SERVICE_CATEGORIES: ServiceCategory[] = [
  {
    slug: "cloud-hebergement",
    title: "Cloud & Hébergement",
    shortTitle: "Cloud & Hébergement",
    menuSummary: "VPS · Web · Applications · Sauvegarde",
    description:
      "Des services hébergés, suivis et sauvegardés pour rester disponibles sans transformer votre équipe en administrateurs système.",
    audience: "Indépendants, associations et structures qui ont besoin de services fiables sans les gérer au quotidien.",
    icon: "cloud",
    intro:
      "Vos outils doivent rester accessibles, même quand personne n’a le temps de surveiller un serveur. Zachary IT conçoit un environnement adapté, puis en assure le suivi au quotidien.",
    problems: [
      "Un site ou une application ne doit pas dépendre d’un ordinateur laissé sur place.",
      "Les sauvegardes doivent être vérifiables et séparées de l’infrastructure principale.",
      "Les alertes techniques doivent être comprises et traitées, pas simplement reçues.",
    ],
    services: [
      {
        title: "VPS et services hébergés",
        description: "Un environnement dimensionné pour vos usages, avec une base claire pour évoluer sereinement.",
        details: ["VPS", "Applications métier", "Services web"],
        cta: { href: "/contact", label: "Demander un devis" },
      },
      {
        title: "Hébergement d’applications",
        description: "Mise en ligne et maintenance de services web ou applicatifs, avec des choix expliqués simplement.",
        details: ["Déploiement", "Mises à jour", "Disponibilité"],
        cta: { href: "/contact", label: "Nous contacter" },
      },
      {
        title: "Sauvegarde et supervision",
        description: "Des copies séparées et une supervision qui aide à agir avant qu’un incident ne bloque votre activité.",
        details: ["Sauvegarde", "Alertes utiles", "Suivi de fonctionnement"],
        cta: { href: "/formules", label: "Voir les tarifs" },
      },
    ],
    highlights: [
      "Un interlocuteur qui explique les choix et les limites.",
      "Un suivi adapté aux services réellement nécessaires.",
      "Une continuité pensée dès la mise en place.",
    ],
    cta: { href: "/contact", label: "Parler de votre projet" },
  },
  {
    slug: "domaines-messagerie",
    title: "Domaines & Messagerie",
    shortTitle: "Domaines & Messagerie",
    menuSummary: "Domaines · DNS · Microsoft 365 · E-mail",
    description:
      "Votre nom de domaine, vos e-mails et vos réglages DNS restent cohérents, sécurisés et compréhensibles.",
    audience: "Structures qui veulent une adresse professionnelle fiable, sans se perdre dans les réglages de messagerie.",
    icon: "mail",
    intro:
      "Un domaine et une messagerie sont souvent au cœur de la relation avec vos clients. Ils méritent une configuration suivie, documentée et conçue pour éviter les mauvaises surprises.",
    problems: [
      "Un domaine ne doit pas être lié au compte personnel d’un ancien prestataire ou bénévole.",
      "Les e-mails légitimes ne doivent pas finir en courrier indésirable faute de réglages adaptés.",
      "Une migration de messagerie doit préserver les échanges, les adresses et les habitudes de travail.",
    ],
    services: [
      {
        title: "Domaines et DNS",
        description: "Gestion des noms de domaine et des zones DNS avec une organisation claire et maintenable.",
        details: ["Renouvellement", "DNS", "Transfert de domaine"],
        cta: { href: "/contact", label: "Nous contacter" },
      },
      {
        title: "Messagerie professionnelle",
        description: "Une messagerie adaptée à vos usages, avec Microsoft 365 lorsque ce choix est pertinent.",
        details: ["E-mail", "Microsoft 365", "Comptes utilisateurs"],
        cta: { href: "/contact", label: "Demander un devis" },
      },
      {
        title: "Délivrabilité et migrations",
        description: "SPF, DKIM, DMARC et migrations traités avec méthode pour conserver une communication fiable.",
        details: ["SPF/DKIM/DMARC", "Migration", "Cloudflare si pertinent"],
        cta: { href: "/contact", label: "Demander un audit" },
      },
    ],
    highlights: [
      "Des accès et responsabilités identifiés.",
      "Une configuration pensée pour vos usages réels.",
      "Des migrations préparées, pas improvisées.",
    ],
    cta: { href: "/contact", label: "Sécuriser votre messagerie" },
  },
  {
    slug: "reseau-securite",
    title: "Réseau & Sécurité",
    shortTitle: "Réseau & Sécurité",
    menuSummary: "VPN · UniFi · Firewall · Cloudflare · Supervision",
    description:
      "Un réseau lisible, protégé et suivi pour que vos accès et vos outils restent disponibles sans compromis inutile.",
    audience: "TPE/PME, associations et équipes réparties qui ont besoin d’accès fiables, sur site comme à distance.",
    icon: "shield",
    intro:
      "La sécurité n’est pas une accumulation de produits. C’est un ensemble cohérent : accès, règles, exposition web, suivi et maintenance, expliqué de façon concrète.",
    problems: [
      "Les accès distants doivent être pratiques sans être ouverts à tous.",
      "Le réseau doit rester compréhensible quand il faut intervenir ou faire évoluer l’installation.",
      "Les services exposés sur internet demandent une protection et une surveillance continues.",
    ],
    services: [
      {
        title: "Réseau et Wi-Fi UniFi",
        description: "Conception, déploiement et maintenance d’un réseau adapté à vos locaux et à vos utilisateurs.",
        details: ["UniFi", "Wi-Fi", "Segmentation réseau"],
        cta: { href: "/contact", label: "Demander un audit" },
      },
      {
        title: "Accès distant et firewall",
        description: "VPN et règles de sécurité définis pour protéger les accès sans compliquer les usages quotidiens.",
        details: ["VPN", "Firewall", "Accès utilisateurs"],
        cta: { href: "/contact", label: "Parler de votre besoin" },
      },
      {
        title: "Protection des services exposés",
        description: "Reverse proxy, WAF et Cloudflare lorsque le contexte le justifie, avec supervision associée.",
        details: ["Reverse proxy", "WAF", "Supervision"],
        cta: { href: "/contact", label: "Nous contacter" },
      },
    ],
    highlights: [
      "Des règles de sécurité proportionnées à votre activité.",
      "Une maintenance réseau qui évite l’accumulation de bricolages.",
      "Un suivi qui alerte sur les signaux utiles.",
    ],
    cta: { href: "/contact", label: "Évaluer votre réseau" },
  },
  {
    slug: "support-it",
    title: "Support & IT",
    shortTitle: "Support & IT",
    menuSummary: "Assistance · Postes · Maintenance · Migrations",
    description:
      "Un accompagnement concret pour les postes, les utilisateurs et les évolutions du quotidien, sans jargon superflu.",
    audience: "Structures qui souhaitent déléguer une partie de leur informatique tout en gardant une relation directe.",
    icon: "headphones",
    intro:
      "L’infogérance ne se résume pas à dépanner quand tout est bloqué. Elle associe assistance, maintenance, suivi des postes et préparation des évolutions pour rendre votre informatique plus simple à vivre.",
    problems: [
      "Les utilisateurs ont besoin d’une aide accessible, pas d’une liste de tickets incompréhensibles.",
      "Les postes et logiciels doivent être suivis avant que les mises à jour ne deviennent une urgence.",
      "Les changements d’outils, de matériel ou de prestataire demandent une migration organisée.",
    ],
    services: [
      {
        title: "Assistance et accompagnement",
        description: "Un point de contact pour débloquer, expliquer et guider les utilisateurs au quotidien.",
        details: ["Assistance", "Accompagnement utilisateurs", "Conseils"],
        cta: { href: "/contact", label: "Nous contacter" },
      },
      {
        title: "Maintenance des postes",
        description: "Suivi des postes, des mises à jour et des besoins courants pour limiter les interruptions évitables.",
        details: ["Postes", "Mises à jour", "Prévention"],
        cta: { href: "/contact", label: "Demander un audit" },
      },
      {
        title: "Infogérance et migrations",
        description: "Un accompagnement durable ou ponctuel pour faire évoluer votre environnement sans tout arrêter.",
        details: ["Infogérance", "Migrations", "Mise en service"],
        cta: { href: "/contact", label: "Demander un devis" },
      },
    ],
    highlights: [
      "Des échanges en langage clair, adaptés à vos habitudes.",
      "Une prise en charge qui peut évoluer avec votre structure.",
      "Une attention portée aux utilisateurs autant qu’aux outils.",
    ],
    cta: { href: "/contact", label: "Parler d’infogérance" },
  },
];

export const SERVICE_CATEGORY_BY_SLUG = Object.fromEntries(
  SERVICE_CATEGORIES.map((category) => [category.slug, category]),
) as Record<ServiceCategory["slug"], ServiceCategory>;
