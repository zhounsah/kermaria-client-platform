export const DIAGNOSTIC_CONTEXT_IDS = [
  "backup",
  "remote-access",
  "network",
  "messaging",
  "domain-dns",
  "server",
  "web-hosting",
  "general",
] as const;

export type DiagnosticContextId = (typeof DIAGNOSTIC_CONTEXT_IDS)[number];
export type DiagnosticAnswerValue = string | readonly string[];
export type DiagnosticAnswerMap = Record<string, DiagnosticAnswerValue | undefined>;

export type DiagnosticQuestionOption = {
  value: string;
  label: string;
  exclusive?: boolean;
};

export type DiagnosticQuestion = {
  id: string;
  legend: string;
  summaryLabel: string;
  mode: "single" | "multi";
  options: readonly DiagnosticQuestionOption[];
  hint?: string;
  when?: { questionId: string; values: readonly string[] };
};

export type DiagnosticContextDefinition = {
  id: DiagnosticContextId;
  label: string;
  eyebrow: string;
  title: string;
  intro: string;
  contactSubject: string;
  formulaEligible: boolean;
  questions: readonly DiagnosticQuestion[];
};

const o = (value: string, label: string, exclusive = false): DiagnosticQuestionOption => ({
  value,
  label,
  exclusive,
});

const q = (
  id: string,
  legend: string,
  summaryLabel: string,
  options: readonly DiagnosticQuestionOption[],
  mode: "single" | "multi" = "single",
  hint?: string,
  when?: DiagnosticQuestion["when"],
): DiagnosticQuestion => ({ id, legend, summaryLabel, options, mode, hint, when });

const STRUCTURES = [
  o("individual", "Moi-même / un particulier"),
  o("business", "Une activité professionnelle ou une petite entreprise"),
  o("association", "Une association"),
  o("other", "Une autre structure"),
] as const;

const USERS = [
  ...Array.from({ length: 11 }, (_, index) => o(String(index + 1), String(index + 1))),
  o("12-plus", "12 ou plus"),
  o("unknown", "Je ne sais pas", true),
] as const;

const CONTEXTS: Record<DiagnosticContextId, DiagnosticContextDefinition> = {
  general: {
    id: "general",
    label: "Besoin à préciser",
    eyebrow: "Diagnostic IT",
    title: "Partons de ce que vous cherchez à résoudre.",
    intro:
      "Choisissez simplement le sujet le plus proche de votre situation. Vous n'avez pas besoin de connaître la solution technique à l'avance.",
    contactSubject: "Diagnostic IT",
    formulaEligible: false,
    questions: [],
  },
  backup: {
    id: "backup",
    label: "Sauvegarde et protection des données",
    eyebrow: "Diagnostic sauvegarde",
    title: "Voyons ce qu'il faut réellement protéger.",
    intro:
      "Quelques réponses suffisent pour distinguer une protection simple de fichiers d'un poste, d'un serveur, d'un NAS ou d'un environnement qui demande un cadrage plus précis.",
    contactSubject: "Diagnostic sauvegarde",
    formulaEligible: true,
    questions: [
      q(
        "backup-targets",
        "Que faut-il protéger ?",
        "À protéger",
        [
          o("files", "Des fichiers et documents"),
          o("workstations", "Un ou plusieurs ordinateurs"),
          o("server", "Un serveur"),
          o("nas", "Un NAS ou un stockage réseau"),
          o("other", "Autre chose"),
          o("unknown", "Je ne sais pas encore", true),
        ],
        "multi",
        "Vous pouvez sélectionner plusieurs éléments.",
      ),
      q("storage", "Quel volume faut-il protéger environ ?", "Volume approximatif", [
        o("16", "Jusqu'à 16 Go"),
        o("32", "Jusqu'à 32 Go"),
        o("64", "Jusqu'à 64 Go"),
        o("128", "Jusqu'à 128 Go"),
        o("256", "Jusqu'à 256 Go"),
        o("above-public-max", "Plus de 256 Go"),
        o("unknown", "Je ne sais pas"),
      ]),
      q("structure", "Ce besoin concerne qui ?", "Contexte", STRUCTURES),
      q("users", "Combien de personnes sont concernées ?", "Personnes concernées", USERS),
      q(
        "backup-existing",
        "Avez-vous déjà une sauvegarde distincte de vos fichiers principaux ?",
        "Sauvegarde actuelle",
        [o("yes", "Oui"), o("partial", "En partie"), o("no", "Non"), o("unknown", "Je ne sais pas")],
        "single",
        "Une synchronisation seule n'est pas forcément une sauvegarde.",
      ),
      q("restore-test", "Savez-vous si une restauration a été testée récemment ?", "Test de restauration", [
        o("recent", "Oui, il y a moins d'un an"),
        o("old", "Oui, mais il y a plus d'un an"),
        o("never", "Non, jamais"),
        o("unknown", "Je ne sais pas"),
      ]),
    ],
  },
  "remote-access": {
    id: "remote-access",
    label: "Accès distant",
    eyebrow: "Diagnostic accès distant",
    title: "Commençons par ce que vous voulez retrouver à distance.",
    intro:
      "Le bon choix dépend surtout des ressources à atteindre, des personnes concernées et de l'existant. Vous n'avez pas à choisir vous-même entre VPN et bureau distant.",
    contactSubject: "Diagnostic accès distant",
    formulaEligible: true,
    questions: [
      q("remote-target", "Que voulez-vous pouvoir utiliser depuis l'extérieur ?", "Accès recherché", [
        o("files", "Des fichiers ou un NAS"),
        o("internal-app", "Une application ou un service interne"),
        o("windows-desktop", "Un environnement Windows complet avec ses logiciels"),
        o("several", "Plusieurs de ces éléments"),
        o("unknown", "Je ne sais pas encore"),
      ]),
      q("structure", "Ce besoin concerne qui ?", "Contexte", STRUCTURES),
      q("users", "Combien de personnes devront se connecter ?", "Personnes concernées", USERS),
      q("remote-existing", "Les ressources à atteindre existent-elles déjà ?", "Environnement actuel", [
        o("existing", "Oui, elles existent déjà"),
        o("new", "Non, c'est une nouvelle mise en place"),
        o("mixed", "En partie"),
        o("unknown", "Je ne sais pas"),
      ]),
      q(
        "sites",
        "Combien de lieux doivent être reliés ou accessibles ?",
        "Lieux concernés",
        [
          o("one", "Un seul site ou lieu"),
          o("several", "Plusieurs sites ou lieux"),
          o("none", "Aucun site : seulement un service hébergé"),
          o("unknown", "Je ne sais pas"),
        ],
        "single",
        undefined,
        { questionId: "remote-existing", values: ["existing", "mixed", "unknown"] },
      ),
      q(
        "devices",
        "Depuis quels appareils voulez-vous travailler ?",
        "Appareils utilisés",
        [
          o("windows", "PC Windows"),
          o("mac", "Mac"),
          o("mobile", "Téléphone ou tablette"),
          o("several", "Plusieurs types d'appareils"),
          o("unknown", "Je ne sais pas", true),
        ],
        "multi",
      ),
    ],
  },
  network: {
    id: "network",
    label: "Réseau, Wi-Fi et UniFi",
    eyebrow: "Diagnostic réseau",
    title: "Partons du problème réseau que vous constatez.",
    intro:
      "Couverture Wi-Fi, coupures, lenteurs, nouvelle installation ou reprise d'un réseau existant : le diagnostic commence par vos usages, pas par le matériel à acheter.",
    contactSubject: "Diagnostic réseau / Wi-Fi",
    formulaEligible: false,
    questions: [
      q(
        "network-goal",
        "Qu'est-ce qui vous gêne ou que voulez-vous mettre en place ?",
        "Besoin principal",
        [
          o("coverage", "Le Wi-Fi couvre mal certains endroits"),
          o("drops", "Il y a des coupures ou des déconnexions"),
          o("slow", "Le réseau semble lent"),
          o("new", "Je prépare une nouvelle installation"),
          o("rework", "Je veux reprendre ou faire évoluer l'installation"),
          o("separation", "Je veux mieux séparer les usages ou les équipements"),
          o("unknown", "Je ne sais pas encore", true),
        ],
        "multi",
      ),
      q("network-existing", "Avez-vous déjà une installation réseau en place ?", "Installation actuelle", [
        o("unifi", "Oui, principalement UniFi"),
        o("other", "Oui, avec un autre matériel"),
        o("mixed", "Oui, avec plusieurs marques ou générations"),
        o("none", "Non, c'est une nouvelle installation"),
        o("unknown", "Je ne sais pas"),
      ]),
      q("network-sites", "Combien de sites ou locaux sont concernés ?", "Sites concernés", [
        o("one", "Un"),
        o("several", "Plusieurs"),
        o("unknown", "Je ne sais pas"),
      ]),
      q(
        "network-scale",
        "Quel ordre de grandeur représente l'installation ?",
        "Taille approximative",
        [
          o("small", "Jusqu'à 10 appareils"),
          o("medium", "Environ 10 à 30 appareils"),
          o("large", "Plus de 30 appareils"),
          o("unknown", "Je ne sais pas"),
        ],
        "single",
        "Une estimation suffit ; inutile de compter précisément tous les appareils.",
      ),
    ],
  },
  messaging: {
    id: "messaging",
    label: "Messagerie professionnelle",
    eyebrow: "Diagnostic messagerie",
    title: "Voyons ce que votre messagerie doit mieux faire.",
    intro:
      "Création d'adresses professionnelles, migration, messages classés en spam ou administration quotidienne : quelques réponses permettent de cadrer la bonne intervention.",
    contactSubject: "Diagnostic messagerie professionnelle",
    formulaEligible: false,
    questions: [
      q("mail-goal", "Quel est votre besoin principal ?", "Besoin principal", [
        o("new", "Créer une messagerie professionnelle"),
        o("migration", "Migrer des boîtes existantes"),
        o("deliverability", "Mes messages arrivent en spam ou sont refusés"),
        o("management", "Faire administrer une messagerie existante"),
        o("incident", "Résoudre un problème actuel"),
        o("unknown", "Je ne sais pas exactement"),
      ]),
      q("mail-domain", "Avez-vous déjà votre propre nom de domaine ?", "Nom de domaine", [
        o("yes", "Oui"),
        o("no", "Non"),
        o("unknown", "Je ne sais pas"),
      ]),
      q("mailboxes", "Combien de boîtes ou adresses principales sont concernées ?", "Boîtes concernées", [
        o("1-3", "1 à 3"),
        o("4-10", "4 à 10"),
        o("11-plus", "11 ou plus"),
        o("unknown", "Je ne sais pas"),
      ]),
      q("mail-existing", "Utilisez-vous déjà un service de messagerie ?", "Service actuel", [
        o("m365", "Oui, Microsoft 365"),
        o("other", "Oui, un autre service"),
        o("none", "Non"),
        o("unknown", "Je ne sais pas"),
      ]),
      q(
        "mail-data",
        "Faut-il reprendre d'anciens e-mails, calendriers ou contacts ?",
        "Données à reprendre",
        [o("yes", "Oui"), o("no", "Non"), o("maybe", "Peut-être"), o("unknown", "Je ne sais pas")],
        "single",
        undefined,
        { questionId: "mail-goal", values: ["migration", "management", "unknown"] },
      ),
    ],
  },
  "domain-dns": {
    id: "domain-dns",
    label: "Domaine et DNS",
    eyebrow: "Diagnostic domaine & DNS",
    title: "Commençons par ce que vous voulez reprendre ou raccorder.",
    intro:
      "Vous n'avez pas besoin de connaître les réglages techniques du domaine. Ce qui compte d'abord est de savoir qui le contrôle et quels services en dépendent.",
    contactSubject: "Diagnostic domaine et DNS",
    formulaEligible: false,
    questions: [
      q("domain-goal", "Que voulez-vous faire ?", "Objectif", [
        o("new", "Créer ou préparer un nouveau domaine"),
        o("transfer", "Transférer ou reprendre la gestion d'un domaine"),
        o("access", "Retrouver ou clarifier les accès"),
        o("dns", "Corriger ou modifier des réglages"),
        o("connect", "Raccorder un site, une messagerie ou un autre service"),
        o("unknown", "Je ne sais pas exactement"),
      ]),
      q("domain-control", "Savez-vous qui possède aujourd'hui l'accès principal au domaine ?", "Accès au domaine", [
        o("me", "Oui, j'ai l'accès"),
        o("third-party", "Oui, un prestataire ou une autre personne l'a"),
        o("lost", "L'accès semble perdu"),
        o("unknown", "Je ne sais pas"),
      ]),
      q(
        "domain-services",
        "Quels services utilisent actuellement ce domaine ?",
        "Services liés",
        [
          o("website", "Un site web"),
          o("mail", "Des adresses e-mail"),
          o("apps", "Des applications ou services en ligne"),
          o("none", "Aucun pour le moment", true),
          o("unknown", "Je ne sais pas", true),
        ],
        "multi",
        "Sélectionnez ce que vous connaissez.",
      ),
      q("domain-urgency", "La situation bloque-t-elle actuellement un service ?", "Urgence", [
        o("blocked", "Oui, un site ou des e-mails sont déjà impactés"),
        o("planned", "Non, c'est une évolution planifiée"),
        o("unknown", "Je ne sais pas"),
      ]),
    ],
  },
  server: {
    id: "server",
    label: "Serveur, VPS et infogérance",
    eyebrow: "Diagnostic serveur",
    title: "Voyons d'abord ce que le serveur doit faire et dans quel état il se trouve.",
    intro:
      "Nouveau serveur, reprise d'un VPS existant, maintenance, supervision ou incident : l'objectif est de qualifier l'existant avant de promettre une solution.",
    contactSubject: "Diagnostic serveur / VPS",
    formulaEligible: false,
    questions: [
      q("server-goal", "Que souhaitez-vous faire ?", "Objectif", [
        o("new", "Mettre en place un nouveau serveur"),
        o("takeover", "Faire reprendre un serveur existant"),
        o("maintenance", "Organiser sa maintenance"),
        o("monitoring", "Mettre en place une supervision"),
        o("migration", "Migrer un serveur ou un service"),
        o("incident", "Résoudre un problème actuel"),
      ]),
      q(
        "server-use",
        "Qu'est-ce qui fonctionne ou fonctionnera dessus ?",
        "Usages",
        [
          o("website", "Un site web"),
          o("application", "Une application"),
          o("database", "Une base de données"),
          o("files", "Des fichiers ou du stockage"),
          o("services", "Des services internes"),
          o("other", "Autre chose"),
          o("unknown", "Je ne sais pas", true),
        ],
        "multi",
      ),
      q("server-existing", "Le serveur existe-t-il déjà ?", "État actuel", [
        o("hosted", "Oui, chez un hébergeur"),
        o("onsite", "Oui, dans mes locaux"),
        o("new", "Non, c'est un nouveau besoin"),
        o("unknown", "Je ne sais pas"),
      ]),
      q(
        "server-access",
        "Disposez-vous des accès d'administration ?",
        "Accès d'administration",
        [o("yes", "Oui"), o("partial", "En partie"), o("no", "Non"), o("unknown", "Je ne sais pas")],
        "single",
        undefined,
        { questionId: "server-existing", values: ["hosted", "onsite", "unknown"] },
      ),
      q("server-protection", "Savez-vous si les mises à jour et sauvegardes sont suivies aujourd'hui ?", "Maintenance actuelle", [
        o("yes", "Oui, elles sont suivies"),
        o("partial", "En partie"),
        o("no", "Non"),
        o("unknown", "Je ne sais pas"),
      ]),
      q("server-impact", "Que se passe-t-il pour votre activité si ce service s'arrête ?", "Impact d'une panne", [
        o("low", "Peu d'impact, je peux attendre"),
        o("medium", "Le travail est gêné"),
        o("high", "Une partie importante de l'activité est bloquée"),
        o("unknown", "Je ne sais pas"),
      ]),
    ],
  },
  "web-hosting": {
    id: "web-hosting",
    label: "Hébergement web",
    eyebrow: "Diagnostic hébergement web",
    title: "Voyons ce dont votre site a besoin pour rester maintenable.",
    intro:
      "Nouveau site, migration ou reprise d'un hébergement existant : quelques réponses permettent d'identifier les accès, la maintenance et les protections à prévoir.",
    contactSubject: "Diagnostic hébergement web",
    formulaEligible: false,
    questions: [
      q("web-goal", "Quelle est votre situation ?", "Situation", [
        o("new", "Je prépare un nouveau site"),
        o("migration", "Je veux déplacer un site existant"),
        o("management", "Je veux faire gérer l'hébergement actuel"),
        o("maintenance", "Je veux surtout organiser les mises à jour et la maintenance"),
        o("incident", "Le site rencontre actuellement un problème"),
      ]),
      q("web-type", "Savez-vous comment le site est construit ?", "Type de site", [
        o("wordpress", "WordPress"),
        o("other-cms", "Un autre outil de gestion de site"),
        o("custom", "Un site ou une application sur mesure"),
        o("static", "Un site simple / statique"),
        o("unknown", "Je ne sais pas"),
      ]),
      q("web-domain", "Le nom de domaine existe-t-il déjà ?", "Nom de domaine", [
        o("yes", "Oui"),
        o("no", "Non"),
        o("unknown", "Je ne sais pas"),
      ]),
      q(
        "web-access",
        "Avez-vous accès à l'hébergement ou à l'administration du site ?",
        "Accès actuels",
        [o("yes", "Oui"), o("partial", "En partie"), o("no", "Non"), o("unknown", "Je ne sais pas")],
        "single",
        undefined,
        { questionId: "web-goal", values: ["migration", "management", "maintenance", "incident"] },
      ),
      q("web-maintenance", "Les sauvegardes et mises à jour sont-elles suivies aujourd'hui ?", "Suivi actuel", [
        o("yes", "Oui"),
        o("partial", "En partie"),
        o("no", "Non"),
        o("not-applicable", "Pas encore : le site est nouveau"),
        o("unknown", "Je ne sais pas"),
      ]),
    ],
  },
};

export const GENERAL_CONTEXT_CHOICES = [
  { context: "backup", title: "Protéger mes données", description: "Sauvegarde, restauration, ordinateur, serveur ou NAS." },
  { context: "remote-access", title: "Travailler à distance", description: "Accéder à des fichiers, applications ou à un environnement Windows." },
  { context: "network", title: "Améliorer mon réseau ou mon Wi-Fi", description: "Couverture, coupures, lenteurs, UniFi ou nouvelle installation." },
  { context: "messaging", title: "Organiser ma messagerie", description: "Adresses professionnelles, migration, spam ou administration." },
  { context: "domain-dns", title: "Gérer mon domaine", description: "Accès, transfert, réglages ou raccordement à un site ou une messagerie." },
  { context: "server", title: "Gérer un serveur ou un VPS", description: "Mise en place, reprise, maintenance, supervision ou migration." },
  { context: "web-hosting", title: "Héberger ou maintenir un site web", description: "Nouveau site, migration, sauvegardes et maintenance." },
] as const satisfies readonly {
  context: Exclude<DiagnosticContextId, "general">;
  title: string;
  description: string;
}[];

const SERVICE_CONTEXTS: Readonly<Record<string, Exclude<DiagnosticContextId, "general">>> = {
  "sauvegarde-externalisee": "backup",
  "supervision-nas": "backup",
  "vpn-entreprise": "remote-access",
  "bureau-windows-distance": "remote-access",
  unifi: "network",
  firewall: "network",
  "messagerie-professionnelle": "messaging",
  "gestion-dns-domaines": "domain-dns",
  vps: "server",
  "infogerance-vps": "server",
  "maintenance-linux": "server",
  "supervision-informatique": "server",
  "hebergement-web": "web-hosting",
  "maintenance-wordpress": "web-hosting",
  "cloudflare-waf": "web-hosting",
};

export function resolveDiagnosticContext(value: unknown): DiagnosticContextId {
  if (typeof value !== "string") return "general";
  const normalized = value.trim().toLowerCase();
  return DIAGNOSTIC_CONTEXT_IDS.includes(normalized as DiagnosticContextId)
    ? normalized as DiagnosticContextId
    : "general";
}

export function getDiagnosticContextDefinition(context: DiagnosticContextId) {
  return CONTEXTS[context];
}

export function diagnosticContextForServiceSlug(slug: string): DiagnosticContextId {
  return SERVICE_CONTEXTS[slug] ?? "general";
}

export function buildDiagnosticHref(context: DiagnosticContextId): string {
  return context === "general" ? "/diagnostic" : `/diagnostic?context=${context}`;
}

export function contextualizeDiagnosticHref(
  href: string,
  context: DiagnosticContextId,
): string {
  const pathname = href.split(/[?#]/, 1)[0];
  return pathname === "/diagnostic" ? buildDiagnosticHref(context) : href;
}

export function getVisibleDiagnosticQuestions(
  context: DiagnosticContextId,
  answers: DiagnosticAnswerMap,
): readonly DiagnosticQuestion[] {
  return CONTEXTS[context].questions.filter((question) => {
    if (!question.when) return true;
    const value = answers[question.when.questionId];
    return typeof value === "string" && question.when.values.includes(value);
  });
}

export function pruneHiddenDiagnosticAnswers(
  context: DiagnosticContextId,
  answers: DiagnosticAnswerMap,
): DiagnosticAnswerMap {
  const visibleIds = new Set(getVisibleDiagnosticQuestions(context, answers).map((question) => question.id));
  return Object.fromEntries(Object.entries(answers).filter(([id]) => visibleIds.has(id)));
}

export function isDiagnosticQuestionAnswered(
  question: DiagnosticQuestion,
  answers: DiagnosticAnswerMap,
): boolean {
  const value = answers[question.id];
  return question.mode === "multi"
    ? Array.isArray(value) && value.length > 0
    : typeof value === "string" && value.length > 0;
}

export function describeDiagnosticAnswers(
  context: DiagnosticContextId,
  answers: DiagnosticAnswerMap,
): readonly { label: string; value: string }[] {
  return getVisibleDiagnosticQuestions(context, answers).flatMap((question) => {
    const raw = answers[question.id];
    const values = Array.isArray(raw) ? raw : typeof raw === "string" ? [raw] : [];
    if (values.length === 0) return [];
    const labels = values.map(
      (value) => question.options.find((option) => option.value === value)?.label ?? value,
    );
    return [{ label: question.summaryLabel, value: labels.join(", ") }];
  });
}

export function buildDiagnosticContactMessage(
  context: DiagnosticContextId,
  answers: DiagnosticAnswerMap,
): string {
  const definition = CONTEXTS[context];
  const lines = describeDiagnosticAnswers(context, answers)
    .map((item) => `- ${item.label} : ${item.value}`);
  return [
    `Bonjour, je souhaite être conseillé à partir de mon ${definition.label.toLowerCase()}.`,
    "",
    "Résumé de mes réponses :",
    ...lines,
    "",
    "Vous pouvez compléter ce message avec toute précision utile.",
  ].join("\n");
}
