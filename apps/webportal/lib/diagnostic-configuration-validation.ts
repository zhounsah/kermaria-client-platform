import {
  DIAGNOSTIC_CONDITION_OPERATORS,
  type DiagnosticBillingMappingConfig,
  type DiagnosticConditionConfig,
  type DiagnosticConfiguration,
  type DiagnosticContextConfig,
  type DiagnosticGuidanceRuleConfig,
  type DiagnosticQuestionConfig,
} from "@kermaria/shared";

import { DIAGNOSTIC_CONTEXT_IDS } from "@/lib/diagnostic-context";

/**
 * Validation de la DSL du diagnostic. Elle reproduit exactement le registre
 * ferme d'API-INTERNAL (`DiagnosticConfigurationRegistry`) : l'editeur
 * d'administration signale les erreurs immediatement, et une configuration
 * publiee mais corrompue est ecartee avant d'atteindre le parcours public.
 *
 * L'API reste l'autorite : cette fonction ne remplace pas sa validation, elle
 * evite un aller-retour inutile et un rendu public incoherent.
 */
export type DiagnosticConfigurationValidation = {
  configuration: DiagnosticConfiguration | null;
  errors: string[];
};

const SCHEMA_VERSION = 1;
const DATA_KINDS = new Set([
  "personal_documents",
  "business_documents",
  "photos",
  "association_data",
  "work_files",
  "other_important_files",
]);
const OPERATORS = new Set<string>(DIAGNOSTIC_CONDITION_OPERATORS);
const IDENTIFIER = /^[a-z][a-z0-9-]{1,63}$/;
const OPTION_VALUE = /^[a-z0-9][a-z0-9_-]{0,63}$/;
const RULE_ID = /^[A-Z][A-Z0-9-]{2,63}$/;

type QuestionValues = Map<string, Set<string>>;

export function validateDiagnosticConfiguration(
  value: unknown,
): DiagnosticConfigurationValidation {
  const errors: string[] = [];
  if (!isRecord(value)) {
    return { configuration: null, errors: ["Configuration absente."] };
  }

  if (value.schemaVersion !== SCHEMA_VERSION) {
    errors.push(`schemaVersion doit valoir ${SCHEMA_VERSION}.`);
  }

  const contexts = Array.isArray(value.contexts) ? value.contexts : [];
  const seen = new Set<string>();
  for (const context of contexts) {
    validateContext(context, seen, errors);
  }

  for (const expected of DIAGNOSTIC_CONTEXT_IDS) {
    if (!seen.has(expected)) errors.push(`Contexte manquant : ${expected}.`);
  }

  return errors.length > 0
    ? { configuration: null, errors }
    : { configuration: value as unknown as DiagnosticConfiguration, errors: [] };
}

function validateContext(value: unknown, seen: Set<string>, errors: string[]): void {
  if (!isRecord(value)) {
    errors.push("Contexte vide.");
    return;
  }

  const id = typeof value.id === "string" ? value.id : "";
  if (!(DIAGNOSTIC_CONTEXT_IDS as readonly string[]).includes(id)) {
    errors.push(`Contexte inconnu : ${describe(id)}.`);
    return;
  }

  if (seen.has(id)) {
    errors.push(`Contexte en double : ${id}.`);
    return;
  }
  seen.add(id);

  const context = value as unknown as DiagnosticContextConfig;
  requireText(context.label, 2, 80, `${id}.label`, errors);
  requireText(context.eyebrow, 2, 120, `${id}.eyebrow`, errors);
  requireText(context.title, 5, 200, `${id}.title`, errors);
  requireText(context.intro, 10, 1000, `${id}.intro`, errors);
  requireText(context.contactSubject, 5, 200, `${id}.contactSubject`, errors);

  const questions = Array.isArray(context.questions) ? context.questions : [];
  if (questions.length > 30) errors.push(`${id} : 30 questions au maximum.`);

  // Les options connues sont accumulees question par question : une condition
  // ne peut viser qu'une question declaree avant elle.
  const known: QuestionValues = new Map();
  for (const question of questions) validateQuestion(id, question, known, errors);

  validateGuidance(
    id,
    Array.isArray(context.guidance) ? context.guidance : [],
    known,
    errors,
  );

  const mapping = context.billingMapping ?? null;
  if (mapping !== null && context.formulaEligible !== true) {
    errors.push(`${id} : une correspondance Billing V2 exige formulaEligible = true.`);
  }
  if (mapping !== null) validateBillingMapping(id, mapping, known, errors);
}

function validateQuestion(
  contextId: string,
  value: unknown,
  known: QuestionValues,
  errors: string[],
): void {
  if (!isRecord(value)) {
    errors.push(`${contextId} : question vide.`);
    return;
  }

  const question = value as unknown as DiagnosticQuestionConfig;
  const questionId = typeof question.id === "string" ? question.id : "";
  if (!IDENTIFIER.test(questionId)) {
    errors.push(
      `${contextId} : identifiant de question invalide ${describe(questionId)}.`,
    );
    return;
  }
  if (known.has(questionId)) {
    errors.push(`${contextId} : question en double ${questionId}.`);
    return;
  }

  const label = `${contextId}.${questionId}`;
  requireText(question.legend, 5, 300, `${label}.legend`, errors);
  requireText(question.summaryLabel, 2, 120, `${label}.summaryLabel`, errors);
  if (question.hint !== null && question.hint !== undefined) {
    requireText(question.hint, 3, 400, `${label}.hint`, errors);
  }
  if (question.mode !== "single" && question.mode !== "multi") {
    errors.push(`${label} : mode doit valoir single ou multi.`);
  }

  const options = Array.isArray(question.options) ? question.options : [];
  if (options.length < 2 || options.length > 20) {
    errors.push(`${label} : entre 2 et 20 options.`);
  }

  const values = new Set<string>();
  for (const option of options) {
    const optionValue = isRecord(option) && typeof option.value === "string"
      ? option.value
      : "";
    if (!OPTION_VALUE.test(optionValue)) {
      errors.push(`${label} : valeur d'option invalide ${describe(optionValue)}.`);
      continue;
    }
    if (values.has(optionValue)) {
      errors.push(`${label} : option en double ${optionValue}.`);
      continue;
    }
    values.add(optionValue);
    requireText(
      isRecord(option) ? option.label : undefined,
      1,
      160,
      `${label}.${optionValue}.label`,
      errors,
    );
    if (isRecord(option) && option.exclusive === true && question.mode !== "multi") {
      errors.push(`${label} : une option exclusive n'a de sens qu'en mode multi.`);
    }
  }

  if (question.when) {
    const target = question.when.questionId;
    const targetValues = known.get(target);
    if (!targetValues) {
      errors.push(
        `${label} : condition d'affichage vers une question inconnue ou posterieure ${describe(target)}.`,
      );
    } else {
      const whenValues = Array.isArray(question.when.values) ? question.when.values : [];
      if (whenValues.length === 0) {
        errors.push(`${label} : condition d'affichage sans valeur.`);
      }
      for (const item of whenValues) {
        if (!targetValues.has(item)) {
          errors.push(`${label} : la valeur ${describe(item)} n'existe pas dans ${target}.`);
        }
      }
    }
  }

  known.set(questionId, values);
}

function validateGuidance(
  contextId: string,
  guidance: readonly unknown[],
  known: QuestionValues,
  errors: string[],
): void {
  if (guidance.length === 0) {
    errors.push(`${contextId} : au moins une regle de resultat est requise.`);
    return;
  }
  if (guidance.length > 40) {
    errors.push(`${contextId} : 40 regles de resultat au maximum.`);
  }

  const ids = new Set<string>();
  guidance.forEach((value, index) => {
    if (!isRecord(value)) {
      errors.push(`${contextId} : regle de resultat vide.`);
      return;
    }

    const rule = value as unknown as DiagnosticGuidanceRuleConfig;
    const ruleId = typeof rule.id === "string" ? rule.id : "";
    if (!RULE_ID.test(ruleId)) {
      errors.push(`${contextId} : identifiant de regle invalide ${describe(ruleId)}.`);
    } else if (ids.has(ruleId)) {
      errors.push(`${contextId} : regle en double ${ruleId}.`);
    } else {
      ids.add(ruleId);
    }

    requireText(rule.title, 5, 300, `${contextId}.${ruleId}.title`, errors);
    requireText(rule.body, 10, 1500, `${contextId}.${ruleId}.body`, errors);

    const points = Array.isArray(rule.points) ? rule.points : [];
    if (points.length > 10) errors.push(`${contextId}.${ruleId} : 10 points au maximum.`);
    for (const point of points) {
      requireText(point, 3, 300, `${contextId}.${ruleId}.points`, errors);
    }

    const conditions = Array.isArray(rule.when) ? rule.when : [];
    validateConditions(contextId, `${ruleId}.when`, conditions, known, errors);

    // Sans regle inconditionnelle finale, une combinaison de reponses pourrait
    // ne produire aucun texte : le parcours public afficherait un resultat vide.
    if (index === guidance.length - 1 && conditions.length !== 0) {
      errors.push(
        `${contextId} : la derniere regle de resultat doit etre inconditionnelle.`,
      );
    }
  });
}

function validateBillingMapping(
  contextId: string,
  mapping: DiagnosticBillingMappingConfig,
  known: QuestionValues,
  errors: string[],
): void {
  validateConditions(
    contextId,
    "billingMapping.requireAll",
    Array.isArray(mapping.requireAll) ? mapping.requireAll : [],
    known,
    errors,
  );
  validateOptionalConditions(
    contextId,
    "billingMapping.needsRemoteFilesWhen",
    mapping.needsRemoteFilesWhen,
    known,
    errors,
  );
  validateOptionalConditions(
    contextId,
    "billingMapping.needsVpnWhen",
    mapping.needsVpnWhen,
    known,
    errors,
  );
  validateOptionalConditions(
    contextId,
    "billingMapping.needsWindowsDesktopWhen",
    mapping.needsWindowsDesktopWhen,
    known,
    errors,
  );

  requireKnownQuestion(contextId, "usersQuestionId", mapping.usersQuestionId, known, errors);
  requireKnownQuestion(
    contextId,
    "structureQuestionId",
    mapping.structureQuestionId,
    known,
    errors,
  );
  requireKnownQuestion(
    contextId,
    "storageQuestionId",
    mapping.storageQuestionId,
    known,
    errors,
  );
  requireKnownQuestion(
    contextId,
    "restoreTestQuestionId",
    mapping.restoreTestQuestionId,
    known,
    errors,
  );

  if (!DATA_KINDS.has(mapping.individualDataKind)) {
    errors.push(
      `${contextId} : individualDataKind inconnu ${describe(mapping.individualDataKind)}.`,
    );
  }
  if (!DATA_KINDS.has(mapping.organisationDataKind)) {
    errors.push(
      `${contextId} : organisationDataKind inconnu ${describe(mapping.organisationDataKind)}.`,
    );
  }

  // Sans type de structure, aucune formule ne peut etre construite : la
  // correspondance serait morte et le parcours sortirait toujours en devis.
  if (mapping.structureQuestionId === null || mapping.structureQuestionId === undefined) {
    errors.push(
      `${contextId} : structureQuestionId est obligatoire pour une correspondance Billing V2.`,
    );
  }
}

function requireKnownQuestion(
  contextId: string,
  field: string,
  questionId: string | null,
  known: QuestionValues,
  errors: string[],
): void {
  if (questionId !== null && questionId !== undefined && !known.has(questionId)) {
    errors.push(`${contextId}.${field} : question inconnue ${describe(questionId)}.`);
  }
}

function validateOptionalConditions(
  contextId: string,
  field: string,
  conditions: readonly DiagnosticConditionConfig[] | null | undefined,
  known: QuestionValues,
  errors: string[],
): void {
  if (conditions === null || conditions === undefined) return;
  if (conditions.length === 0) {
    errors.push(`${contextId}.${field} : utiliser null plutot qu'une liste vide.`);
    return;
  }
  validateConditions(contextId, field, conditions, known, errors);
}

function validateConditions(
  contextId: string,
  field: string,
  conditions: readonly unknown[],
  known: QuestionValues,
  errors: string[],
): void {
  if (conditions.length > 10) {
    errors.push(`${contextId}.${field} : 10 conditions au maximum.`);
  }

  for (const value of conditions) {
    if (!isRecord(value)) {
      errors.push(`${contextId}.${field} : condition vide.`);
      continue;
    }

    const condition = value as unknown as DiagnosticConditionConfig;
    const target = typeof condition.questionId === "string" ? condition.questionId : "";
    const values = known.get(target);
    if (!values) {
      errors.push(`${contextId}.${field} : question inconnue ${describe(target)}.`);
      continue;
    }

    const operator = typeof condition.operator === "string" ? condition.operator : "";
    if (!OPERATORS.has(operator)) {
      errors.push(`${contextId}.${field} : operateur inconnu ${describe(operator)}.`);
      continue;
    }

    const conditionValues = Array.isArray(condition.values) ? condition.values : [];
    if (operator === "answered") {
      if (conditionValues.length !== 0) {
        errors.push(`${contextId}.${field} : answered n'accepte aucune valeur.`);
      }
      continue;
    }

    if (conditionValues.length === 0) {
      errors.push(`${contextId}.${field} : l'operateur ${operator} exige au moins une valeur.`);
      continue;
    }
    if (operator === "equals" && conditionValues.length !== 1) {
      errors.push(`${contextId}.${field} : equals n'accepte qu'une valeur.`);
    }
    for (const item of conditionValues) {
      if (!values.has(item)) {
        errors.push(`${contextId}.${field} : la valeur ${describe(item)} n'existe pas dans ${target}.`);
      }
    }
  }
}

function requireText(
  value: unknown,
  minimum: number,
  maximum: number,
  field: string,
  errors: string[],
): void {
  const trimmed = typeof value === "string" ? value.trim() : "";
  if (trimmed.length < minimum || trimmed.length > maximum) {
    errors.push(`${field} : texte requis entre ${minimum} et ${maximum} caracteres.`);
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/** Tronque une valeur refusee : un message d'erreur ne rejoue jamais une charge entiere. */
function describe(value: string): string {
  return value.length <= 40 ? `"${value}"` : `"${value.slice(0, 40)}…"`;
}
