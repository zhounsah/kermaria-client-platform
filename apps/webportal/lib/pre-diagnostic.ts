import type {
  DiagnosticConditionConfig,
  PreDiagnosticCategoryConfig,
  PreDiagnosticCommercialQuestionConfig,
  PreDiagnosticConfiguration,
  PreDiagnosticProfileConfig,
  PreDiagnosticQuestionConfig,
} from "@kermaria/shared";

export const PRE_DIAGNOSTIC_PROFILES = ["individual", "professional", "association"] as const;
export type PreDiagnosticProfile = (typeof PRE_DIAGNOSTIC_PROFILES)[number];
export type PreDiagnosticAnswers = Record<string, string> & { profile: PreDiagnosticProfile };

export type PreDiagnosticCategory = {
  id: "equipment" | "backup" | "network" | "security" | "continuity";
  label: string;
  score: number;
};

export type PreDiagnosticResult = {
  score: number;
  level: "Bon" | "À surveiller" | "Améliorations recommandées" | "Risque important";
  categories: PreDiagnosticCategory[];
  priorities: { title: string; body: string }[];
  positives: string[];
};

export type PreDiagnosticQuestion = {
  id: string;
  category: string;
  label: string;
  hint?: string;
  /**
   * Libellé explicatif destiné aux interfaces d'administration. Il décrit la
   * contribution de la question sans déplacer la règle de calcul hors du
   * moteur applicatif (TypeScript et API-INTERNAL).
   */
  assessmentRole?: string;
  profiles?: readonly PreDiagnosticProfile[];
  /** Omettre une réponse est autorisé uniquement pour les questions v2 facultatives. */
  required?: boolean;
  options: readonly { value: string; label: string }[];
};

const ALL_PROFILES = PRE_DIAGNOSTIC_PROFILES;
const ORGANISATIONS: readonly PreDiagnosticProfile[] = ["professional", "association"];

export const PRE_DIAGNOSTIC_QUESTIONS: readonly PreDiagnosticQuestion[] = [
  {
    id: "profile", category: "Profil", label: "Ce diagnostic concerne…", assessmentRole: "Adapte les questions et les catégories applicables ; ne modifie pas le score directement.", options: [
      { value: "individual", label: "Mon informatique personnelle" },
      { value: "professional", label: "Mon entreprise / activité professionnelle" },
      { value: "association", label: "Une association" },
    ],
  },
  {
    id: "equipmentCount", category: "Équipement", label: "Combien d'appareils sont concernés environ ?", assessmentRole: "Décrit le contexte ; ne modifie pas le score directement.", profiles: ALL_PROFILES, options: [
      { value: "1-2", label: "1 à 2" }, { value: "3-5", label: "3 à 5" }, { value: "6+", label: "6 ou plus" },
    ],
  },
  {
    id: "equipmentAge", category: "Équipement", label: "Quel est l'âge moyen du matériel ?", assessmentRole: "Contribue au score Équipements.", profiles: ALL_PROFILES, options: [
      { value: "under3", label: "Moins de 3 ans" }, { value: "3to5", label: "Entre 3 et 5 ans" }, { value: "over5", label: "Plus de 5 ans ou très variable" },
    ],
  },
  {
    id: "performance", category: "Équipement", label: "Rencontrez-vous des lenteurs, blocages ou coupures ?", assessmentRole: "Contribue au score Équipements.", profiles: ALL_PROFILES, options: [
      { value: "none", label: "Rarement ou jamais" }, { value: "occasional", label: "Par moments" }, { value: "frequent", label: "Souvent" },
    ],
  },
  {
    id: "updates", category: "Équipement", label: "Les mises à jour sont-elles suivies ?", assessmentRole: "Contribue au score Équipements.", profiles: ALL_PROFILES, options: [
      { value: "automatic", label: "Oui, elles sont généralement automatiques" }, { value: "manual", label: "Oui, mais de façon irrégulière" }, { value: "unknown", label: "Je ne sais pas" },
    ],
  },
  {
    id: "backup", category: "Sauvegardes", label: "Comment vos données importantes sont-elles sauvegardées ?", hint: "Une synchronisation OneDrive ou Google Drive seule ne protège pas forcément d'une suppression, d'une erreur ou d'un ransomware.", assessmentRole: "Détermine le score Sauvegardes.", profiles: ALL_PROFILES, options: [
      { value: "automatic_external_tested", label: "Automatiquement hors site, avec une restauration déjà testée" },
      { value: "automatic_external", label: "Automatiquement hors site, sans test récent" },
      { value: "automatic_same_site", label: "Automatiquement, mais sur le même lieu ou appareil" },
      { value: "cloud_sync_only", label: "Uniquement via OneDrive, Google Drive ou équivalent" },
      { value: "manual", label: "Manuellement de temps en temps" },
      { value: "none", label: "Pas de sauvegarde connue" },
    ],
  },
  {
    id: "network", category: "Réseau / Wi-Fi", label: "Votre connexion et votre Wi-Fi sont-ils fiables ?", assessmentRole: "Contribue au score Réseau / Wi-Fi.", profiles: ALL_PROFILES, options: [
      { value: "stable", label: "Oui, sans difficulté notable" }, { value: "occasional", label: "Quelques coupures ou lenteurs" }, { value: "frequent", label: "Des problèmes fréquents" },
    ],
  },
  {
    id: "wifiCoverage", category: "Réseau / Wi-Fi", label: "La couverture Wi-Fi est-elle suffisante là où vous travaillez ?", assessmentRole: "Contribue au score Réseau / Wi-Fi.", profiles: ALL_PROFILES, options: [
      { value: "good", label: "Oui" }, { value: "some_areas", label: "Pas dans toutes les zones" }, { value: "poor", label: "Non, elle gêne régulièrement" },
    ],
  },
  {
    id: "guestWifi", category: "Réseau / Wi-Fi", label: "Le Wi-Fi visiteurs est-il séparé des appareils de travail ?", assessmentRole: "Contribue au score Réseau / Wi-Fi des organisations.", profiles: ORGANISATIONS, options: [
      { value: "separate", label: "Oui" }, { value: "same", label: "Non ou je ne sais pas" },
    ],
  },
  {
    id: "mfa", category: "Sécurité", label: "La validation en deux étapes est-elle activée sur les comptes importants ?", assessmentRole: "Contribue au score Sécurité.", profiles: ALL_PROFILES, options: [
      { value: "all", label: "Oui, sur les comptes importants" }, { value: "some", label: "Seulement sur certains comptes" }, { value: "none", label: "Non ou je ne sais pas comment faire" },
    ],
  },
  {
    id: "sharedAccounts", category: "Sécurité", label: "Des comptes ou mots de passe sont-ils partagés entre plusieurs personnes ?", assessmentRole: "Contribue au score Sécurité.", profiles: ALL_PROFILES, options: [
      { value: "no", label: "Non" }, { value: "some", label: "Parfois" }, { value: "yes", label: "Oui, régulièrement" },
    ],
  },
  {
    id: "phishing", category: "Sécurité", label: "Les personnes concernées savent-elles reconnaître un e-mail ou lien suspect ?", assessmentRole: "Contribue au score Sécurité.", profiles: ALL_PROFILES, options: [
      { value: "aware", label: "Oui, globalement" }, { value: "unsure", label: "Pas toujours" }, { value: "no", label: "C'est un sujet peu connu" },
    ],
  },
  {
    id: "continuity", category: "Continuité", label: "En cas de panne, pouvez-vous reprendre l'activité ou restaurer les données ?", assessmentRole: "Détermine le score Continuité des organisations.", profiles: ORGANISATIONS, options: [
      { value: "tested", label: "Oui, une procédure existe et a déjà été testée" }, { value: "partial", label: "Partiellement, mais sans procédure réellement testée" }, { value: "none", label: "Non, ce serait difficile ou incertain" },
    ],
  },
  {
    id: "businessDependence", category: "Continuité", label: "À quel point une panne informatique bloquerait-elle votre activité ?", assessmentRole: "Ajuste le score Continuité selon l'impact déclaré.", profiles: ORGANISATIONS, options: [
      { value: "low", label: "Peu : nous pouvons attendre" }, { value: "medium", label: "Partiellement : certaines tâches seraient bloquées" }, { value: "high", label: "Fortement : l'activité ou l'accueil seraient bloqués" },
    ],
  },
];

export function questionsForProfile(profile: PreDiagnosticProfile | null): readonly PreDiagnosticQuestion[] {
  if (!profile) return PRE_DIAGNOSTIC_QUESTIONS.slice(0, 1);
  return PRE_DIAGNOSTIC_QUESTIONS.filter((question) => !question.profiles || question.profiles.includes(profile));
}

/**
 * Garde le payload du rappel strictement limité aux réponses du pré-diagnostic
 * de santé. Les réponses de qualification commerciale restent dans le navigateur
 * et servent seulement à générer une sélection Billing V2 revalidée côté serveur.
 */
export function scoringAnswersForProfile(
  answers: Record<string, string>,
  profile: PreDiagnosticProfile,
  configuration?: PreDiagnosticConfiguration,
): PreDiagnosticAnswers {
  if (configuration) {
    return Object.fromEntries(
      configuredQuestionsForProfile(configuration, profile, answers)
        .flatMap((question) => {
          const answer = answers[question.id];
          return answer || question.required !== false ? [[question.id, answer ?? ""]] : [];
        }),
    ) as PreDiagnosticAnswers;
  }
  return Object.fromEntries(
    questionsForProfile(profile).map((question) => [question.id, answers[question.id] ?? ""]),
  ) as PreDiagnosticAnswers;
}

function deduct(score: number, value: string | undefined, deductions: Record<string, number>) {
  return Math.max(0, score - (deductions[value ?? ""] ?? 0));
}

function priorityFor(category: PreDiagnosticCategory, answers: PreDiagnosticAnswers) {
  if (category.id === "backup") {
    return { title: "Mettre en place une sauvegarde réellement récupérable", body: "La sauvegarde actuelle ne combine pas copie externalisée et restauration testée. Une synchronisation seule ne remplace pas cette protection." };
  }
  if (category.id === "security") {
    return { title: "Renforcer les accès aux comptes importants", body: "La validation en deux étapes et des comptes individuels réduisent fortement le risque lié au phishing ou à un mot de passe compromis." };
  }
  if (category.id === "network") {
    return { title: "Fiabiliser le Wi-Fi et les accès réseau", body: "Les réponses indiquent des coupures ou une couverture inégale qui peuvent gêner le travail au quotidien." };
  }
  if (category.id === "equipment") {
    return { title: "Planifier les améliorations de matériel et de mises à jour", body: "Du matériel vieillissant, des lenteurs ou des mises à jour irrégulières augmentent les interruptions et les risques de sécurité." };
  }
  return { title: "Préparer la reprise après incident", body: answers.businessDependence === "high" ? "Une panne pourrait bloquer l'activité ; une procédure de reprise testée permet de savoir quoi faire et dans quel ordre." : "Une procédure simple et testée évite d'improviser lorsque l'activité ou les données sont indisponibles." };
}

export function evaluatePreDiagnostic(answers: PreDiagnosticAnswers): PreDiagnosticResult {
  let equipment = 100;
  equipment = deduct(equipment, answers.equipmentAge, { "3to5": 10, over5: 25 });
  equipment = deduct(equipment, answers.performance, { occasional: 15, frequent: 30 });
  equipment = deduct(equipment, answers.updates, { manual: 15, unknown: 20 });

  const backupScores: Record<string, number> = { automatic_external_tested: 100, automatic_external: 80, automatic_same_site: 45, cloud_sync_only: 35, manual: 25, none: 0 };
  const backup = backupScores[answers.backup] ?? 20;

  let network = 100;
  network = deduct(network, answers.network, { occasional: 15, frequent: 35 });
  network = deduct(network, answers.wifiCoverage, { some_areas: 12, poor: 30 });
  if (answers.profile !== "individual") network = deduct(network, answers.guestWifi, { same: 20 });

  let security = 100;
  security = deduct(security, answers.mfa, { some: 15, none: 35 });
  security = deduct(security, answers.sharedAccounts, { some: 12, yes: 30 });
  security = deduct(security, answers.phishing, { unsure: 8, no: 15 });

  const categories: PreDiagnosticCategory[] = [
    { id: "equipment", label: "Équipements", score: equipment },
    { id: "backup", label: "Sauvegarde", score: backup },
    { id: "network", label: "Réseau / Wi-Fi", score: network },
    { id: "security", label: "Sécurité", score: security },
  ];
  if (answers.profile !== "individual") {
    let continuity = ({ tested: 100, partial: 55, none: 15 } as Record<string, number>)[answers.continuity] ?? 35;
    if (answers.businessDependence === "high" && answers.continuity !== "tested") continuity = Math.max(0, continuity - 15);
    categories.push({ id: "continuity", label: "Continuité", score: continuity });
  }

  const weights = answers.profile === "individual" ? [0.25, 0.35, 0.2, 0.2] : [0.2, 0.3, 0.18, 0.2, 0.12];
  const score = Math.round(categories.reduce((total, category, index) => total + category.score * (weights[index] ?? 0), 0));
  const level = score >= 85 ? "Bon" : score >= 70 ? "À surveiller" : score >= 45 ? "Améliorations recommandées" : "Risque important";
  const priorities = [...categories].sort((a, b) => a.score - b.score).filter((category) => category.score < 70).slice(0, 3).map((category) => priorityFor(category, answers));
  const positives = categories.filter((category) => category.score >= 85).map((category) => {
    if (category.id === "backup") return "Vos sauvegardes sont externalisées et leur restauration a déjà été vérifiée.";
    if (category.id === "security") return "Les protections de compte et les bonnes pratiques de sécurité sont déjà bien engagées.";
    if (category.id === "network") return "Le réseau et le Wi-Fi ne montrent pas de fragilité particulière dans vos réponses.";
    if (category.id === "continuity") return "Une procédure de reprise existe et a déjà été testée.";
    return "Le matériel et le suivi des mises à jour semblent adaptés à l'usage déclaré.";
  });
  return { score, level, categories, priorities, positives };
}

/**
 * Moteur v2 generique. Les valeurs metier ne vivent pas dans cet interpreteur:
 * il applique seulement les categories, effets, ponderations et textes de la
 * configuration publiee. La version v1 ci-dessus reste le repli de
 * compatibilite tant qu'aucune v2 n'a ete publiee.
 */
export function evaluatePreDiagnosticWithConfiguration(
  configuration: PreDiagnosticConfiguration,
  answers: PreDiagnosticAnswers,
): PreDiagnosticResult {
  const categories = configuration.categories
    .filter((category) => category.active && (category.weights[answers.profile] ?? 0) > 0)
    .sort((left, right) => left.order - right.order)
    .map((category) => ({
      id: category.id as PreDiagnosticCategory["id"],
      label: category.label,
      score: evaluateCategory(configuration, category, answers),
    }));
  const score = clamp(Math.round(categories.reduce((total, category) => {
    const weight = configuration.categories.find((item) => item.id === category.id)
      ?.weights[answers.profile] ?? 0;
    return total + category.score * weight / 100;
  }, 0)));
  const level = configuration.levels
    .slice()
    .sort((left, right) => right.minimumScore - left.minimumScore || left.order - right.order)
    .find((candidate) => score >= candidate.minimumScore)?.label ?? "Risque important";
  const priorities = categories
    .flatMap((category) => configuration.priorities
      .filter((rule) => rule.categoryId === category.id && category.score < rule.threshold)
      .map((rule) => ({ ...rule, categoryScore: category.score })))
    .sort((left, right) => left.categoryScore - right.categoryScore || left.order - right.order)
    .slice(0, configuration.maximumPriorities)
    .map((rule) => ({ title: rule.title, body: rule.body }));
  const positives = categories
    .flatMap((category) => configuration.positives
      .filter((rule) => rule.categoryId === category.id && category.score >= rule.threshold))
    .sort((left, right) => left.order - right.order)
    .map((rule) => rule.text);

  return {
    score,
    level: level as PreDiagnosticResult["level"],
    categories,
    priorities,
    positives,
  };
}

export function configuredQuestionsForProfile(
  configuration: PreDiagnosticConfiguration,
  profile: PreDiagnosticProfile | null,
  answers: Record<string, string> = {},
): readonly PreDiagnosticQuestion[] {
  if (!profile) {
    const profileQuestion = configuration.questions.find((question) => question.id === "profile" && question.active);
    return profileQuestion ? [toQuestion(profileQuestion, configuration)] : [];
  }
  return configuration.questions
    .filter((question) => question.active && question.profiles.includes(profile))
    .filter((question) => question.when.every((condition) => matchesCondition(condition, answers)))
    .sort((left, right) => left.order - right.order)
    .map((question) => toQuestion(question, configuration));
}

function evaluateCategory(
  configuration: PreDiagnosticConfiguration,
  category: PreDiagnosticCategoryConfig,
  answers: PreDiagnosticAnswers,
) {
  let score = 100;
  for (const question of configuration.questions
    .filter((item) => item.active && item.categoryId === category.id && item.profiles.includes(answers.profile))
    .sort((left, right) => left.order - right.order)) {
    const option = question.options.find((item) => item.active && item.value === answers[question.id]);
    if (!option) continue;
    for (const effect of option.effects) {
      if (!effect.when.every((condition) => matchesCondition(condition, answers))) continue;
      score = effect.mode === "absolute" ? effect.value : score - effect.value;
      score = clamp(score);
    }
  }
  return score;
}

function matchesCondition(
  condition: { questionId: string; operator: string; values: readonly string[] },
  answers: Record<string, string>,
) {
  const value = answers[condition.questionId];
  switch (condition.operator) {
    case "equals": return value === condition.values[0];
    case "not_equals": return value !== condition.values[0];
    case "one_of": return value !== undefined && condition.values.includes(value);
    case "answered": return Boolean(value);
    default: return false;
  }
}

function toQuestion(
  question: PreDiagnosticQuestionConfig,
  configuration: PreDiagnosticConfiguration,
): PreDiagnosticQuestion {
  const category = question.categoryId
    ? configuration.categories.find((item) => item.id === question.categoryId)?.label
    : undefined;
  return {
    id: question.id,
    category: category ?? "Profil",
    label: question.label,
    hint: question.hint ?? undefined,
    profiles: question.profiles,
    required: question.required,
    options: question.options
      .filter((option) => option.active)
      .filter((option) => question.id !== "profile" || configuration.profiles.some((profile) => profile.id === option.value && profile.active))
      .sort((left, right) => left.order - right.order),
  };
}

function clamp(value: number) {
  return Math.max(0, Math.min(100, value));
}

const V2_PROFILES: PreDiagnosticProfileConfig[] = [
  { id: "individual", label: "Particulier", description: "Informatique personnelle", active: true, order: 10 },
  { id: "professional", label: "Entreprise / activité professionnelle", description: "Petite structure ou activité", active: true, order: 20 },
  { id: "association", label: "Association", description: "Structure associative", active: true, order: 30 },
];

const ALL = ["individual", "professional", "association"] as const;
const ORG = ["professional", "association"] as const;
const V2_CONTEXTS = ["general", "backup", "remote-access", "network", "messaging", "domain-dns", "server", "web-hosting"] as const;
const effect = (mode: "absolute" | "deduction", value: number, when: DiagnosticConditionConfig[] = []) => ({ mode, value, when });
const option = (value: string, label: string, order: number, effects: ReturnType<typeof effect>[] = []) => ({ value, label, active: true, order, effects });
const question = (id: string, categoryId: string | null, label: string, profiles: readonly PreDiagnosticProfile[], order: number, options: ReturnType<typeof option>[], hint: string | null = null, when: DiagnosticConditionConfig[] = []): PreDiagnosticQuestionConfig => ({ id, categoryId, label, hint, profiles: [...profiles], required: true, active: true, order, when, options });
const cOption = (value: string, label: string, order: number, profiles: readonly PreDiagnosticProfile[] = ALL, contexts: readonly string[] = V2_CONTEXTS) => ({ value, label, active: true, order, effects: [], profiles: [...profiles], contexts: [...contexts] });
const cQuestion = (id: string, label: string, profiles: readonly PreDiagnosticProfile[], contexts: readonly string[], order: number, options: ReturnType<typeof cOption>[], hint: string | null = null, dynamicOptions: PreDiagnosticCommercialQuestionConfig["dynamicOptions"] = "none"): PreDiagnosticCommercialQuestionConfig => ({ id, label, hint, profiles: [...profiles], contexts: [...contexts], active: true, order, dynamicOptions, options });

/**
 * Valeur initiale v2. Elle n'est active qu'apres enregistrement puis
 * publication; avant cela le parcours conserve strictement le comportement du
 * commit 77fc11c. L'API-INTERNAL expose ce meme document pour l'amorce admin.
 */
export const DEFAULT_PRE_DIAGNOSTIC_CONFIGURATION_V2: PreDiagnosticConfiguration = {
  schemaVersion: 2,
  profiles: V2_PROFILES,
  categories: [
    { id: "equipment", label: "Équipements", active: true, order: 10, weights: { individual: 25, professional: 20, association: 20 } },
    { id: "backup", label: "Sauvegarde", active: true, order: 20, weights: { individual: 35, professional: 30, association: 30 } },
    { id: "network", label: "Réseau / Wi-Fi", active: true, order: 30, weights: { individual: 20, professional: 18, association: 18 } },
    { id: "security", label: "Sécurité", active: true, order: 40, weights: { individual: 20, professional: 20, association: 20 } },
    { id: "continuity", label: "Continuité", active: true, order: 50, weights: { individual: 0, professional: 12, association: 12 } },
  ],
  questions: [
    question("profile", null, "Ce diagnostic concerne…", ALL, 10, [option("individual", "Mon informatique personnelle", 10), option("professional", "Mon entreprise / activité professionnelle", 20), option("association", "Une association", 30)]),
    question("equipmentCount", null, "Combien d'appareils sont concernés environ ?", ALL, 20, [option("1-2", "1 à 2", 10), option("3-5", "3 à 5", 20), option("6+", "6 ou plus", 30)]),
    question("equipmentAge", "equipment", "Quel est l'âge moyen du matériel ?", ALL, 30, [option("under3", "Moins de 3 ans", 10), option("3to5", "Entre 3 et 5 ans", 20, [effect("deduction", 10)]), option("over5", "Plus de 5 ans ou très variable", 30, [effect("deduction", 25)])]),
    question("performance", "equipment", "Rencontrez-vous des lenteurs, blocages ou coupures ?", ALL, 40, [option("none", "Rarement ou jamais", 10), option("occasional", "Par moments", 20, [effect("deduction", 15)]), option("frequent", "Souvent", 30, [effect("deduction", 30)])]),
    question("updates", "equipment", "Les mises à jour sont-elles suivies ?", ALL, 50, [option("automatic", "Oui, elles sont généralement automatiques", 10), option("manual", "Oui, mais de façon irrégulière", 20, [effect("deduction", 15)]), option("unknown", "Je ne sais pas", 30, [effect("deduction", 20)])]),
    question("backup", "backup", "Comment vos données importantes sont-elles sauvegardées ?", ALL, 60, [option("automatic_external_tested", "Automatiquement hors site, avec une restauration déjà testée", 10, [effect("absolute", 100)]), option("automatic_external", "Automatiquement hors site, sans test récent", 20, [effect("absolute", 80)]), option("automatic_same_site", "Automatiquement, mais sur le même lieu ou appareil", 30, [effect("absolute", 45)]), option("cloud_sync_only", "Uniquement via OneDrive, Google Drive ou équivalent", 40, [effect("absolute", 35)]), option("manual", "Manuellement de temps en temps", 50, [effect("absolute", 25)]), option("none", "Pas de sauvegarde connue", 60, [effect("absolute", 0)])], "Une synchronisation seule ne remplace pas une sauvegarde récupérable."),
    question("network", "network", "Votre connexion et votre Wi-Fi sont-ils fiables ?", ALL, 70, [option("stable", "Oui, sans difficulté notable", 10), option("occasional", "Quelques coupures ou lenteurs", 20, [effect("deduction", 15)]), option("frequent", "Des problèmes fréquents", 30, [effect("deduction", 35)])]),
    question("wifiCoverage", "network", "La couverture Wi-Fi est-elle suffisante là où vous travaillez ?", ALL, 80, [option("good", "Oui", 10), option("some_areas", "Pas dans toutes les zones", 20, [effect("deduction", 12)]), option("poor", "Non, elle gêne régulièrement", 30, [effect("deduction", 30)])]),
    question("guestWifi", "network", "Le Wi-Fi visiteurs est-il séparé des appareils de travail ?", ORG, 90, [option("separate", "Oui", 10), option("same", "Non ou je ne sais pas", 20, [effect("deduction", 20)])]),
    question("mfa", "security", "La validation en deux étapes est-elle activée sur les comptes importants ?", ALL, 100, [option("all", "Oui, sur les comptes importants", 10), option("some", "Seulement sur certains comptes", 20, [effect("deduction", 15)]), option("none", "Non ou je ne sais pas comment faire", 30, [effect("deduction", 35)])]),
    question("sharedAccounts", "security", "Des comptes ou mots de passe sont-ils partagés entre plusieurs personnes ?", ALL, 110, [option("no", "Non", 10), option("some", "Parfois", 20, [effect("deduction", 12)]), option("yes", "Oui, régulièrement", 30, [effect("deduction", 30)])]),
    question("phishing", "security", "Les personnes concernées savent-elles reconnaître un e-mail ou lien suspect ?", ALL, 120, [option("aware", "Oui, globalement", 10), option("unsure", "Pas toujours", 20, [effect("deduction", 8)]), option("no", "C'est un sujet peu connu", 30, [effect("deduction", 15)])]),
    question("continuity", "continuity", "En cas de panne, pouvez-vous reprendre l'activité ou restaurer les données ?", ORG, 130, [option("tested", "Oui, une procédure existe et a déjà été testée", 10, [effect("absolute", 100)]), option("partial", "Partiellement, mais sans procédure réellement testée", 20, [effect("absolute", 55)]), option("none", "Non, ce serait difficile ou incertain", 30, [effect("absolute", 15)])]),
    question("businessDependence", "continuity", "À quel point une panne informatique bloquerait-elle votre activité ?", ORG, 140, [option("low", "Peu : nous pouvons attendre", 10), option("medium", "Partiellement : certaines tâches seraient bloquées", 20), option("high", "Fortement : l'activité ou l'accueil seraient bloqués", 30, [effect("deduction", 15, [{ questionId: "continuity", operator: "not_equals", values: ["tested"] }])])]),
  ],
  levels: [
    { id: "good", label: "Bon", minimumScore: 85, order: 10, description: null },
    { id: "watch", label: "À surveiller", minimumScore: 70, order: 20, description: null },
    { id: "improve", label: "Améliorations recommandées", minimumScore: 45, order: 30, description: null },
    { id: "risk", label: "Risque important", minimumScore: 0, order: 40, description: null },
  ],
  maximumPriorities: 3,
  priorities: [
    { categoryId: "backup", threshold: 70, title: "Mettre en place une sauvegarde réellement récupérable", body: "La sauvegarde actuelle ne combine pas copie externalisée et restauration testée. Une synchronisation seule ne remplace pas cette protection.", order: 10 },
    { categoryId: "security", threshold: 70, title: "Renforcer les accès aux comptes importants", body: "La validation en deux étapes et des comptes individuels réduisent fortement le risque lié au phishing ou à un mot de passe compromis.", order: 20 },
    { categoryId: "network", threshold: 70, title: "Fiabiliser le Wi-Fi et les accès réseau", body: "Les réponses indiquent des coupures ou une couverture inégale qui peuvent gêner le travail au quotidien.", order: 30 },
    { categoryId: "equipment", threshold: 70, title: "Planifier les améliorations de matériel et de mises à jour", body: "Du matériel vieillissant, des lenteurs ou des mises à jour irrégulières augmentent les interruptions et les risques de sécurité.", order: 40 },
    { categoryId: "continuity", threshold: 70, title: "Préparer la reprise après incident", body: "Une procédure simple et testée évite d'improviser lorsque l'activité ou les données sont indisponibles.", order: 50 },
  ],
  positives: [
    { categoryId: "backup", threshold: 85, text: "Vos sauvegardes sont externalisées et leur restauration a déjà été vérifiée.", order: 10 },
    { categoryId: "security", threshold: 85, text: "Les protections de compte et les bonnes pratiques de sécurité sont déjà bien engagées.", order: 20 },
    { categoryId: "network", threshold: 85, text: "Le réseau et le Wi-Fi ne montrent pas de fragilité particulière dans vos réponses.", order: 30 },
    { categoryId: "continuity", threshold: 85, text: "Une procédure de reprise existe et a déjà été testée.", order: 40 },
    { categoryId: "equipment", threshold: 85, text: "Le matériel et le suivi des mises à jour semblent adaptés à l'usage déclaré.", order: 50 },
  ],
  contexts: [
    { id: "general", label: "Besoin à préciser", text: "Orientation générale", active: true, order: 10, allowsSelfService: true },
    { id: "backup", label: "Sauvegarde et protection des données", text: "Protection des fichiers", active: true, order: 20, allowsSelfService: true },
    { id: "remote-access", label: "Accès distant", text: "Accès privé", active: true, order: 30, allowsSelfService: true },
    { id: "network", label: "Réseau / Wi-Fi", text: "Cadrage réseau", active: true, order: 40, allowsSelfService: false },
    { id: "messaging", label: "Messagerie", text: "Cadrage messagerie", active: true, order: 50, allowsSelfService: false },
    { id: "domain-dns", label: "Domaine et DNS", text: "Cadrage domaine", active: true, order: 60, allowsSelfService: false },
    { id: "server", label: "Serveur ou VPS", text: "Cadrage serveur", active: true, order: 70, allowsSelfService: false },
    { id: "web-hosting", label: "Hébergement web", text: "Cadrage hébergement", active: true, order: 80, allowsSelfService: false },
  ],
  commerce: {
    minimumStorageGb: 1, maximumStorageGb: 256, minimumUsers: 1, maximumUsers: 11, maximumSites: 1,
    compatibleScopes: ["files", "windows"], humanReviewIntents: ["access_complex", "backup_complex", "unknown"],
    questions: [
      cQuestion("commercialIntent", "Quel résultat cherchez-vous principalement ?", ALL, ["general", "backup", "remote-access"], 10, [
        cOption("backup_simple", "Protéger simplement des fichiers ou documents", 10, ["individual"], ["general", "backup"]),
        cOption("remote_files", "Accéder à des fichiers privés à distance", 20, ALL, ["general", "remote-access"]),
        cOption("windows_desktop", "Utiliser un bureau Windows à distance", 30, ALL, ["general", "remote-access"]),
        cOption("team_files", "Protéger et partager les fichiers d'une petite structure", 40, ORG, ["general", "backup"]),
        cOption("team_windows", "Équiper une petite structure avec des bureaux Windows distants", 50, ORG, ["general", "remote-access"]),
        cOption("backup_complex", "Protéger un ordinateur complet, un NAS, un serveur ou plusieurs éléments", 60, ALL, ["general", "backup"]),
        cOption("access_complex", "Accéder à plusieurs applications, réseaux ou équipements", 70, ALL, ["general", "remote-access"]),
        cOption("unknown", "Je ne sais pas encore", 80, ALL, ["general"]),
      ]),
      cQuestion("commercialScope", "Quel élément correspond le mieux à ce besoin ?", ALL, ["general", "backup", "remote-access"], 20, [cOption("files", "Des fichiers et documents", 10), cOption("windows", "Un bureau Windows distant", 20), cOption("device_or_nas", "Un ordinateur complet ou un NAS", 30), cOption("server_or_multiple", "Un serveur ou plusieurs cibles", 40), cOption("unknown", "Je ne sais pas", 50)], "Cette précision permet de distinguer un besoin standard d'un besoin à cadrer."),
      cQuestion("commercialUsers", "Combien de personnes utiliseront ce service ?", ORG, ["general", "backup", "remote-access"], 30, [cOption("1", "1", 10, ORG), cOption("2", "2", 20, ORG)], null, "users"),
      cQuestion("commercialSites", "Combien de sites ou de locaux sont concernés ?", ORG, ["general", "backup", "remote-access"], 40, [cOption("one", "Un seul site", 10, ORG), cOption("several", "Plusieurs sites ou locaux", 20, ORG)]),
      cQuestion("commercialStorage", "Quel volume de fichiers faut-il prévoir environ ?", ALL, ["general", "backup", "remote-access"], 50, [cOption("16", "Jusqu'à 16 Go", 10), cOption("32", "Jusqu'à 32 Go", 20), cOption("64", "Jusqu'à 64 Go", 30), cOption("128", "Jusqu'à 128 Go", 40), cOption("256", "Jusqu'à 256 Go", 50), cOption("above-public-max", "Plus de 256 Go", 60), cOption("unknown", "Je ne sais pas", 70)], "Une estimation suffit. Au-delà des paliers publics ou sans estimation, un cadrage est préférable.", "storage"),
      cQuestion("commercialNetworkDetail", "Quel problème réseau ou Wi-Fi souhaitez-vous résoudre ?", ALL, ["network"], 60, [cOption("wifi", "Améliorer la couverture ou la stabilité du Wi-Fi", 10, ALL, ["network"]), cOption("separation", "Séparer les usages ou les équipements", 20, ALL, ["network"]), cOption("installation", "Préparer ou reprendre une installation", 30, ALL, ["network"])], "Ce type de besoin mérite d'être compris avant de proposer une offre en ligne."),
      cQuestion("commercialMessagingDetail", "Quel besoin de messagerie avez-vous ?", ALL, ["messaging"], 70, [cOption("migration", "Migrer ou reprendre une messagerie", 10, ALL, ["messaging"]), cOption("addresses", "Créer ou organiser des adresses professionnelles", 20, ALL, ["messaging"]), cOption("deliverability", "Résoudre un problème d'envoi ou de réception", 30, ALL, ["messaging"])], "Ce type de besoin mérite d'être compris avant de proposer une offre en ligne."),
      cQuestion("commercialDomainDetail", "Que souhaitez-vous faire avec votre domaine ou DNS ?", ALL, ["domain-dns"], 80, [cOption("access", "Retrouver ou transférer les accès", 10, ALL, ["domain-dns"]), cOption("configuration", "Raccorder le domaine à un site ou une messagerie", 20, ALL, ["domain-dns"]), cOption("incident", "Résoudre un incident en cours", 30, ALL, ["domain-dns"])], "Ce type de besoin mérite d'être compris avant de proposer une offre en ligne."),
      cQuestion("commercialServerDetail", "Quel besoin serveur ou VPS souhaitez-vous examiner ?", ALL, ["server"], 90, [cOption("new", "Mettre en place un nouveau serveur ou VPS", 10, ALL, ["server"]), cOption("takeover", "Reprendre, maintenir ou migrer un existant", 20, ALL, ["server"]), cOption("incident", "Résoudre un incident ou une indisponibilité", 30, ALL, ["server"])], "Ce type de besoin mérite d'être compris avant de proposer une offre en ligne."),
      cQuestion("commercialHostingDetail", "Quel sujet d'hébergement souhaitez-vous examiner ?", ALL, ["web-hosting"], 100, [cOption("site", "Créer, migrer ou maintenir un site", 10, ALL, ["web-hosting"]), cOption("wordpress", "Sécuriser ou maintenir WordPress", 20, ALL, ["web-hosting"]), cOption("access", "Retrouver les accès ou reprendre un hébergement", 30, ALL, ["web-hosting"])], "Ce type de besoin mérite d'être compris avant de proposer une offre en ligne."),
    ],
    profiles: [
      { id: "simple_backup", label: "Sauvegarde simple", active: true, intents: ["backup_simple"], scopes: ["files"] },
      { id: "vpn_access", label: "Accès distant privé", active: true, intents: ["remote_files"], scopes: ["files"] },
      { id: "windows_desktop", label: "Bureau Windows distant", active: true, intents: ["windows_desktop"], scopes: ["windows"] },
      { id: "team_or_structure", label: "Petite structure", active: true, intents: ["team_files"], scopes: ["files"] },
      { id: "team_windows_desktop", label: "Petite structure Windows", active: true, intents: ["team_windows"], scopes: ["windows"] },
    ],
    // Le diagnostic ne choisit ni un preset fixe ni un prix. Il exprime les
    // composants indispensables ; Billing V2 choisit ensuite le plus petit
    // palier public qui couvre le besoin de stockage.
    catalogBindings: [
      { profileId: "simple_backup", storageServiceCode: "STORAGE-PERSONAL", requiredServiceCodes: ["BASE-SERVICE", "STORAGE-PERSONAL", "BACKUP-PERSONAL"] },
      { profileId: "vpn_access", storageServiceCode: "STORAGE-PERSONAL", requiredServiceCodes: ["BASE-SERVICE", "STORAGE-PERSONAL", "BACKUP-PERSONAL", "VPN-ACCESS"] },
      { profileId: "windows_desktop", storageServiceCode: "STORAGE-PERSONAL", requiredServiceCodes: ["BASE-SERVICE", "STORAGE-PERSONAL", "BACKUP-PERSONAL", "VPN-ACCESS", "RDS-ACCESS"] },
      { profileId: "team_or_structure", storageServiceCode: "STORAGE-SHARED", requiredServiceCodes: ["BASE-SERVICE", "STORAGE-SHARED", "BACKUP-SHARED", "VPN-ACCESS", "USER-ADDITIONAL", "SUPPORT-PLUS"] },
      { profileId: "team_windows_desktop", storageServiceCode: "STORAGE-SHARED", requiredServiceCodes: ["BASE-SERVICE", "STORAGE-SHARED", "BACKUP-SHARED", "VPN-ACCESS", "RDS-ACCESS", "USER-ADDITIONAL", "SUPPORT-PLUS"] },
    ],
  },
};
