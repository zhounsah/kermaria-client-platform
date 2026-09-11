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
  profiles?: readonly PreDiagnosticProfile[];
  options: readonly { value: string; label: string }[];
};

const ALL_PROFILES = PRE_DIAGNOSTIC_PROFILES;
const ORGANISATIONS: readonly PreDiagnosticProfile[] = ["professional", "association"];

export const PRE_DIAGNOSTIC_QUESTIONS: readonly PreDiagnosticQuestion[] = [
  {
    id: "profile", category: "Profil", label: "Ce diagnostic concerne…", options: [
      { value: "individual", label: "Mon informatique personnelle" },
      { value: "professional", label: "Mon entreprise / activité professionnelle" },
      { value: "association", label: "Une association" },
    ],
  },
  {
    id: "equipmentCount", category: "Équipement", label: "Combien d'appareils sont concernés environ ?", profiles: ALL_PROFILES, options: [
      { value: "1-2", label: "1 à 2" }, { value: "3-5", label: "3 à 5" }, { value: "6+", label: "6 ou plus" },
    ],
  },
  {
    id: "equipmentAge", category: "Équipement", label: "Quel est l'âge moyen du matériel ?", profiles: ALL_PROFILES, options: [
      { value: "under3", label: "Moins de 3 ans" }, { value: "3to5", label: "Entre 3 et 5 ans" }, { value: "over5", label: "Plus de 5 ans ou très variable" },
    ],
  },
  {
    id: "performance", category: "Équipement", label: "Rencontrez-vous des lenteurs, blocages ou coupures ?", profiles: ALL_PROFILES, options: [
      { value: "none", label: "Rarement ou jamais" }, { value: "occasional", label: "Par moments" }, { value: "frequent", label: "Souvent" },
    ],
  },
  {
    id: "updates", category: "Équipement", label: "Les mises à jour sont-elles suivies ?", profiles: ALL_PROFILES, options: [
      { value: "automatic", label: "Oui, elles sont généralement automatiques" }, { value: "manual", label: "Oui, mais de façon irrégulière" }, { value: "unknown", label: "Je ne sais pas" },
    ],
  },
  {
    id: "backup", category: "Sauvegardes", label: "Comment vos données importantes sont-elles sauvegardées ?", hint: "Une synchronisation OneDrive ou Google Drive seule ne protège pas forcément d'une suppression, d'une erreur ou d'un ransomware.", profiles: ALL_PROFILES, options: [
      { value: "automatic_external_tested", label: "Automatiquement hors site, avec une restauration déjà testée" },
      { value: "automatic_external", label: "Automatiquement hors site, sans test récent" },
      { value: "automatic_same_site", label: "Automatiquement, mais sur le même lieu ou appareil" },
      { value: "cloud_sync_only", label: "Uniquement via OneDrive, Google Drive ou équivalent" },
      { value: "manual", label: "Manuellement de temps en temps" },
      { value: "none", label: "Pas de sauvegarde connue" },
    ],
  },
  {
    id: "network", category: "Réseau / Wi-Fi", label: "Votre connexion et votre Wi-Fi sont-ils fiables ?", profiles: ALL_PROFILES, options: [
      { value: "stable", label: "Oui, sans difficulté notable" }, { value: "occasional", label: "Quelques coupures ou lenteurs" }, { value: "frequent", label: "Des problèmes fréquents" },
    ],
  },
  {
    id: "wifiCoverage", category: "Réseau / Wi-Fi", label: "La couverture Wi-Fi est-elle suffisante là où vous travaillez ?", profiles: ALL_PROFILES, options: [
      { value: "good", label: "Oui" }, { value: "some_areas", label: "Pas dans toutes les zones" }, { value: "poor", label: "Non, elle gêne régulièrement" },
    ],
  },
  {
    id: "guestWifi", category: "Réseau / Wi-Fi", label: "Le Wi-Fi visiteurs est-il séparé des appareils de travail ?", profiles: ORGANISATIONS, options: [
      { value: "separate", label: "Oui" }, { value: "same", label: "Non ou je ne sais pas" },
    ],
  },
  {
    id: "mfa", category: "Sécurité", label: "La validation en deux étapes est-elle activée sur les comptes importants ?", profiles: ALL_PROFILES, options: [
      { value: "all", label: "Oui, sur les comptes importants" }, { value: "some", label: "Seulement sur certains comptes" }, { value: "none", label: "Non ou je ne sais pas comment faire" },
    ],
  },
  {
    id: "sharedAccounts", category: "Sécurité", label: "Des comptes ou mots de passe sont-ils partagés entre plusieurs personnes ?", profiles: ALL_PROFILES, options: [
      { value: "no", label: "Non" }, { value: "some", label: "Parfois" }, { value: "yes", label: "Oui, régulièrement" },
    ],
  },
  {
    id: "phishing", category: "Sécurité", label: "Les personnes concernées savent-elles reconnaître un e-mail ou lien suspect ?", profiles: ALL_PROFILES, options: [
      { value: "aware", label: "Oui, globalement" }, { value: "unsure", label: "Pas toujours" }, { value: "no", label: "C'est un sujet peu connu" },
    ],
  },
  {
    id: "continuity", category: "Continuité", label: "En cas de panne, pouvez-vous reprendre l'activité ou restaurer les données ?", profiles: ORGANISATIONS, options: [
      { value: "tested", label: "Oui, une procédure existe et a déjà été testée" }, { value: "partial", label: "Partiellement, mais sans procédure réellement testée" }, { value: "none", label: "Non, ce serait difficile ou incertain" },
    ],
  },
  {
    id: "businessDependence", category: "Continuité", label: "À quel point une panne informatique bloquerait-elle votre activité ?", profiles: ORGANISATIONS, options: [
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
): PreDiagnosticAnswers {
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
