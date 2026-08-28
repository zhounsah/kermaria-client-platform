import type {
  BillingV2PublicCatalog,
  DiagnosticAnswers,
  DiagnosticBillingMappingConfig,
  DiagnosticConditionConfig,
  DiagnosticConfiguration,
  DiagnosticRecommendation,
  DiagnosticRecommendationConfig,
} from "@kermaria/shared";

import {
  DEFAULT_DIAGNOSTIC_CONFIGURATION,
  resolveDiagnosticContextConfig,
  type DiagnosticAnswerMap,
  type DiagnosticContextId,
} from "@/lib/diagnostic-context";
import { recommendOffer } from "@/lib/public-diagnostic";

export type AdaptiveDiagnosticGuidance = {
  title: string;
  body: string;
  points: readonly string[];
};

export type AdaptiveDiagnosticOutcome = {
  guidance: AdaptiveDiagnosticGuidance;
  recommendation: DiagnosticRecommendation | null;
  /**
   * Identifiants des regles reellement appliquees. Le simulateur
   * d'administration les affiche pour rendre la decision explicable ; le
   * parcours public les ignore.
   */
  appliedRuleIds: readonly string[];
};

/**
 * Constantes du contrat Billing V2 que le diagnostic ne cherche pas a deviner.
 * Les rendre administrables reviendrait a laisser un texte decider d'une
 * severite commerciale sans qu'aucune question ne l'ait mesuree.
 */
const FIXED_ANSWER_DEFAULTS = {
  recoveryImportance: "normal",
  backupFrequency: "unknown",
  continuityPlan: "unknown",
} as const satisfies Pick<
  DiagnosticAnswers,
  "recoveryImportance" | "backupFrequency" | "continuityPlan"
>;

export function canContextProduceFormula(
  context: DiagnosticContextId,
  configuration: DiagnosticConfiguration = DEFAULT_DIAGNOSTIC_CONFIGURATION,
): boolean {
  const definition = resolveDiagnosticContextConfig(context, configuration);
  return definition.formulaEligible && definition.billingMapping !== null;
}

export function buildAdaptiveDiagnosticOutcome(
  context: DiagnosticContextId,
  answers: DiagnosticAnswerMap,
  catalog: BillingV2PublicCatalog,
  recommendationConfig?: DiagnosticRecommendationConfig,
  configuration: DiagnosticConfiguration = DEFAULT_DIAGNOSTIC_CONFIGURATION,
): AdaptiveDiagnosticOutcome {
  const definition = resolveDiagnosticContextConfig(context, configuration);
  const guidanceRule = definition.guidance.find((rule) =>
    matchesAll(rule.when, answers));
  const billingAnswers = definition.formulaEligible
    ? buildBillingAnswers(definition.billingMapping, answers)
    : null;

  return {
    guidance: guidanceRule
      ? {
          title: guidanceRule.title,
          body: guidanceRule.body,
          points: guidanceRule.points,
        }
      // Une configuration sans regle inconditionnelle ne doit pas produire un
      // ecran vide : le repli reste le texte d'orientation generique.
      : {
          title: "Votre besoin doit d'abord être orienté vers le bon sujet.",
          body: "Choisissez le problème principal pour obtenir des questions réellement pertinentes.",
          points: [],
        },
    recommendation: billingAnswers
      ? recommendOffer(billingAnswers, catalog, recommendationConfig)
      : null,
    appliedRuleIds: guidanceRule ? [guidanceRule.id] : [],
  };
}

/** Evalue une condition de la DSL fermee. Aucun autre operateur n'existe. */
export function evaluateDiagnosticCondition(
  condition: DiagnosticConditionConfig,
  answers: DiagnosticAnswerMap,
): boolean {
  const raw = answers[condition.questionId];
  const single = typeof raw === "string" ? raw : null;
  const multi = Array.isArray(raw) ? [...raw] : single !== null ? [single] : [];

  switch (condition.operator) {
    case "equals":
      return single !== null && single === condition.values[0];
    case "not_equals":
      // Une question sans reponse n'est pas egale a la valeur interdite : la
      // condition tient, comme dans le code d'origine.
      return single === null || !condition.values.includes(single);
    case "one_of":
      return single !== null && condition.values.includes(single);
    case "includes":
      return condition.values.some((value) => multi.includes(value));
    case "only":
      return multi.length === condition.values.length
        && condition.values.every((value) => multi.includes(value));
    case "answered":
      return multi.length > 0;
    default:
      // Operateur inconnu : refus ferme, jamais une acceptation par defaut.
      return false;
  }
}

function matchesAll(
  conditions: readonly DiagnosticConditionConfig[],
  answers: DiagnosticAnswerMap,
): boolean {
  return conditions.every((condition) =>
    evaluateDiagnosticCondition(condition, answers));
}

function buildBillingAnswers(
  mapping: DiagnosticBillingMappingConfig | null,
  answers: DiagnosticAnswerMap,
): DiagnosticAnswers | null {
  if (!mapping || !matchesAll(mapping.requireAll, answers)) return null;

  const users = readUsers(answers, mapping.usersQuestionId);
  const customerType = readCustomerType(answers, mapping.structureQuestionId);
  if (users === null || customerType === null) return null;

  const storage = readStorage(answers, mapping.storageQuestionId);
  if (storage === undefined) return null;

  return {
    customerType,
    users,
    dataKinds: [
      customerType === "individual"
        ? mapping.individualDataKind
        : mapping.organisationDataKind,
    ] as DiagnosticAnswers["dataKinds"],
    estimatedStorageGb: storage,
    needsRemoteFiles: matchesOptional(mapping.needsRemoteFilesWhen, answers),
    needsVpn: matchesOptional(mapping.needsVpnWhen, answers),
    needsWindowsDesktop: matchesOptional(mapping.needsWindowsDesktopWhen, answers),
    recoveryImportance: FIXED_ANSWER_DEFAULTS.recoveryImportance,
    backupFrequency: FIXED_ANSWER_DEFAULTS.backupFrequency,
    restoreTestRecency: readRestoreTest(answers, mapping.restoreTestQuestionId),
    continuityPlan: FIXED_ANSWER_DEFAULTS.continuityPlan,
  };
}

/** Absence de conditions = besoin non exprime, donc `false`. */
function matchesOptional(
  conditions: readonly DiagnosticConditionConfig[] | null,
  answers: DiagnosticAnswerMap,
): boolean {
  return conditions !== null && conditions.length > 0
    && matchesAll(conditions, answers);
}

function readSingle(answers: DiagnosticAnswerMap, id: string): string | null {
  const value = answers[id];
  return typeof value === "string" ? value : null;
}

function readUsers(
  answers: DiagnosticAnswerMap,
  questionId: string | null,
): number | null {
  if (!questionId) return 1;
  const raw = readSingle(answers, questionId);
  if (!raw || raw === "unknown") return null;
  if (raw === "12-plus") return 12;
  const users = Number(raw);
  return Number.isInteger(users) && users >= 1 && users <= 11 ? users : null;
}

function readCustomerType(
  answers: DiagnosticAnswerMap,
  questionId: string | null,
): DiagnosticAnswers["customerType"] | null {
  if (!questionId) return null;
  const structure = readSingle(answers, questionId);
  return structure === "individual"
    || structure === "business"
    || structure === "association"
    || structure === "other"
    ? structure
    : null;
}

/** `undefined` signifie « reponse attendue mais absente », donc pas de formule. */
function readStorage(
  answers: DiagnosticAnswerMap,
  questionId: string | null,
): DiagnosticAnswers["estimatedStorageGb"] | undefined {
  if (!questionId) return null;
  const raw = readSingle(answers, questionId);
  if (!raw) return undefined;
  if (raw === "unknown") return null;
  if (raw === "above-public-max") return "above_public_max";
  const value = Number(raw);
  return Number.isFinite(value) && value > 0 ? value : undefined;
}

function readRestoreTest(
  answers: DiagnosticAnswerMap,
  questionId: string | null,
): DiagnosticAnswers["restoreTestRecency"] {
  if (!questionId) return "unknown";
  switch (readSingle(answers, questionId)) {
    case "recent":
      return "less_than_12_months";
    case "old":
      return "more_than_12_months";
    case "never":
      return "never";
    default:
      return "unknown";
  }
}
