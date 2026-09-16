import type {
  BillingV2PublicCatalog,
  DiagnosticRecommendationProfileId,
  BillingV2PublicSelection,
  PreDiagnosticConfiguration,
} from "@kermaria/shared";

import type {
  PreDiagnosticProfile,
  PreDiagnosticQuestion,
} from "@/lib/pre-diagnostic";
import { buildBaselineSelection, findService } from "@/lib/billing-v2-formules";
import { MAX_ADDITIONAL_USERS } from "@/lib/billing-v2-selection";
import type { DiagnosticContextId } from "@/lib/diagnostic-context";

export type CommercialRecommendation = {
  kind: "standard" | "human_review";
  title: string;
  reason: string;
  /** Besoin metier, jamais une reference de prix ou un preset impose. */
  need: NormalizedDiagnosticNeed | null;
  /** Selection Billing V2 derivee du catalogue vivant. */
  selection: BillingV2PublicSelection | null;
  offerName: string | null;
  selectedStorageGb: number | null;
  commercialProfileId: DiagnosticRecommendationProfileId | null;
};

export type NormalizedDiagnosticNeed = {
  customerType: "personal" | "organisation";
  family: "storage" | "remote_access" | "windows_desktop";
  requiredStorageGb: number;
  requiredUsers: number;
  requiredSites: number;
  scope: "files" | "windows";
};

export const PRE_DIAGNOSTIC_HUMAN_REVIEW_CONTEXTS = [
  "network",
  "messaging",
  "domain-dns",
  "server",
  "web-hosting",
] as const satisfies readonly DiagnosticContextId[];

export const PRE_DIAGNOSTIC_COMMERCIAL_LIMITS = {
  maximumPublicStorageGb: 256,
  maximumPublicUsers: 11,
  requireSingleSiteForOrganisations: true,
} as const;

export type PreDiagnosticContextPresentation = {
  label: string;
  selfService: boolean;
  humanReviewReason: string | null;
};

const HUMAN_REVIEW_CONTEXTS = new Set<DiagnosticContextId>(
  PRE_DIAGNOSTIC_HUMAN_REVIEW_CONTEXTS,
);

export function isPreDiagnosticHumanReviewContext(
  context: DiagnosticContextId,
  configuration?: PreDiagnosticConfiguration,
) {
  if (configuration) {
    const configured = configuration.contexts.find((item) => item.id === context && item.active);
    return configured ? !configured.allowsSelfService : true;
  }
  return HUMAN_REVIEW_CONTEXTS.has(context);
}

function contextAllowsSelfService(
  context: DiagnosticContextId,
  configuration?: PreDiagnosticConfiguration,
) {
  return !isPreDiagnosticHumanReviewContext(context, configuration);
}

/**
 * Présentation des contextes réellement interprétés par le pré-diagnostic.
 * Les questions restent construites par `commercialQuestionsForProfile`, ce
 * qui évite à l'administration de maintenir une seconde liste.
 */
export function getPreDiagnosticContextPresentation(
  context: DiagnosticContextId,
): PreDiagnosticContextPresentation {
  const labels: Record<DiagnosticContextId, string> = {
    general: "Besoin à préciser",
    backup: "Sauvegarde et protection des données",
    "remote-access": "Accès distant",
    network: "Réseau / Wi-Fi",
    messaging: "Messagerie",
    "domain-dns": "Domaine et DNS",
    server: "Serveur ou VPS",
    "web-hosting": "Hébergement web",
  };
  const humanReview = isPreDiagnosticHumanReviewContext(context);

  return {
    label: labels[context],
    selfService: !humanReview,
    humanReviewReason: humanReview
      ? "Ce contexte force un cadrage humain avant toute proposition commerciale."
      : null,
  };
}

const STORAGE_OPTIONS = [
  { value: "16", label: "Jusqu'à 16 Go" },
  { value: "32", label: "Jusqu'à 32 Go" },
  { value: "64", label: "Jusqu'à 64 Go" },
  { value: "128", label: "Jusqu'à 128 Go" },
  { value: "256", label: "Jusqu'à 256 Go" },
  { value: "above-public-max", label: "Plus de 256 Go" },
  { value: "unknown", label: "Je ne sais pas" },
] as const;

function storageOptions(maximum: number) {
  const options: { value: string; label: string }[] = STORAGE_OPTIONS.filter((option) => {
    const value = Number(option.value);
    return !Number.isFinite(value) || value <= maximum;
  });
  if (!options.some((option) => Number(option.value) === maximum)) {
    options.push({ value: String(maximum), label: `Jusqu'à ${maximum} Go` });
  }
  return [...options, { value: "above-public-max", label: `Plus de ${maximum} Go` }, { value: "unknown", label: "Je ne sais pas" }]
    .filter((option, index, values) => values.findIndex((item) => item.value === option.value) === index);
}

function userOptions(maximum: number) {
  return [
    ...Array.from({ length: maximum }, (_, index) => ({ value: String(index + 1), label: String(index + 1) })),
    { value: `${maximum + 1}-plus`, label: `${maximum + 1} ou plus` },
  ];
}

export function commercialQuestionsForProfile(
  profile: PreDiagnosticProfile | null,
  context: DiagnosticContextId,
  configuration?: PreDiagnosticConfiguration,
): readonly PreDiagnosticQuestion[] {
  if (!profile) return [];

  // Une v2 publiee decrit integralement les textes, options et visibilites de
  // qualification. Le fallback historique ci-dessous ne sert qu'avant la
  // premiere publication v2 afin de ne pas changer le parcours valide.
  if (configuration) {
    return configuration.commerce.questions
      .filter((question) => question.active
        && question.profiles.includes(profile)
        && question.contexts.includes(context))
      .sort((left, right) => left.order - right.order)
      .map((question) => ({
        id: question.id,
        category: "Votre besoin",
        label: question.label,
        hint: question.hint ?? undefined,
        options: configuredCommercialOptions(question, profile, context, configuration),
      }));
  }

  if (isPreDiagnosticHumanReviewContext(context, configuration)) {
    return [
      {
        id: "commercialContextDetail",
        category: "Votre besoin",
        label: contextQuestionLabel(context),
        hint: "Ce type de besoin mérite d'être compris avant de proposer une offre en ligne.",
        options: contextQuestionOptions(context),
      },
    ];
  }

  const organisation = profile !== "individual";
  const maximumUsers = PRE_DIAGNOSTIC_COMMERCIAL_LIMITS.maximumPublicUsers;
  const maximumStorage = PRE_DIAGNOSTIC_COMMERCIAL_LIMITS.maximumPublicStorageGb;
  const intentOptions = context === "backup"
    ? organisation
      ? [
          { value: "team_files", label: "Protéger les fichiers et documents de notre structure" },
          { value: "backup_complex", label: "Protéger un ordinateur complet, un NAS, un serveur ou plusieurs éléments" },
        ]
      : [
          { value: "backup_simple", label: "Protéger simplement des fichiers et documents" },
          { value: "backup_complex", label: "Protéger un ordinateur complet, un NAS, un serveur ou plusieurs éléments" },
        ]
    : context === "remote-access"
      ? [
          { value: "remote_files", label: organisation ? "Accéder aux fichiers de notre structure à distance" : "Retrouver des fichiers privés à distance" },
          { value: "windows_desktop", label: organisation ? "Utiliser des bureaux Windows complets à distance" : "Utiliser un bureau Windows complet à distance" },
          { value: "access_complex", label: "Accéder à plusieurs applications, réseaux ou équipements" },
        ]
      : [
          { value: "backup_simple", label: "Protéger simplement mes fichiers ou documents" },
          { value: "remote_files", label: "Accéder à des fichiers privés à distance" },
          { value: "windows_desktop", label: "Utiliser un bureau Windows à distance" },
          ...(organisation ? [
            { value: "team_files", label: "Protéger et partager les fichiers d'une petite structure" },
            { value: "team_windows", label: "Équiper une petite structure avec des bureaux Windows distants" },
          ] : []),
          { value: "access_complex", label: "Traiter un réseau, un serveur, un NAS ou plusieurs besoins" },
          { value: "unknown", label: "Je ne sais pas encore" },
        ];

  const questions: PreDiagnosticQuestion[] = [{
    id: "commercialIntent",
    category: "Votre besoin",
    label: "Quel résultat cherchez-vous principalement ?",
    options: intentOptions,
  }];

  questions.push({
    id: "commercialScope",
    category: "Votre besoin",
    label: "Quel élément correspond le mieux à ce besoin ?",
    hint: "Cette précision permet de distinguer un besoin standard d'un besoin à cadrer.",
    options: [
      { value: "files", label: "Des fichiers et documents" },
      { value: "windows", label: "Un bureau Windows distant" },
      { value: "device_or_nas", label: "Un ordinateur complet ou un NAS" },
      { value: "server_or_multiple", label: "Un serveur ou plusieurs cibles" },
      { value: "unknown", label: "Je ne sais pas" },
    ],
  });

  if (organisation) {
    questions.push({
      id: "commercialUsers",
      category: "Votre besoin",
      label: "Combien de personnes utiliseront ce service ?",
      options: userOptions(maximumUsers),
    });
    questions.push({
      id: "commercialSites",
      category: "Votre besoin",
      label: "Combien de sites ou de locaux sont concernés ?",
      options: [
        { value: "one", label: "Un seul site" },
        { value: "several", label: "Plusieurs sites ou locaux" },
      ],
    });
  }

  questions.push({
    id: "commercialStorage",
    category: "Votre besoin",
    label: "Quel volume de fichiers faut-il prévoir environ ?",
    hint: "Une estimation suffit. Au-delà des paliers publics ou sans estimation, un cadrage est préférable.",
    options: storageOptions(maximumStorage),
  });

  return questions;
}

function configuredCommercialOptions(
  question: PreDiagnosticConfiguration["commerce"]["questions"][number],
  profile: PreDiagnosticProfile,
  context: DiagnosticContextId,
  configuration: PreDiagnosticConfiguration,
) {
  if (question.dynamicOptions === "users") {
    return userOptions(configuration.commerce.maximumUsers);
  }
  if (question.dynamicOptions === "storage") {
    const configured = question.options
      .filter((option) => option.active && option.profiles.includes(profile) && option.contexts.includes(context));
    const numeric = configured
      .filter((option) => Number.isFinite(Number(option.value)) && Number(option.value) <= configuration.commerce.maximumStorageGb)
      .sort((left, right) => Number(left.value) - Number(right.value))
      .map(({ value, label }) => ({ value, label }));
    if (!numeric.some((option) => Number(option.value) === configuration.commerce.maximumStorageGb)) {
      numeric.push({ value: String(configuration.commerce.maximumStorageGb), label: `Jusqu'à ${configuration.commerce.maximumStorageGb} Go` });
    }
    const special = configured
      .filter((option) => !Number.isFinite(Number(option.value)))
      .map(({ value, label }) => ({ value, label }));
    return [...numeric, ...special];
  }
  return question.options
    .filter((option) => option.active && option.profiles.includes(profile) && option.contexts.includes(context))
    .sort((left, right) => left.order - right.order)
    .map(({ value, label }) => ({ value, label }));
}

export function recommendPreDiagnosticOffer(
  answers: Record<string, string>,
  profile: PreDiagnosticProfile,
  context: DiagnosticContextId,
  catalog: BillingV2PublicCatalog,
  configuration?: PreDiagnosticConfiguration,
): CommercialRecommendation {
  if (!contextAllowsSelfService(context, configuration)) {
    return humanReview(
      "Ce sujet demande une vérification avant toute proposition commerciale.",
    );
  }

  const intent = answers.commercialIntent;
  const scope = answers.commercialScope;
  const commerce = configuration?.commerce;
  const storage = readStorage(
    answers.commercialStorage,
    commerce?.maximumStorageGb,
    commerce?.minimumStorageGb,
  );
  const organisation = profile !== "individual";
  const users = organisation
    ? readUsers(answers.commercialUsers, commerce?.maximumUsers, commerce?.minimumUsers)
    : 1;
  const expectedScope = commerce?.profiles.find((item) => item.intents.includes(intent))
    ?.scopes[0] ?? (intent === "windows_desktop" || intent === "team_windows" ? "windows" : "files");

  if (
    !intent
    || (commerce?.humanReviewIntents ?? ["access_complex", "backup_complex", "unknown"]).includes(intent)
    || (commerce !== undefined && !commerce.compatibleScopes.includes(scope))
    || scope !== expectedScope
    || storage === null
    || users === null
    // Garde-fou technique du contrat de selection Billing V2 : l'admin peut
    // assouplir sa limite de qualification, pas fabriquer une selection que
    // le moteur de souscription refuserait ensuite.
    || users - 1 > MAX_ADDITIONAL_USERS
    || (
      organisation
      && (commerce?.maximumSites ?? 1) <= 1
      && answers.commercialSites !== "one"
    )
  ) {
    return humanReview(
      "Vos réponses décrivent un besoin qui mérite un cadrage plutôt qu'une formule automatique.",
    );
  }

  const commercialProfile = commerce?.profiles.find((item) => item.active
    && item.intents.includes(intent)
    && item.scopes.includes(scope));
  if (!commercialProfile) {
    return humanReview(
      "Aucune formule standard ne représente honnêtement le besoin indiqué.",
    );
  }

  const need = normalizeNeed(intent, profile, scope, storage, users);
  const binding = commerce?.catalogBindings.find(
    (item) => item.profileId === commercialProfile.id,
  );
  if (!binding) {
    return humanReview(
      "Aucune correspondance catalogue n'est configurée pour ce besoin.",
    );
  }

  const storageService = findService(catalog, binding.storageServiceCode);
  const storageTier = storageService?.tiers
    .filter((tier) => tier.publicSelectable && tier.numericValue !== null
      && tier.numericValue >= need.requiredStorageGb)
    .sort((left, right) => (left.numericValue ?? 0) - (right.numericValue ?? 0))[0] ?? null;
  if (!storageTier || storageTier.numericValue === null) {
    return humanReview(
      "Aucun palier public ne couvre le volume de stockage demandé.",
    );
  }

  // Le preset n'est plus un mapping admin : il est recherche dans le
  // catalogue selon les composants necessaires au besoin. La capacite retenue
  // est ensuite le plus petit palier suffisant du service de stockage lie.
  const requiredServices = new Set(binding.requiredServiceCodes);
  const offer = catalog.presets
    .filter((preset) => [...requiredServices].every((serviceCode) =>
      preset.items.some((item) => item.serviceCode === serviceCode)))
    .sort((left, right) => left.displayOrder - right.displayOrder)[0] ?? null;
  const commitment = catalog.commitments.find((item) => item.code === "FLEX"
    && item.paymentOptions.some((option) => option.paymentMode === "monthly"))
    ?? catalog.commitments.find((item) => item.months <= 1
      && item.paymentOptions.some((option) => option.paymentMode === "monthly"))
    ?? null;
  if (!offer || !commitment) {
    return humanReview(
      "Aucune offre publique active ne contient tous les composants nécessaires.",
    );
  }

  const baseline = buildBaselineSelection(offer, commitment.code);
  const selection: BillingV2PublicSelection = {
    ...baseline,
    paymentMode: "monthly",
    // Les deux champs sont imposes par le contrat Billing V2 historique.
    // La configuration choisit le service cible ; aucun palier n'est code ici.
    storagePersonalTierCode: binding.storageServiceCode === "STORAGE-PERSONAL"
      ? storageTier.code
      : baseline.storagePersonalTierCode,
    storageSharedTierCode: binding.storageServiceCode === "STORAGE-SHARED"
      ? storageTier.code
      : baseline.storageSharedTierCode,
    additionalUsers: Math.max(0, need.requiredUsers - 1),
  };

  return {
    kind: "standard",
    title: "Une formule correspond à votre besoin",
    reason: commercialReason(intent, organisation),
    need,
    selection,
    offerName: offer.name,
    selectedStorageGb: storageTier.numericValue,
    commercialProfileId: commercialProfile.id,
  };
}

export function normalizeNeed(
  intent: string,
  profile: PreDiagnosticProfile,
  scope: string,
  storage: number,
  users: number,
): NormalizedDiagnosticNeed {
  return {
    customerType: profile === "individual" ? "personal" : "organisation",
    family: intent === "remote_files"
      ? "remote_access"
      : intent === "windows_desktop" || intent === "team_windows"
        ? "windows_desktop"
        : "storage",
    requiredStorageGb: storage,
    requiredUsers: users,
    requiredSites: 1,
    scope: scope === "windows" ? "windows" : "files",
  };
}

function readStorage(value: string | undefined, maximum: number = PRE_DIAGNOSTIC_COMMERCIAL_LIMITS.maximumPublicStorageGb, minimum: number = 1): number | null {
  if (!value || value === "unknown" || value === "above-public-max") return null;
  const parsed = Number(value);
  return Number.isInteger(parsed)
    && parsed >= minimum
    && parsed <= maximum
    ? parsed
    : null;
}

function readUsers(value: string | undefined, maximum: number = PRE_DIAGNOSTIC_COMMERCIAL_LIMITS.maximumPublicUsers, minimum: number = 1): number | null {
  if (!value || value.endsWith("-plus")) return null;
  const parsed = Number(value);
  return Number.isInteger(parsed)
    && parsed >= minimum
    && parsed <= maximum
    ? parsed
    : null;
}

function humanReview(reason: string): CommercialRecommendation {
  return {
    kind: "human_review",
    title: "Ce besoin mérite un échange avant de choisir une offre",
    reason,
    need: null,
    selection: null,
    offerName: null,
    selectedStorageGb: null,
    commercialProfileId: null,
  };
}

function commercialReason(intent: string, organisation: boolean) {
  if (intent === "remote_files") return "Vous avez indiqué un accès privé et simple à des fichiers à distance.";
  if (intent === "windows_desktop" || intent === "team_windows") return "Vous recherchez un environnement Windows distant dans un cadre standard.";
  if (organisation) return "Vous avez indiqué un besoin de petite structure, sur un seul site et dans les limites du catalogue public.";
  return "Vous souhaitez protéger des fichiers avec un volume compatible avec les paliers disponibles en ligne.";
}

function contextQuestionLabel(context: DiagnosticContextId) {
  return context === "web-hosting"
    ? "Quel sujet d'hébergement souhaitez-vous examiner ?"
    : context === "network"
      ? "Quel problème réseau ou Wi-Fi souhaitez-vous résoudre ?"
      : context === "messaging"
        ? "Quel besoin de messagerie avez-vous ?"
        : context === "domain-dns"
          ? "Que souhaitez-vous faire avec votre domaine ou DNS ?"
          : "Quel besoin serveur ou VPS souhaitez-vous examiner ?";
}

function contextQuestionOptions(context: DiagnosticContextId) {
  if (context === "web-hosting") return [
    { value: "site", label: "Créer, migrer ou maintenir un site" },
    { value: "wordpress", label: "Sécuriser ou maintenir WordPress" },
    { value: "access", label: "Retrouver les accès ou reprendre un hébergement" },
  ];
  if (context === "network") return [
    { value: "wifi", label: "Améliorer la couverture ou la stabilité du Wi-Fi" },
    { value: "separation", label: "Séparer les usages ou les équipements" },
    { value: "installation", label: "Préparer ou reprendre une installation" },
  ];
  if (context === "messaging") return [
    { value: "migration", label: "Migrer ou reprendre une messagerie" },
    { value: "addresses", label: "Créer ou organiser des adresses professionnelles" },
    { value: "deliverability", label: "Résoudre un problème d'envoi ou de réception" },
  ];
  if (context === "domain-dns") return [
    { value: "access", label: "Retrouver ou transférer les accès" },
    { value: "configuration", label: "Raccorder le domaine à un site ou une messagerie" },
    { value: "incident", label: "Résoudre un incident en cours" },
  ];
  return [
    { value: "new", label: "Mettre en place un nouveau serveur ou VPS" },
    { value: "takeover", label: "Reprendre, maintenir ou migrer un existant" },
    { value: "incident", label: "Résoudre un incident ou une indisponibilité" },
  ];
}
