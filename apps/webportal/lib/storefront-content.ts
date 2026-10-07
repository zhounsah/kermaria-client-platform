import type { BillingV2PublicCatalog, ManagedContentKey } from "@kermaria/shared";
export type StorefrontLink = { label: string; href: string };
export const STOREFRONT_SERVICES_PROBLEM_DESTINATIONS = [
  "/services/messagerie-professionnelle",
  "/services/sauvegarde-externalisee",
  "/vpn-ou-bureau-a-distance-que-choisir",
  "/services/unifi",
  "/services/cloud-hebergement",
  "/services/support-it",
] as const;
export const STOREFRONT_SERVICES_CATEGORY_DESTINATIONS = [
  "/services/cloud-hebergement",
  "/services/domaines-messagerie",
  "/services/reseau-securite",
  "/services/support-it",
] as const;
export type StorefrontServicesProblemDestination =
  (typeof STOREFRONT_SERVICES_PROBLEM_DESTINATIONS)[number];
export type StorefrontProblemEntry = {
  title: string;
  description: string;
  href: StorefrontServicesProblemDestination;
};
export type StorefrontSection = { heading: string; bodyMarkdown: string };
export type StorefrontFaq = { question: string; answer: string };
export type StorefrontCta = { label: string; href: string };
export type StorefrontCommercialMode = "FORMULA" | "QUOTE" | "HYBRID";
export type StorefrontCommercialActions = {
  mode: StorefrontCommercialMode;
  primaryAction: StorefrontCta;
  secondaryAction: StorefrontCta | null;
  presetCode: string | null;
};
export type StorefrontPageContent = {
  seoTitle: string;
  seoDescription: string;
  title: string;
  lead: string;
  ctaLabel: string;
  ctaHref: string;
  sections: StorefrontSection[];
  faq: StorefrontFaq[];
  relatedLinks: StorefrontLink[];
};
export type StorefrontServicesLandingContent = StorefrontPageContent & {
  problemEntries: StorefrontProblemEntry[];
};

export const DEFAULT_STOREFRONT_SERVICES_PROBLEM_ENTRIES: readonly StorefrontProblemEntry[] = [
  {
    title: "Mes emails posent problème",
    description: "Spam, migration, domaine, comptes ou configuration : identifiez le bon point de départ pour remettre la messagerie au propre.",
    href: "/services/messagerie-professionnelle",
  },
  {
    title: "Je veux protéger mes données",
    description: "Sauvegarde, restauration et conservation : protégez les fichiers importants avec une stratégie adaptée à leur usage.",
    href: "/services/sauvegarde-externalisee",
  },
  {
    title: "Je dois travailler à distance",
    description: "Retrouver ses fichiers ou un bureau complet à distance demande des solutions différentes. Comparez-les avant de choisir.",
    href: "/vpn-ou-bureau-a-distance-que-choisir",
  },
  {
    title: "Mon réseau ou mon Wi-Fi fonctionne mal",
    description: "Coupures, mauvaise couverture ou lenteurs : partons de vos usages pour retrouver un réseau et un Wi-Fi fiables.",
    href: "/services/unifi",
  },
  {
    title: "J'ai un serveur, un site ou une application à maintenir",
    description: "Site, serveur ou application : identifiez ce qu'il faut entretenir, protéger et suivre dans le temps.",
    href: "/services/cloud-hebergement",
  },
  {
    title: "Je veux déléguer mon informatique",
    description: "Confiez les problèmes du quotidien à un interlocuteur qui connaît vos outils et vos priorités.",
    href: "/services/support-it",
  },
];

const LEGACY_SERVICE_PROBLEM_DESCRIPTIONS: Readonly<Record<string, string>> = {
  "/services/messagerie-professionnelle": "Spam, migration, domaine, comptes ou configuration : identifiez le bon point de départ pour remettre la messagerie au propre.",
  "/services/sauvegarde-externalisee": "Sauvegarde, restauration et conservation : protégez les fichiers importants avec une stratégie adaptée à leur usage.",
  "/vpn-ou-bureau-a-distance-que-choisir": "VPN et bureau Windows distant ne répondent pas au même besoin. Comparez les deux approches avant de choisir.",
  "/services/unifi": "Coupures, couverture, lenteurs ou segmentation : partez des usages réels pour fiabiliser le réseau et le Wi-Fi.",
  "/services/cloud-hebergement": "Hébergement, mises à jour, supervision et sauvegarde : identifiez les briques à suivre pour garder le service maintenable.",
  "/services/support-it": "Support, maintenance, supervision et coordination : confiez le quotidien IT avec un périmètre clair et adapté à votre structure.",
};

/** Projection de transition : seuls les anciens textes exacts sont remplacés.
 * Une réécriture libre effectuée dans le CMS garde toujours la priorité. */
export function presentPublicServicesLandingContent(
  content: StorefrontServicesLandingContent,
): StorefrontServicesLandingContent {
  return {
    ...content,
    title: content.title === "L'informatique dont votre activité a besoin. Gérée pour vous."
      ? "Des services pour la maison et le travail."
      : content.title,
    seoTitle: content.seoTitle === "Services IT gérés pour indépendants, associations et TPE"
      ? "Services informatiques pour particuliers et petites structures"
      : content.seoTitle,
    seoDescription: content.seoDescription === "Messagerie, sauvegarde, accès distant, réseau, hébergement et support : partez de votre problème pour identifier le service IT adapté avec Zachary IT."
      ? "Messagerie, sauvegarde, accès à distance, réseau et assistance : partez de votre besoin pour trouver une réponse claire avec Zachary IT."
      : content.seoDescription,
    sections: content.sections.map((section) => section.heading === "Des services modulaires, pas un catalogue figé"
      && section.bodyMarkdown === "Vous pouvez confier une brique précise ou un ensemble cohérent. Le périmètre est défini selon votre besoin, l'existant et les dépendances utiles ; les prestations nécessitant une étude restent traitées sur devis."
      ? DEFAULT_STOREFRONT_SERVICES_SECTIONS[0] : section),
    problemEntries: content.problemEntries.map((entry) => {
      const replacement = DEFAULT_STOREFRONT_SERVICES_PROBLEM_ENTRIES.find((item) => item.href === entry.href);
      if (!replacement || entry.description !== LEGACY_SERVICE_PROBLEM_DESCRIPTIONS[entry.href]) return entry;
      return { ...entry, title: entry.title === "Mes emails posent problème" ? replacement.title : entry.title,
        description: replacement.description };
    }),
  };
}

export const DEFAULT_STOREFRONT_SERVICES_CATEGORY_LINKS: readonly StorefrontLink[] = [
  { label: "Hébergement & services en ligne", href: "/services/cloud-hebergement" },
  { label: "Domaines & messagerie", href: "/services/domaines-messagerie" },
  { label: "Réseau & sécurité", href: "/services/reseau-securite" },
  { label: "Assistance & maintenance", href: "/services/support-it" },
];
export const DEFAULT_STOREFRONT_SERVICES_LEAD =
  "Un problème de messagerie, des données à protéger, un accès distant à organiser, un Wi-Fi instable ou un serveur à maintenir ? Partez de votre besoin : Zachary IT vous oriente vers la solution adaptée.";

export const DEFAULT_STOREFRONT_SERVICES_SEO_DESCRIPTION =
  "Messagerie, sauvegarde, accès distant, réseau, hébergement et support : partez de votre problème pour identifier le service IT adapté avec Zachary IT.";

export const DEFAULT_STOREFRONT_SERVICES_SECTIONS: readonly StorefrontSection[] = [
  {
    heading: "Une aide adaptée à votre situation",
    bodyMarkdown: "Vous pouvez nous confier un besoin précis ou plusieurs sujets liés. Nous définissons ensemble ce qui est utile ; les travaux à étudier font l'objet d'un devis.",
  },
];

export const DEFAULT_STOREFRONT_SERVICES_FAQ: readonly StorefrontFaq[] = [
  {
    question: "Puis-je choisir un seul service ?",
    answer: "Oui. Les services sont modulaires ; les dépendances éventuelles sont expliquées avant la mise en place.",
  },
  {
    question: "Tout est-il commandable en ligne ?",
    answer: "Non. Les services nécessitant une étude, une migration ou une mise en service restent traités par devis.",
  },
  {
    question: "À qui s'adressent ces services ?",
    answer: "Aux indépendants, associations, TPE et petites structures qui veulent déléguer une informatique utile et maintenable.",
  },
];

export const STOREFRONT_SERVICE_SLUGS = [
  "vps",
  "infogerance-vps",
  "hebergement-web",
  "maintenance-linux",
  "maintenance-wordpress",
  "sauvegarde-externalisee",
  "supervision-informatique",
  "supervision-nas",
  "vpn-entreprise",
  "bureau-windows-distance",
  "unifi",
  "firewall",
  "cloudflare-waf",
  "gestion-dns-domaines",
  "messagerie-professionnelle",
] as const;
export type StorefrontServiceSlug = (typeof STOREFRONT_SERVICE_SLUGS)[number];

const LEGACY_WEB_HOSTING_COPY = {
  lead: "Un site public dépend de son hébergement, de ses mises à jour, de ses accès et de ses sauvegardes. Zachary IT aide à organiser ces éléments sans masquer les limites du CMS existant.",
  sectionHeading: "CMS, sauvegarde et sécurité",
  sectionBody: "La maintenance peut couvrir un CMS, ses extensions et les correctifs nécessaires. Une protection web ou une sauvegarde sont des briques séparées, choisies selon le risque et le contenu à protéger.",
  faqQuestion: "Puis-je garder mon CMS actuel ?",
  publicLead: "Un site public dépend de son hébergement, de ses mises à jour, de ses accès et de ses sauvegardes. Zachary IT aide à organiser ces éléments et à identifier ce qui doit être amélioré avant intervention.",
  publicSectionHeading: "Mises à jour, sauvegarde et sécurité",
  publicSectionBody: "La maintenance peut couvrir l'outil qui fait fonctionner votre site, ses extensions et les correctifs nécessaires. Une protection web ou une sauvegarde sont des services distincts, choisis selon votre besoin.",
  publicFaqQuestion: "Puis-je garder mon outil actuel de gestion de site ?",
} as const;

/**
 * Présentation de compatibilité pour les anciennes valeurs publiques connues.
 * La projection ne réécrit jamais des fragments de contenu libre : une valeur
 * CMS est conservée telle quelle lorsqu'elle ne correspond pas exactement à
 * une formulation historique recensée.
 */
export function presentPublicStorefrontContent(
  content: StorefrontPageContent,
  serviceSlug: StorefrontServiceSlug | null,
): StorefrontPageContent {
  const normalizedContent: StorefrontPageContent = {
    ...content,
    seoTitle: normalizeLegacyPublicText(content.seoTitle),
    seoDescription: normalizeLegacyPublicText(content.seoDescription),
    title: normalizeLegacyPublicText(content.title),
    lead: normalizeLegacyPublicText(content.lead),
    ctaLabel: normalizeLegacyPublicText(content.ctaLabel),
    sections: content.sections.map((section) => ({
      heading: normalizeLegacyPublicText(section.heading),
      bodyMarkdown: normalizeLegacyPublicText(section.bodyMarkdown),
    })),
    faq: content.faq.map((item) => ({
      question: normalizeLegacyPublicText(item.question),
      answer: normalizeLegacyPublicText(item.answer),
    })),
    relatedLinks: content.relatedLinks.map((link) => ({
      ...link,
      label: normalizeLegacyPublicText(link.label),
    })),
  };

  if (serviceSlug !== "hebergement-web") return normalizedContent;

  return {
    ...normalizedContent,
    lead: content.lead === LEGACY_WEB_HOSTING_COPY.lead
      ? LEGACY_WEB_HOSTING_COPY.publicLead
      : normalizedContent.lead,
    sections: normalizedContent.sections.map((section) => (
      section.heading === LEGACY_WEB_HOSTING_COPY.sectionHeading
        && section.bodyMarkdown === LEGACY_WEB_HOSTING_COPY.sectionBody
        ? {
            ...section,
            heading: LEGACY_WEB_HOSTING_COPY.publicSectionHeading,
            bodyMarkdown: LEGACY_WEB_HOSTING_COPY.publicSectionBody,
          }
        : section
    )),
    faq: normalizedContent.faq.map((item) => (
      item.question === LEGACY_WEB_HOSTING_COPY.faqQuestion
        ? { ...item, question: LEGACY_WEB_HOSTING_COPY.publicFaqQuestion }
        : item
    )),
  };
}

/**
 * Compatibilité de rendu pour les contenus CMS créés avant les chantiers V3.
 * Les données persistées restent inchangées. Ne sont normalisées que des
 * valeurs complètes identifiées : un véritable audit décrit dans le contenu
 * libre reste donc un audit.
 */
const LEGACY_PUBLIC_TEXT_EQUIVALENTS: Readonly<Record<string, string>> = {
  "Cloud & Hébergement": "Hébergement & services en ligne",
  "VPS et hébergement": "Serveurs et hébergement",
  "Un VPS Zachary IT ou un VPS Cloud peut être préparé et géré ; les caractéristiques CPU, RAM et stockage sont celles affichées sur chaque offre. Lorsque le parcours le permet, la commande peut être payée en ligne, puis la mise en service intervient après validation technique.": "Nous préparons et suivons le serveur adapté à votre site ou à vos outils. Les capacités et le prix sont précisés dans chaque offre ; la mise en service suit la validation de votre demande.",
  "Mises à jour Linux ou CMS, copie séparée, tests de restauration et alertes utiles complètent l’hébergement. Une sauvegarde sans suivi ne remplace pas la supervision.": "Les mises à jour, une copie séparée de vos fichiers, des essais de récupération et un suivi régulier complètent l'hébergement.",
  "VPS": "Serveur à distance",
  "VPN entreprise": "Accès privé à distance",
  "UniFi": "Réseau et Wi-Fi",
  "Firewall": "Protection du réseau",
  "Cloudflare WAF": "Protection de site web",
  "Domaines & Messagerie": "Domaines & messagerie",
  "Réseau & Sécurité": "Réseau & sécurité",
  "Support & IT": "Assistance & maintenance",
  "lorsquÔÇÖelles sÔÇÖappliquent": "lorsqu’elles s’appliquent",
  "Demander un diagnostic": "Faire le diagnostic",
  "VPS géré pour votre activité": "Un serveur à distance suivi pour votre activité",
  "Infogérance VPS : qui gère votre serveur au quotidien ?": "Qui s'occupe de votre serveur au quotidien ?",
  "Maintenance Linux pour ne pas laisser un serveur s’user en silence": "Entretenir votre serveur pour éviter les mauvaises surprises",
  "Maintenance WordPress : garder votre site à jour sans improviser": "Gardez votre site WordPress à jour et protégé",
  "Sauvegarde externalisée : une copie séparée, utile le jour où il faut restaurer": "Une copie séparée, utile quand il faut récupérer vos fichiers",
  "Supervision informatique : voir les signaux utiles avant qu’ils bloquent l’activité": "Repérer les problèmes avant qu'ils bloquent votre activité",
  "Supervision NAS : ne pas découvrir un problème de stockage trop tard": "Ne découvrez pas un problème de stockage trop tard",
  "Accès VPN sécurisé pour votre entreprise": "Travaillez à distance avec un accès protégé",
  "Réseau UniFi : Wi-Fi, switching et évolution de votre installation": "Un Wi-Fi fiable et adapté à vos locaux",
  "Firewall : maîtriser les accès sans bloquer le travail": "Protégez vos accès sans bloquer le travail",
  "Cloudflare WAF : protéger un service web exposé sans promettre l’impossible": "Protégez votre site web avec des règles adaptées",
  "Gestion DNS et domaines : garder la maîtrise de votre identité en ligne": "Gardez la maîtrise de votre nom de domaine",
  "VPS géré : hébergement et administration": "Serveur à distance et accompagnement",
  "Infogérance VPS : maintenance de serveur existant": "Entretien et suivi de votre serveur",
  "Maintenance Linux : mises à jour et suivi de serveur": "Entretien et mises à jour de serveur",
  "Maintenance WordPress : mises à jour, sauvegarde et sécurité": "Entretien de site WordPress",
  "Sauvegarde externalisée : copie séparée et restauration": "Copie de sécurité de vos fichiers",
  "Supervision informatique : alertes et suivi des services": "Suivi de vos services informatiques",
  "Supervision NAS : suivi du stockage et des sauvegardes": "Suivi de votre stockage et de vos sauvegardes",
  "VPN entreprise : accès distant sécurisé": "Accès sécurisé à distance pour votre équipe",
  "Réseau UniFi : Wi-Fi, switching et supervision": "Réseau et Wi-Fi fiables",
  "Firewall entreprise : règles réseau et accès maîtrisés": "Protection de votre réseau",
  "Cloudflare WAF : protection de site web gérée": "Protection de site web",
  "Gestion DNS et domaines professionnels": "Gestion de votre nom de domaine",
  "Messagerie professionnelle : boîtes mail, migration et DNS": "Messagerie professionnelle",
  "Accédez à vos fichiers, applications et équipements depuis l’extérieur sans les exposer directement sur Internet avec un VPN d’entreprise cadré.": "Travaillez à distance et retrouvez vos fichiers avec un accès réservé aux personnes autorisées, adapté à votre activité.",
  "Accédez à vos fichiers, applications et équipements depuis l’extérieur sans les exposer directement sur Internet. Zachary IT configure un VPN adapté aux utilisateurs et aux ressources concernées.": "Retrouvez vos fichiers et outils de travail depuis l'extérieur grâce à un accès réservé aux personnes autorisées. Zachary IT adapte la solution à vos usages.",
  "À quoi sert un VPN ?": "À quoi sert un accès protégé ?",
  "Un VPN crée un accès privé entre un appareil autorisé et le réseau ou service visé. Il convient pour rejoindre des ressources internes, sans transformer chaque usage distant en accès public.": "Il permet à une personne autorisée de retrouver les fichiers ou outils prévus pour elle, sans les ouvrir à tout Internet.",
  "VPN ou bureau Windows distant ?": "Accéder à ses fichiers ou à un bureau complet ?",
  "Le VPN donne accès à un réseau ou à des ressources autorisées ; un bureau Windows distant fournit un environnement Windows exécuté à distance. Le bon choix dépend des applications, données et postes utilisés.": "La première solution donne accès aux fichiers et outils autorisés ; la seconde ouvre un bureau Windows complet à distance. Le bon choix dépend de vos logiciels, de vos données et de vos habitudes.",
  "Le VPN donne-t-il accès à tout le réseau ?": "Puis-je accéder à tous les fichiers ?",
  "Non, les droits et segments accessibles sont définis selon les utilisateurs.": "Non. Chaque personne ne reçoit que les accès prévus pour elle.",
  "Un VPN rend-il tout sécurisé ?": "Cet accès suffit-il à tout protéger ?",
  "Il réduit l’exposition de certains accès mais doit être associé à des comptes, règles et mises à jour adaptés.": "Non. Les comptes, les droits et les mises à jour doivent aussi être suivis.",
  "Zachary IT peut reprendre l’administration d’un VPS déjà hébergé chez un fournisseur tiers, après vérification des accès, de l’état du système et des dépendances.": "Nous pouvons reprendre le suivi d'un serveur déjà hébergé chez un autre fournisseur, après avoir vérifié les accès et son état actuel.",
  "Une synchronisation propage aussi une suppression ou une erreur. Une sauvegarde conserve une copie distincte et doit être pensée avec la restauration et la supervision.": "Une copie distincte de vos fichiers aide à les retrouver après une suppression, une erreur ou une panne. Nous prévoyons aussi comment les récupérer.",
  "Zachary IT conçoit, reprend ou fait évoluer une installation UniFi en tenant compte des locaux, utilisateurs, équipements et usages critiques.": "Nous créons ou améliorons votre réseau et votre Wi-Fi selon vos locaux, vos appareils et vos habitudes de travail.",
  "Zachary IT accompagne la mise en place, la migration et l’administration technique d’une messagerie professionnelle, avec une distinction claire entre service, licences et configuration DNS.": "Nous vous aidons à créer ou reprendre votre messagerie professionnelle, tout en expliquant clairement les services et licences nécessaires.",
  "La supervision suit des services, équipements ou sauvegardes selon un périmètre défini. Elle transforme des signaux techniques en alertes exploitables, sans remplacer une intervention humaine.": "Nous suivons les points importants de vos outils et de vos sauvegardes pour repérer les problèmes et organiser une intervention si nécessaire.",
  "Un NAS peut concentrer des fichiers essentiels et des sauvegardes. Zachary IT aide à suivre les alertes utiles, l’espace, les services exposés et les copies prévues.": "Votre boîtier de stockage peut contenir des fichiers essentiels. Nous suivons son fonctionnement, l'espace disponible et les copies prévues.",
  "Un serveur virtuel peut héberger un site, une application ou un service métier. Zachary IT distingue le VPS fourni, le VPS Cloud et l’infogérance d’un serveur existant.": "Un serveur à distance peut faire fonctionner votre site ou vos outils de travail. Nous vous aidons à choisir une solution neuve ou à faire suivre votre serveur actuel.",
  "Sauvegarde, synchronisation et restauration": "Protéger vos fichiers et pouvoir les récupérer",
  "Pourquoi la supervision compte": "Pourquoi le suivi compte",
  "Wi-Fi, switching et segmentation": "Un Wi-Fi adapté à vos locaux et usages",
  "Maintenance et supervision": "Un réseau entretenu et suivi",
  "SPF, DKIM et DMARC": "Protéger l'envoi de vos e-mails",
  "Ce qui peut être supervisé": "Ce qui peut être suivi",
  "Suivre le NAS et ses signaux": "Suivre votre stockage",
  "NAS, sauvegarde et accès distant": "Fichiers, copies et accès à distance",
  "VPS Zachary IT, VPS Cloud et infogérance": "Choisir et faire suivre votre serveur",
  "Ce qui est cadré avant mise en service": "Ce que nous vérifions avant de commencer",
  "Tarifs des services IT : unités et devis": "Tarifs clairs : prix affichés et devis",
  "Comprenez les unités de facturation des services Zachary IT : domaine, utilisateur, site, serveur ou instance. Les prestations étudiées restent sur devis.": "Comprenez les prix des services Zachary IT et les situations qui demandent un devis personnalisé.",
  "Des tarifs lisibles, sans faire croire qu’un service sur mesure est instantané.": "Des tarifs clairs et une réponse adaptée à votre besoin.",
  "Les prix affichés correspondent aux services proposés. Les services de mise en place, migration, réseau ou infogérance restent qualifiés avant devis.": "Les prix affichés décrivent les services proposés. Pour une installation ou une reprise de l'existant, nous vérifions d'abord votre besoin avant de préparer un devis.",
  "Comment lire les unités": "Comment lire les prix affichés",
  "Selon le service, la facturation peut être exprimée par domaine, utilisateur, site, serveur ou instance et par mois. Le montant affiché correspond au service et à son unité de facturation.": "Chaque prix précise ce qui est facturé et pour quelle période : par personne, par site ou par service. Le montant exact est confirmé avant votre commande.",
  "Un domaine géré, une migration de messagerie, un réseau UniFi, une sécurité firewall, une reprise de serveur ou une infogérance nécessitent de confirmer le périmètre, les accès et les frais de fournisseurs éventuels.": "Pour reprendre une messagerie, améliorer un réseau ou gérer un serveur existant, nous vérifions les accès et les travaux nécessaires avant de proposer un devis. Les frais d'autres fournisseurs sont indiqués séparément.",
  "Le VPS Zachary IT et le VPS Cloud ne recouvrent pas la même mise en œuvre. Les caractéristiques CPU, RAM et stockage sont celles affichées sur chaque offre. Lorsque le parcours le permet, la commande peut être payée en ligne, puis la mise en service intervient après validation technique. L’infogérance peut aussi porter sur un VPS chez un autre fournisseur.": "Nos offres de serveur à distance ne couvrent pas toutes les mêmes besoins. Les capacités sont indiquées pour chaque offre ; la mise en service est organisée après vérification. Nous pouvons aussi suivre un serveur déjà hébergé ailleurs.",
  "Usages, données, accès, sauvegarde, maintenance, dépendances et responsabilité du fournisseur sont qualifiés avant la mise en service. L’infogérance peut aussi porter sur un VPS chez un autre fournisseur.": "Avant de commencer, nous vérifions vos usages, vos fichiers, les accès nécessaires, les copies de sécurité et le rôle de votre fournisseur. Nous pouvons aussi suivre un serveur déjà hébergé ailleurs.",
  "Oui, l’infogérance VPS peut être étudiée sur un serveur existant.": "Oui. Nous pouvons étudier le suivi de votre serveur actuel après avoir vérifié ses accès et son état.",
  "Le VPS Cloud est-il créé instantanément ?": "Le serveur est-il prêt immédiatement ?",
  "Mises à jour, surveillance des erreurs, sauvegarde, accès d’administration et documentation sont organisés selon le périmètre. L’infogérance ne supprime pas les obligations du fournisseur d’hébergement.": "Nous organisons les mises à jour, le suivi des erreurs, les copies de sécurité et les accès selon votre besoin. Votre hébergeur conserve ses propres responsabilités.",
  "La maintenance peut couvrir le système qui fait fonctionner votre site (CMS), ses extensions et les correctifs nécessaires. Une protection web ou une sauvegarde sont des services distincts, choisis selon le risque et le contenu à protéger.": "Nous pouvons entretenir l'outil qui fait fonctionner votre site, ses extensions et ses mises à jour. La protection du site et la copie de ses données sont choisies selon votre besoin.",
  "Puis-je garder mon système de gestion de site (CMS) actuel ?": "Puis-je garder l'outil actuel de mon site ?",
  "Une copie restaurable est distincte de la synchronisation de fichiers. La protection WAF ou les réglages DNS peuvent compléter la maintenance, mais ne garantissent pas l’absence de vulnérabilité.": "Une copie de sécurité doit pouvoir être récupérée. Des règles de protection et des réglages du nom de domaine peuvent compléter l'entretien du site, sans garantir qu'aucun problème ne surviendra.",
  "Serveurs, sites, sauvegardes, NAS, réseau ou services hébergés peuvent être inclus selon les accès et les besoins. Les seuils et destinataires des alertes sont définis avec vous.": "Nous pouvons suivre vos serveurs, sites, copies de sécurité, boîtiers de stockage et réseau selon vos besoins. Nous définissons avec vous quelles alertes recevoir et à qui les transmettre.",
  "La supervision répare-t-elle automatiquement ?": "Le suivi corrige-t-il les problèmes tout seul ?",
  "Puis-je superviser un NAS ?": "Pouvez-vous suivre mon boîtier de stockage ?",
  "La supervision dépend du modèle, des accès et des services activés. Elle peut couvrir la disponibilité, l’état de stockage ou les sauvegardes, sans se substituer au constructeur ni à une stratégie de restauration.": "Le suivi dépend de votre équipement et des accès disponibles. Il peut signaler une panne, un manque de place ou un problème de copie, mais ne remplace pas la possibilité de récupérer vos fichiers.",
  "Un NAS exposé sans contrôle crée un risque. Les accès distants, VPN, droits utilisateurs et copie externe sont étudiés ensemble lorsque les données sont sensibles.": "Un boîtier de stockage accessible sans protection présente des risques. Nous vérifions ensemble les accès à distance, les droits de chacun et les copies conservées ailleurs.",
  "Un NAS est-il une sauvegarde à lui seul ?": "Mon boîtier de stockage suffit-il à protéger mes fichiers ?",
  "Pouvez-vous surveiller un NAS existant ?": "Pouvez-vous suivre mon boîtier actuel ?",
  "Faut-il ouvrir le NAS sur Internet ?": "Faut-il rendre mon boîtier accessible sur Internet ?",
  "Pas par défaut ; un accès VPN ou une solution adaptée est étudié selon l’usage.": "Pas nécessairement. Nous choisissons un accès protégé selon votre besoin.",
  "Un bureau Windows distant permet d’utiliser un environnement Windows hébergé depuis un appareil autorisé. Il ne doit pas être confondu avec un simple VPN.": "Un bureau Windows à distance vous permet de retrouver vos logiciels depuis un appareil autorisé. Il répond à un autre besoin qu'un accès limité à vos fichiers.",
  "Bureau distant ou VPN": "Un bureau complet ou seulement vos fichiers ?",
  "Le VPN rejoint des ressources internes ; le bureau Windows distant exécute les applications dans un environnement Windows à distance. Zachary IT vous aide à choisir selon les logiciels, utilisateurs et données.": "Un accès privé permet de retrouver les fichiers et outils autorisés ; le bureau à distance donne accès à un environnement Windows complet. Nous vous aidons à choisir selon vos logiciels et vos habitudes.",
  "Pouvez-vous reprendre une installation UniFi existante ?": "Pouvez-vous améliorer mon réseau actuel ?",
  "Un firewall filtre les flux entre réseaux et vers Internet. Zachary IT définit des règles lisibles, liées aux usages et documentées pour faciliter la maintenance.": "La protection du réseau contrôle les échanges entre vos équipements et Internet. Nous définissons des règles adaptées à vos usages et les documentons pour pouvoir les suivre.",
  "Les ouvertures, VPN, postes, équipements et applications sont recensés avant configuration. Les exceptions sont limitées et expliquées ; une règle permissive n’est pas une solution durable.": "Avant de régler la protection, nous recensons les appareils, les applications et les accès à distance utiles. Les exceptions sont limitées et expliquées.",
  "Les mises à jour, accès d’administration et évolutions sont suivis selon le périmètre. Un firewall ne protège pas seul contre tous les risques et ne remplace ni les sauvegardes ni les mises à jour.": "Nous suivons les mises à jour et les accès de gestion selon ce qui est prévu. Cette protection ne remplace ni les copies de sécurité ni l'entretien des appareils.",
  "Pouvez-vous remplacer un firewall existant ?": "Pouvez-vous remplacer ma protection réseau actuelle ?",
  "Pas systématiquement ; un VPN ou une autre architecture peut éviter une exposition directe.": "Pas nécessairement. Un accès privé peut éviter d'ouvrir directement certains services sur Internet.",
  "Le firewall protège-t-il contre toutes les attaques ?": "Cette protection bloque-t-elle tous les risques ?",
  "Un WAF aide à filtrer certains trafics malveillants vers un site ou une application. Zachary IT configure les règles, DNS et accès nécessaires selon les usages légitimes.": "Une protection de site peut filtrer certains accès malveillants. Nous réglons les protections et le nom de domaine sans bloquer les visiteurs attendus.",
  "DNS, WAF et règles de protection": "Nom de domaine et règles de protection",
  "La mise en place commence par le domaine, les enregistrements DNS, l’origine du site et les flux attendus. Les règles sont ajustées pour limiter les faux positifs et rester compréhensibles.": "Nous vérifions d'abord le nom de domaine, l'hébergement du site et les visiteurs attendus. Les règles sont ajustées pour éviter de bloquer des usages légitimes.",
  "Ce que le WAF ne remplace pas": "Ce que cette protection ne remplace pas",
  "Un WAF ne corrige pas un CMS non maintenu, une application vulnérable ou des accès administrateurs faibles. Il complète la maintenance, les mises à jour et les sauvegardes.": "Cette protection ne corrige pas un site non entretenu ou des accès administrateur trop faibles. Elle complète les mises à jour et les copies de sécurité.",
  "Cloudflare WAF empêche-t-il toutes les attaques ?": "Cette protection empêche-t-elle toutes les attaques ?",
  "Pouvez-vous gérer le DNS ?": "Pouvez-vous gérer mon nom de domaine ?",
  "Oui, dans le cadre d’une gestion de domaine et de DNS cadrée.": "Oui, si la gestion du nom de domaine et de ses réglages fait partie de la prestation prévue.",
  "Un nom de domaine, ses accès et sa zone DNS doivent rester identifiés, documentés et liés aux bons services. Zachary IT organise cette gestion sur devis.": "Le nom de domaine et ses réglages doivent rester connus, documentés et liés aux bons services. Nous pouvons organiser leur suivi sur devis.",
  "Domaine, zone DNS et responsabilités": "Nom de domaine et responsabilités",
  "Pouvez-vous corriger une zone DNS existante ?": "Pouvez-vous corriger les réglages de mon domaine ?",
  "Les réglages DNS peuvent servir la messagerie ; les boîtes et licences sont un périmètre distinct.": "Les réglages du domaine peuvent aider votre messagerie. Les boîtes et les licences sont prévues séparément.",
  "Ces enregistrements DNS aident à authentifier les messages et à réduire l’usurpation. Ils ne garantissent pas que chaque message sera délivré et nécessitent d’être alignés avec les services réellement émetteurs.": "Certains réglages du domaine permettent de vérifier l'origine des e-mails et de limiter l'usurpation. Ils ne garantissent pas la réception de chaque message.",
  "Les réglages DNS, la réputation et le contenu peuvent jouer ; le diagnostic permet de vérifier SPF, DKIM et DMARC.": "Les réglages du domaine, la réputation de l'adresse et le contenu du message peuvent jouer. Un diagnostic permet de trouver la cause probable.",
  "Choisissez un VPS Zachary IT, un VPS Cloud ou une infogérance VPS. Zachary IT cadre l’hébergement, la maintenance, la sauvegarde et le suivi.": "Choisissez un serveur à distance adapté à votre activité, avec les mises à jour, les copies de sécurité et le suivi nécessaires.",
  "Confiez la maintenance et le suivi de votre VPS existant à Zachary IT, y compris chez un fournisseur tiers après cadrage.": "Confiez-nous l'entretien et le suivi de votre serveur actuel, même s'il est hébergé chez un autre fournisseur.",
  "Hébergement web, mises à jour et suivi de site : Zachary IT prépare un service maintenable en tenant compte de votre site et de son système de gestion (CMS).": "Hébergement, mises à jour et suivi de votre site : une solution adaptée à l'outil qui le fait fonctionner.",
  "Protégez vos données avec une sauvegarde externalisée, une copie séparée, des restaurations préparées et une supervision adaptée.": "Protégez vos fichiers avec une copie conservée ailleurs, une récupération préparée et un suivi adapté.",
  "Zachary IT met en place une supervision informatique pour suivre les services, détecter les alertes utiles et organiser les premières actions.": "Zachary IT suit vos services informatiques pour repérer les problèmes et organiser les premières actions.",
  "Zachary IT supervise votre NAS pour suivre les alertes de stockage, la disponibilité et les sauvegardes prévues.": "Zachary IT suit votre boîtier de stockage, son espace disponible et les copies de sécurité prévues.",
  "Conception, évolution et maintenance de réseau UniFi : Wi-Fi, switching, segmentation, supervision et documentation utiles.": "Création, amélioration et entretien de votre réseau et de votre Wi-Fi, avec un suivi adapté à vos locaux.",
  "Zachary IT configure et maintient un firewall pour maîtriser les accès réseau, les services exposés et les règles utiles à votre activité.": "Zachary IT règle et entretient la protection de votre réseau pour garder les accès utiles à votre activité.",
  "Protégez un site ou service web exposé avec Cloudflare WAF, DNS, règles adaptées et suivi des changements par Zachary IT.": "Protégez votre site web avec des règles adaptées, un nom de domaine bien réglé et un suivi des changements.",
  "Zachary IT gère domaines et DNS : accès, zones, transferts, messagerie et réglages utiles à votre présence en ligne.": "Zachary IT suit votre nom de domaine, ses accès et les réglages nécessaires à votre site et à vos e-mails.",
};

function normalizeLegacyPublicText(value: string): string {
  return LEGACY_PUBLIC_TEXT_EQUIVALENTS[value] ?? value;
}

export const STOREFRONT_PRIORITY_SERVICE_SLUGS = [
  "messagerie-professionnelle",
  "vpn-entreprise",
  "sauvegarde-externalisee",
  "unifi",
  "infogerance-vps",
  "hebergement-web",
] as const satisfies readonly StorefrontServiceSlug[];
export type StorefrontPriorityServiceSlug =
  (typeof STOREFRONT_PRIORITY_SERVICE_SLUGS)[number];
export function isStorefrontPriorityServiceSlug(
  slug: StorefrontServiceSlug,
): slug is StorefrontPriorityServiceSlug {
  return STOREFRONT_PRIORITY_SERVICE_SLUGS.includes(
    slug as StorefrontPriorityServiceSlug,
  );
}
export type StorefrontBreadcrumbItem = { name: string; path: string };

const STOREFRONT_CATEGORY_BREADCRUMB_LABELS = {
  "cloud-hebergement": "H\u00e9bergement & services en ligne",
  "domaines-messagerie": "Nom de domaine & messagerie",
  "reseau-securite": "R\u00e9seau & S\u00e9curit\u00e9",
  "support-it": "Assistance & maintenance",
} as const;

const STOREFRONT_SERVICE_BREADCRUMB_LABELS: Record<StorefrontServiceSlug, string> = {
  "vps": "Serveur à distance",
  "infogerance-vps": "Gestion de votre serveur",
  "hebergement-web": "H\u00e9bergement web",
  "maintenance-linux": "Entretien de serveur",
  "maintenance-wordpress": "Maintenance WordPress",
  "sauvegarde-externalisee": "Copie de sécurité de vos fichiers",
  "supervision-informatique": "Surveillance de vos services",
  "supervision-nas": "Suivi de votre stockage",
  "vpn-entreprise": "Accès sécurisé à distance",
  "bureau-windows-distance": "Bureau Windows \u00e0 distance",
  "unifi": "Réseau et Wi-Fi",
  "firewall": "Protection du réseau",
  "cloudflare-waf": "Protection de site web",
  "gestion-dns-domaines": "Gestion du nom de domaine",
  "messagerie-professionnelle": "Messagerie professionnelle",
};

export function resolveStorefrontBreadcrumb(
  pathname: string,
): StorefrontBreadcrumbItem[] | null {
  if (pathname === "/services") return [{ name: "Services", path: "/services" }];
  if (pathname === "/tarifs") return [{ name: "Tarifs", path: "/tarifs" }];
  if (!pathname.startsWith("/services/")) return null;

  const slug = pathname.slice("/services/".length);
  const categoryLabel = STOREFRONT_CATEGORY_BREADCRUMB_LABELS[
    slug as keyof typeof STOREFRONT_CATEGORY_BREADCRUMB_LABELS
  ];
  if (categoryLabel) {
    return [
      { name: "Services", path: "/services" },
      { name: categoryLabel, path: `/services/${slug}` },
    ];
  }

  if (!STOREFRONT_SERVICE_SLUGS.includes(slug as StorefrontServiceSlug)) return null;
  return [
    { name: "Services", path: "/services" },
    {
      name: STOREFRONT_SERVICE_BREADCRUMB_LABELS[slug as StorefrontServiceSlug],
      path: `/services/${slug}`,
    },
  ];
}

// Mapping fermé et non administrable : le CMS ne choisit jamais le serviceCode.
// Une page qui agrège plusieurs services n'est self-service que si tous les
// services Billing visibles qui la composent sont explicitement commandables.
const STOREFRONT_SERVICE_BILLING_CODES: Record<StorefrontServiceSlug, readonly string[]> = {
  "vps": ["VPS-LOCAL", "VPS-CLOUD", "VPS-EXTERNAL-MANAGED", "VPS-MANAGED-ADDON"],
  "infogerance-vps": ["VPS-EXTERNAL-MANAGED", "VPS-MANAGED-ADDON"],
  "hebergement-web": ["WEB-EXTERNAL-MANAGED"],
  "maintenance-linux": ["LINUX-PATCH-MANAGED"],
  "maintenance-wordpress": ["CMS-MAINT"],
  "sauvegarde-externalisee": ["BACKUP-EXTERNAL-MANAGED"],
  "supervision-informatique": ["MONITORING-EXTERNAL"],
  "supervision-nas": ["NAS-MONITORING"],
  "vpn-entreprise": ["VPN-ACCESS"],
  "bureau-windows-distance": ["RDS-ACCESS"],
  "unifi": ["UNIFI-MANAGED"],
  "firewall": ["FIREWALL-MANAGED"],
  "cloudflare-waf": ["WAF-REVERSE-PROXY"],
  "gestion-dns-domaines": ["DOMAIN-MANAGED", "DNS-MANAGED"],
  "messagerie-professionnelle": ["MAIL-MANAGED", "MAIL-DMARC-MANAGED", "M365-MANAGED"],
};

/**
 * Retrouve une page de service existante depuis son identifiant Billing.
 * Le catalogue commercial reutilise ce raccordement deja employe par les
 * pages services ; il ne maintient donc pas une seconde liste de routes.
 */
export function storefrontServiceUrlForBillingCode(serviceCode: string): string | null {
  const entry = (Object.entries(STOREFRONT_SERVICE_BILLING_CODES) as Array<
    [StorefrontServiceSlug, readonly string[]]
  >).find(([, serviceCodes]) => serviceCodes.includes(serviceCode));

  return entry ? `/services/${entry[0]}` : null;
}

type StorefrontCommercialRouteDefinition = {
  mode: Exclude<StorefrontCommercialMode, "QUOTE">;
  presetCode: string;
  formulaLabel: string;
  secondaryLabel: string;
  requiredPresetServiceCodes: readonly string[];
};

// Mapping commercial ferme et distinct du mapping technique SEO -> Billing.
// Toute page non declaree ici reste sur devis par defaut.
const STOREFRONT_COMMERCIAL_ROUTES: Readonly<Partial<Record<
  StorefrontServiceSlug,
  StorefrontCommercialRouteDefinition
>>> = {
  "sauvegarde-externalisee": {
    mode: "HYBRID",
    presetCode: "pack-dossier-securise",
    formulaLabel: "Prot\u00e9ger mes fichiers avec une offre",
    secondaryLabel: "Protéger un serveur ou un boîtier de stockage",
    requiredPresetServiceCodes: ["BACKUP-PERSONAL"],
  },
  "vpn-entreprise": {
    mode: "FORMULA",
    presetCode: "pack-acces-distance",
    formulaLabel: "Configurer mon acc\u00e8s \u00e0 distance",
    secondaryLabel: "J'ai un besoin sp\u00e9cifique",
    requiredPresetServiceCodes: ["VPN-ACCESS"],
  },
  "bureau-windows-distance": {
    mode: "FORMULA",
    presetCode: "pack-bureau-windows-distance",
    formulaLabel: "Configurer mon bureau \u00e0 distance",
    secondaryLabel: "Demander un conseil",
    requiredPresetServiceCodes: ["RDS-ACCESS"],
  },
};

const STOREFRONT_TARIFF_PRESET_BY_SERVICE_CODE: Readonly<Record<string, string>> = {
  "VPN-ACCESS": "pack-acces-distance",
  "RDS-ACCESS": "pack-bureau-windows-distance",
};
const STOREFRONT_DIRECT_TARIFF_SERVICE_CODES = new Set([
  "VPS-LOCAL",
  "VPS-CLOUD",
]);
const SELF_SERVICE_LABEL_PATTERN = /\b(command(?:er|ez|e|es)?|achet(?:er|ez|e|es)?|achat|configur(?:er|ez|e|es|ation)?)\b/i;
const GENERIC_AUDIT_LABEL_PATTERN = /\baudit\b/i;
export function storefrontContentKeyForServiceSlug(
  slug: StorefrontServiceSlug,
): ManagedContentKey {
  return `storefront:${slug}` as ManagedContentKey;
}
export function storefrontServiceSlugForContentKey(
  key: ManagedContentKey,
): StorefrontServiceSlug | null {
  if (!key.startsWith("storefront:")) return null;
  const slug = key.slice("storefront:".length);
  return STOREFRONT_SERVICE_SLUGS.includes(slug as StorefrontServiceSlug)
    ? slug as StorefrontServiceSlug
    : null;
}
export function storefrontServiceSelfServiceOrderable(
  slug: StorefrontServiceSlug,
  catalog: BillingV2PublicCatalog,
): boolean {
  const requiredCodes = STOREFRONT_SERVICE_BILLING_CODES[slug];
  const linkedServices = requiredCodes.map((code) =>
    catalog.services.find((service) => service.code === code) ?? null,
  );
  // Fail closed si le catalogue est indisponible, incomplet ou si un service
  // lié n'est pas public. Le CMS ne peut donc jamais réactiver le self-service.
  return linkedServices.length > 0
    && linkedServices.every((service) =>
      service !== null
      && service.publicVisible === true
      && service.selfServiceOrderable === true,
    );
}
export function resolveStorefrontCommercialActions(
  slug: StorefrontServiceSlug,
  catalog: BillingV2PublicCatalog,
  content: Pick<StorefrontPageContent, "ctaLabel" | "ctaHref">,
): StorefrontCommercialActions {
  const route = STOREFRONT_COMMERCIAL_ROUTES[slug];
  const quoteAction = resolveStorefrontPublicCta(content, false);

  if (!route) {
    return { mode: "QUOTE", primaryAction: quoteAction, secondaryAction: null, presetCode: null };
  }

  const preset = catalog.presets.find((item) => item.code === route.presetCode) ?? null;
  const presetContainsRequiredServices = preset !== null
    && route.requiredPresetServiceCodes.every((serviceCode) =>
      preset.items.some((item) => item.serviceCode === serviceCode),
    );

  if (!presetContainsRequiredServices) {
    return { mode: "QUOTE", primaryAction: quoteAction, secondaryAction: null, presetCode: null };
  }

  return {
    mode: route.mode,
    primaryAction: {
      label: route.formulaLabel,
      href: `/formules/${encodeURIComponent(route.presetCode)}`,
    },
    secondaryAction: { label: route.secondaryLabel, href: quoteAction.href },
    presetCode: route.presetCode,
  };
}

export function resolveStorefrontTariffAction(
  serviceCode: string,
  catalog: BillingV2PublicCatalog,
): StorefrontCta {
  const presetCode = STOREFRONT_TARIFF_PRESET_BY_SERVICE_CODE[serviceCode];
  const preset = presetCode
    ? catalog.presets.find((item) => item.code === presetCode) ?? null
    : null;

  if (preset?.items.some((item) => item.serviceCode === serviceCode)) {
    return {
      label: "Voir l'offre",
      href: `/formules/${encodeURIComponent(preset.code)}`,
    };
  }

  return { label: "Demander un devis", href: "/contact" };
}

/**
 * Destination individuelle réellement disponible. Cette liste est un registre
 * de routes de la vitrine, pas une seconde donnée commerciale : le mode
 * `direct` reste administré dans Billing V2 et l'API le refuse pour tout
 * service qui n'a pas ce parcours.
 */
export function resolveStorefrontDirectTariffAction(
  serviceCode: string,
  selfServiceOrderable: boolean,
  tiers: readonly { code: string; monthlyAmountCents: number; publicSelectable: boolean }[],
): StorefrontCta | null {
  if (
    !selfServiceOrderable ||
    !STOREFRONT_DIRECT_TARIFF_SERVICE_CODES.has(serviceCode)
  ) {
    return null;
  }

  const tier = tiers
    .filter((candidate) => candidate.publicSelectable && candidate.monthlyAmountCents > 0)
    .sort((left, right) => left.monthlyAmountCents - right.monthlyAmountCents)[0];
  if (!tier) {
    return null;
  }

  return {
    label: "Configurer",
    href: `/services/vps/choisir?serviceCode=${encodeURIComponent(serviceCode)}&tierCode=${encodeURIComponent(tier.code)}`,
  };
}


export function isStorefrontSelfServiceCta(label: string, href: string): boolean {
  const normalizedHref = href.trim().toLowerCase();
  return normalizedHref === "/formules"
    || normalizedHref.startsWith("/formules/")
    || SELF_SERVICE_LABEL_PATTERN.test(label.trim());
}

// Les routes et les codes techniques restent les identifiants stables du
// catalogue. Cette table ne modifie que le libelle public des liens associes,
// afin que le visiteur comprenne d'abord le service rendu.
const PUBLIC_RELATED_SERVICE_LABELS: Readonly<Record<string, string>> = {
  "/services/infogerance-vps": "Gestion de votre serveur",
  "/services/maintenance-linux": "Entretien de votre serveur",
  "/services/supervision-informatique": "Surveillance de vos services",
  "/services/supervision-nas": "Suivi de votre stockage",
  "/services/unifi": "Réseau et Wi-Fi",
  "/services/firewall": "Protection du réseau",
  "/services/cloudflare-waf": "Protection de site web",
  "/services/gestion-dns-domaines": "Gestion du nom de domaine",
};

export function resolveStorefrontPublicRelatedLinks(
  links: readonly StorefrontLink[],
  selfServiceOrderable: boolean | null,
): StorefrontLink[] {
  const visibleLinks = selfServiceOrderable !== false
    ? links
    : links.filter((link) => !isStorefrontSelfServiceCta(link.label, link.href));
  return visibleLinks.map((link) => ({
    ...link,
    label: link.href === "/diagnostic" && (
      GENERIC_AUDIT_LABEL_PATTERN.test(link.label)
      || link.label.trim() === "Demander un diagnostic"
    )
      ? "Faire le diagnostic"
      : PUBLIC_RELATED_SERVICE_LABELS[link.href] ?? link.label,
  }));
}

export function resolveStorefrontPublicCta(
  content: Pick<StorefrontPageContent, "ctaLabel" | "ctaHref">,
  selfServiceOrderable: boolean | null,
): StorefrontCta {
  const configured = { label: content.ctaLabel, href: content.ctaHref };
  if (selfServiceOrderable !== false || !isStorefrontSelfServiceCta(configured.label, configured.href)) {
    return normalizeStorefrontPublicCta(configured);
  }
  // Le rendu public est l'autorité finale. Même un CTA CMS volontairement
  // incohérent est neutralisé pour un service Billing non self-service.
  if (configured.href === "/diagnostic") {
    return { label: "Faire le diagnostic", href: "/diagnostic" };
  }
  return { label: "Demander un devis", href: "/contact" };
}

/**
 * Vocabulaire public : le diagnostic est le questionnaire d'orientation,
 * le devis qualifie une prestation et le contact reste general. Un CTA CMS
 * peut nommer un vrai audit seulement lorsqu'il conduit vers une prestation
 * identifiee ; /contact et /diagnostic ne sont jamais des synonymes d'audit.
 */
function normalizeStorefrontPublicCta(configured: StorefrontCta): StorefrontCta {
  if (!GENERIC_AUDIT_LABEL_PATTERN.test(configured.label)) return configured;
  if (configured.href === "/diagnostic") {
    return { label: "Faire le diagnostic", href: "/diagnostic" };
  }
  if (configured.href === "/contact") {
    return { label: "Nous contacter", href: "/contact" };
  }
  return configured;
}
export function parseStorefrontPageContent(
  value: string,
): StorefrontPageContent | null {
  try {
    const candidate = JSON.parse(value) as Partial<StorefrontPageContent>;
    if (
      !isText(candidate.seoTitle, 10, 200)
      || !isText(candidate.seoDescription, 30, 400)
      || !isText(candidate.title, 3, 200)
      || !isText(candidate.lead, 10, 1200)
      || !isText(candidate.ctaLabel, 3, 80)
      || !isSafeInternalPath(candidate.ctaHref)
      || !Array.isArray(candidate.sections)
      || candidate.sections.length < 1
      || candidate.sections.length > 12
      || !candidate.sections.every(
        (section) => isText(section?.heading, 2, 4000)
          && isText(section?.bodyMarkdown, 3, 12000),
      )
      || !Array.isArray(candidate.faq)
      || candidate.faq.length < 2
      || candidate.faq.length > 12
      || !candidate.faq.every(
        (item) => isText(item?.question, 2, 4000)
          && isText(item?.answer, 3, 12000),
      )
      || !Array.isArray(candidate.relatedLinks)
      || candidate.relatedLinks.length < 1
      || candidate.relatedLinks.length > 12
      || !candidate.relatedLinks.every(
        (link) => isText(link?.label, 2, 4000) && isSafeInternalPath(link?.href),
      )
    ) {
      return null;
    }
    return {
      seoTitle: candidate.seoTitle.trim().replace(/\s*\|\s*Zachary IT$/i, ""),
      seoDescription: candidate.seoDescription.trim(),
      title: candidate.title.trim(),
      lead: candidate.lead.trim(),
      ctaLabel: candidate.ctaLabel.trim(),
      ctaHref: candidate.ctaHref.trim(),
      sections: candidate.sections.map((section) => ({
        heading: section.heading.trim(),
        bodyMarkdown: section.bodyMarkdown.trim(),
      })),
      faq: candidate.faq.map((item) => ({
        question: item.question.trim(),
        answer: item.answer.trim(),
      })),
      relatedLinks: candidate.relatedLinks.map((link) => ({
        label: link.label.trim(),
        href: link.href.trim(),
      })),
    };
  } catch {
    return null;
  }
}
export function parseStorefrontServicesLandingContent(
  value: string,
  allowLegacy = false,
): StorefrontServicesLandingContent | null {
  const base = parseStorefrontPageContent(value);
  if (!base) return null;

  try {
    const candidate = JSON.parse(value) as { problemEntries?: unknown };
    const hasValidProblemEntries = Array.isArray(candidate.problemEntries)
      && candidate.problemEntries.length === 6
      && candidate.problemEntries.every((entry) => {
        if (!entry || typeof entry !== "object") return false;
        const item = entry as Partial<StorefrontProblemEntry>;
        return isText(item.title, 3, 120)
          && isText(item.description, 10, 400)
          && typeof item.href === "string"
          && STOREFRONT_SERVICES_PROBLEM_DESTINATIONS.includes(
            item.href as StorefrontServicesProblemDestination,
          );
      });

    const hasValidCategoryLinks = base.relatedLinks.length === 4
      && base.relatedLinks.every((link) =>
        STOREFRONT_SERVICES_CATEGORY_DESTINATIONS.includes(
          link.href as (typeof STOREFRONT_SERVICES_CATEGORY_DESTINATIONS)[number],
        )
      )
      && new Set(base.relatedLinks.map((link) => link.href)).size === 4;

    if (!hasValidProblemEntries || !hasValidCategoryLinks) {
      if (!allowLegacy) return null;
      return {
        ...base,
        seoDescription: DEFAULT_STOREFRONT_SERVICES_SEO_DESCRIPTION,
        lead: DEFAULT_STOREFRONT_SERVICES_LEAD,
        sections: DEFAULT_STOREFRONT_SERVICES_SECTIONS.map((section) => ({ ...section })),
        faq: DEFAULT_STOREFRONT_SERVICES_FAQ.map((item) => ({ ...item })),
        relatedLinks: DEFAULT_STOREFRONT_SERVICES_CATEGORY_LINKS.map((link) => ({ ...link })),
        problemEntries: DEFAULT_STOREFRONT_SERVICES_PROBLEM_ENTRIES.map((entry) => ({ ...entry })),
      };
    }

    const problemEntries = (candidate.problemEntries as unknown[]).map((entry: unknown) => {
      const item = entry as StorefrontProblemEntry;
      return {
        title: item.title.trim(),
        description: item.description.trim(),
        href: item.href,
      };
    });

    if (new Set(problemEntries.map((entry) => entry.href)).size !== problemEntries.length) {
      return null;
    }

    return { ...base, problemEntries };
  } catch {
    return null;
  }
}
export function isStorefrontContentKey(key: ManagedContentKey): boolean {
  return key.startsWith("storefront:");
}
function isText(value: unknown, min: number, max: number): value is string {
  return typeof value === "string" && value.trim().length >= min && value.trim().length <= max;
}
function isSafeInternalPath(value: unknown): value is string {
  return typeof value === "string"
    && value.startsWith("/")
    && !value.startsWith("//")
    && !value.includes("\\")
    && value.length <= 160;
}
