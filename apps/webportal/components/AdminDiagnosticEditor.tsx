"use client";

import {
  DIAGNOSTIC_CONDITION_OPERATORS,
  type DiagnosticBillingMappingConfig,
  type DiagnosticConditionConfig,
  type DiagnosticContextConfig,
  type DiagnosticGuidanceRuleConfig,
  type DiagnosticQuestionConfig,
} from "@kermaria/shared";

const DATA_KIND_CHOICES = [
  { value: "personal_documents", label: "Documents personnels" },
  { value: "business_documents", label: "Documents professionnels" },
  { value: "photos", label: "Photos" },
  { value: "association_data", label: "Données d'association" },
  { value: "work_files", label: "Fichiers de travail" },
  { value: "other_important_files", label: "Autres fichiers importants" },
] as const;

/**
 * Editeur structure d'un contexte de diagnostic. Il n'expose que les
 * operations de la DSL fermee : aucune saisie libre d'expression, aucun
 * operateur hors de la liste partagee. La validation reste faite par
 * API-INTERNAL, cet editeur empeche seulement de composer l'invalide.
 */
export function AdminDiagnosticContextEditor({
  context,
  onChange,
}: {
  context: DiagnosticContextConfig;
  onChange: (next: DiagnosticContextConfig) => void;
}) {
  const questionChoices = context.questions.map((question) => question.id);

  function patch(partial: Partial<DiagnosticContextConfig>) {
    onChange({ ...context, ...partial });
  }

  function patchQuestion(index: number, next: DiagnosticQuestionConfig) {
    patch({
      questions: context.questions.map((item, position) =>
        position === index ? next : item),
    });
  }

  function moveQuestion(index: number, delta: number) {
    const target = index + delta;
    if (target < 0 || target >= context.questions.length) return;
    const next = [...context.questions];
    [next[index], next[target]] = [next[target], next[index]];
    patch({ questions: next });
  }

  return (
    <div className="admin-diagnostic-editor">
      <fieldset>
        <legend>Présentation</legend>
        <TextField
          label="Libellé court"
          onChange={(value) => patch({ label: value })}
          value={context.label}
        />
        <TextField
          label="Surtitre"
          onChange={(value) => patch({ eyebrow: value })}
          value={context.eyebrow}
        />
        <TextField
          label="Titre"
          onChange={(value) => patch({ title: value })}
          value={context.title}
        />
        <TextAreaField
          label="Introduction"
          onChange={(value) => patch({ intro: value })}
          rows={3}
          value={context.intro}
        />
        <TextField
          label="Sujet de contact"
          onChange={(value) => patch({ contactSubject: value })}
          value={context.contactSubject}
        />
        <label className="admin-diagnostic-check">
          <input
            checked={context.formulaEligible}
            onChange={(event) => patch({
              formulaEligible: event.target.checked,
              // Une correspondance Billing V2 sans eligibilite serait refusee
              // par l'API : on la retire au moment ou l'administrateur decoche.
              billingMapping: event.target.checked ? context.billingMapping : null,
            })}
            type="checkbox"
          />
          Ce contexte peut proposer une formule
        </label>
      </fieldset>

      <fieldset>
        <legend>Questions ({context.questions.length})</legend>
        {context.questions.map((question, index) => (
          <QuestionEditor
            key={question.id}
            onChange={(next) => patchQuestion(index, next)}
            onMove={(delta) => moveQuestion(index, delta)}
            onRemove={() => patch({
              questions: context.questions.filter((_, position) => position !== index),
            })}
            // Une condition d'affichage ne peut viser qu'une question posee
            // avant celle-ci : la liste proposee s'arrete donc a l'index.
            previousQuestions={context.questions.slice(0, index)}
            question={question}
          />
        ))}
        <button
          className="button button-secondary"
          onClick={() => patch({
            questions: [...context.questions, createQuestion(context.questions.length)],
          })}
          type="button"
        >
          Ajouter une question
        </button>
      </fieldset>

      <fieldset>
        <legend>Textes de résultat ({context.guidance.length})</legend>
        <p className="muted">
          La première règle satisfaite gagne. La dernière doit rester sans condition :
          elle garantit qu&apos;un résultat existe toujours.
        </p>
        {context.guidance.map((rule, index) => (
          <GuidanceEditor
            key={rule.id}
            onChange={(next) => patch({
              guidance: context.guidance.map((item, position) =>
                position === index ? next : item),
            })}
            onMove={(delta) => {
              const target = index + delta;
              if (target < 0 || target >= context.guidance.length) return;
              const next = [...context.guidance];
              [next[index], next[target]] = [next[target], next[index]];
              patch({ guidance: next });
            }}
            onRemove={() => patch({
              guidance: context.guidance.filter((_, position) => position !== index),
            })}
            questions={context.questions}
            rule={rule}
          />
        ))}
        <button
          className="button button-secondary"
          onClick={() => patch({
            guidance: [...context.guidance, createRule(context.id, context.guidance.length)],
          })}
          type="button"
        >
          Ajouter une règle
        </button>
      </fieldset>

      <fieldset>
        <legend>Correspondance Billing V2</legend>
        <p className="muted">
          Traduction des réponses vers un besoin. Aucun prix n&apos;est calculé ici :
          la tarification reste l&apos;autorité de Billing V2.
        </p>
        {context.formulaEligible ? (
          <>
            <label className="admin-diagnostic-check">
              <input
                checked={context.billingMapping !== null}
                onChange={(event) => patch({
                  billingMapping: event.target.checked
                    ? createBillingMapping(questionChoices)
                    : null,
                })}
                type="checkbox"
              />
              Activer une correspondance automatique
            </label>
            {context.billingMapping ? (
              <BillingMappingEditor
                mapping={context.billingMapping}
                onChange={(next) => patch({ billingMapping: next })}
                questions={context.questions}
              />
            ) : (
              <p className="muted">
                Sans correspondance, ce contexte sort systématiquement en cadrage/devis.
              </p>
            )}
          </>
        ) : (
          <p className="muted">
            Ce contexte n&apos;est pas éligible à une formule : il sort toujours en
            cadrage/devis.
          </p>
        )}
      </fieldset>
    </div>
  );
}

function QuestionEditor({
  question,
  previousQuestions,
  onChange,
  onMove,
  onRemove,
}: {
  question: DiagnosticQuestionConfig;
  previousQuestions: readonly DiagnosticQuestionConfig[];
  onChange: (next: DiagnosticQuestionConfig) => void;
  onMove: (delta: number) => void;
  onRemove: () => void;
}) {
  const visibilityTarget = previousQuestions.find(
    (item) => item.id === question.when?.questionId,
  ) ?? null;

  function patch(partial: Partial<DiagnosticQuestionConfig>) {
    onChange({ ...question, ...partial });
  }

  return (
    <article className="admin-diagnostic-card">
      <header>
        <code>{question.id}</code>
        <span className="admin-diagnostic-actions">
          <button className="button button-link" onClick={() => onMove(-1)} type="button">
            Monter
          </button>
          <button className="button button-link" onClick={() => onMove(1)} type="button">
            Descendre
          </button>
          <button className="button button-link" onClick={onRemove} type="button">
            Supprimer
          </button>
        </span>
      </header>

      <TextField
        label="Identifiant"
        onChange={(value) => patch({ id: value })}
        value={question.id}
      />
      <TextField
        label="Question posée"
        onChange={(value) => patch({ legend: value })}
        value={question.legend}
      />
      <TextField
        label="Libellé de récapitulatif"
        onChange={(value) => patch({ summaryLabel: value })}
        value={question.summaryLabel}
      />
      <TextField
        label="Aide (facultatif)"
        onChange={(value) => patch({ hint: value.trim() === "" ? null : value })}
        value={question.hint ?? ""}
      />
      <label className="admin-diagnostic-field">
        <span>Mode</span>
        <select
          onChange={(event) => patch({
            mode: event.target.value === "multi" ? "multi" : "single",
            // Une option exclusive n'existe qu'en choix multiple : repasser en
            // simple la retire plutot que de laisser une configuration refusee.
            options: event.target.value === "multi"
              ? question.options
              : question.options.map((option) => ({ ...option, exclusive: false })),
          })}
          value={question.mode}
        >
          <option value="single">Choix unique</option>
          <option value="multi">Choix multiple</option>
        </select>
      </label>

      <div className="admin-diagnostic-options">
        <span className="admin-diagnostic-subtitle">Options</span>
        {question.options.map((option, index) => (
          <div className="admin-diagnostic-option-row" key={`${option.value}-${index}`}>
            <input
              aria-label="Valeur technique"
              onChange={(event) => patch({
                options: question.options.map((item, position) =>
                  position === index ? { ...item, value: event.target.value } : item),
              })}
              value={option.value}
            />
            <input
              aria-label="Libellé affiché"
              onChange={(event) => patch({
                options: question.options.map((item, position) =>
                  position === index ? { ...item, label: event.target.value } : item),
              })}
              value={option.label}
            />
            {question.mode === "multi" ? (
              <label>
                <input
                  checked={option.exclusive}
                  onChange={(event) => patch({
                    options: question.options.map((item, position) =>
                      position === index
                        ? { ...item, exclusive: event.target.checked }
                        : item),
                  })}
                  type="checkbox"
                />
                exclusive
              </label>
            ) : null}
            <button
              className="button button-link"
              onClick={() => patch({
                options: question.options.filter((_, position) => position !== index),
              })}
              type="button"
            >
              Retirer
            </button>
          </div>
        ))}
        <button
          className="button button-link"
          onClick={() => patch({
            options: [
              ...question.options,
              { value: `option-${question.options.length + 1}`, label: "Nouvelle option", exclusive: false },
            ],
          })}
          type="button"
        >
          Ajouter une option
        </button>
      </div>

      <div className="admin-diagnostic-visibility">
        <span className="admin-diagnostic-subtitle">Affichage conditionnel</span>
        <label className="admin-diagnostic-field">
          <span>Dépend de</span>
          <select
            onChange={(event) => patch({
              when: event.target.value === ""
                ? null
                : { questionId: event.target.value, values: [] },
            })}
            value={question.when?.questionId ?? ""}
          >
            <option value="">Toujours affichée</option>
            {previousQuestions.map((item) => (
              <option key={item.id} value={item.id}>{item.id}</option>
            ))}
          </select>
        </label>
        {question.when && visibilityTarget ? (
          <ValuePicker
            onChange={(values) => patch({
              when: { questionId: question.when!.questionId, values },
            })}
            options={visibilityTarget.options}
            values={question.when.values}
          />
        ) : null}
      </div>
    </article>
  );
}

function GuidanceEditor({
  rule,
  questions,
  onChange,
  onMove,
  onRemove,
}: {
  rule: DiagnosticGuidanceRuleConfig;
  questions: readonly DiagnosticQuestionConfig[];
  onChange: (next: DiagnosticGuidanceRuleConfig) => void;
  onMove: (delta: number) => void;
  onRemove: () => void;
}) {
  function patch(partial: Partial<DiagnosticGuidanceRuleConfig>) {
    onChange({ ...rule, ...partial });
  }

  return (
    <article className="admin-diagnostic-card">
      <header>
        <code>{rule.id}</code>
        <span className="admin-diagnostic-actions">
          <button className="button button-link" onClick={() => onMove(-1)} type="button">
            Monter
          </button>
          <button className="button button-link" onClick={() => onMove(1)} type="button">
            Descendre
          </button>
          <button className="button button-link" onClick={onRemove} type="button">
            Supprimer
          </button>
        </span>
      </header>
      <TextField
        label="Identifiant de règle"
        onChange={(value) => patch({ id: value.toUpperCase() })}
        value={rule.id}
      />
      <TextField
        label="Titre du résultat"
        onChange={(value) => patch({ title: value })}
        value={rule.title}
      />
      <TextAreaField
        label="Corps du résultat"
        onChange={(value) => patch({ body: value })}
        rows={3}
        value={rule.body}
      />
      <TextAreaField
        label="Points (une ligne par point)"
        onChange={(value) => patch({
          points: value.split("\n").map((line) => line.trim()).filter(Boolean),
        })}
        rows={3}
        value={rule.points.join("\n")}
      />
      <ConditionListEditor
        conditions={rule.when}
        label="Conditions"
        onChange={(next) => patch({ when: next })}
        questions={questions}
      />
    </article>
  );
}

function BillingMappingEditor({
  mapping,
  questions,
  onChange,
}: {
  mapping: DiagnosticBillingMappingConfig;
  questions: readonly DiagnosticQuestionConfig[];
  onChange: (next: DiagnosticBillingMappingConfig) => void;
}) {
  function patch(partial: Partial<DiagnosticBillingMappingConfig>) {
    onChange({ ...mapping, ...partial });
  }

  return (
    <div className="admin-diagnostic-mapping">
      <ConditionListEditor
        conditions={mapping.requireAll}
        label="Conditions requises pour proposer une formule"
        onChange={(next) => patch({ requireAll: next })}
        questions={questions}
      />
      <QuestionSelect
        allowNone
        label="Question « nombre d'utilisateurs »"
        onChange={(value) => patch({ usersQuestionId: value })}
        questions={questions}
        value={mapping.usersQuestionId}
      />
      <QuestionSelect
        label="Question « type de structure »"
        onChange={(value) => patch({ structureQuestionId: value })}
        questions={questions}
        value={mapping.structureQuestionId}
      />
      <QuestionSelect
        allowNone
        label="Question « volume de stockage »"
        onChange={(value) => patch({ storageQuestionId: value })}
        questions={questions}
        value={mapping.storageQuestionId}
      />
      <QuestionSelect
        allowNone
        label="Question « test de restauration »"
        onChange={(value) => patch({ restoreTestQuestionId: value })}
        questions={questions}
        value={mapping.restoreTestQuestionId}
      />
      <OptionalConditionListEditor
        conditions={mapping.needsRemoteFilesWhen}
        label="Besoin : fichiers à distance"
        onChange={(next) => patch({ needsRemoteFilesWhen: next })}
        questions={questions}
      />
      <OptionalConditionListEditor
        conditions={mapping.needsVpnWhen}
        label="Besoin : accès VPN"
        onChange={(next) => patch({ needsVpnWhen: next })}
        questions={questions}
      />
      <OptionalConditionListEditor
        conditions={mapping.needsWindowsDesktopWhen}
        label="Besoin : bureau Windows distant"
        onChange={(next) => patch({ needsWindowsDesktopWhen: next })}
        questions={questions}
      />
      <label className="admin-diagnostic-field">
        <span>Nature des données — particulier</span>
        <select
          onChange={(event) => patch({ individualDataKind: event.target.value })}
          value={mapping.individualDataKind}
        >
          {DATA_KIND_CHOICES.map((choice) => (
            <option key={choice.value} value={choice.value}>{choice.label}</option>
          ))}
        </select>
      </label>
      <label className="admin-diagnostic-field">
        <span>Nature des données — organisation</span>
        <select
          onChange={(event) => patch({ organisationDataKind: event.target.value })}
          value={mapping.organisationDataKind}
        >
          {DATA_KIND_CHOICES.map((choice) => (
            <option key={choice.value} value={choice.value}>{choice.label}</option>
          ))}
        </select>
      </label>
    </div>
  );
}

function OptionalConditionListEditor({
  conditions,
  label,
  questions,
  onChange,
}: {
  conditions: DiagnosticConditionConfig[] | null;
  label: string;
  questions: readonly DiagnosticQuestionConfig[];
  onChange: (next: DiagnosticConditionConfig[] | null) => void;
}) {
  return (
    <div className="admin-diagnostic-optional">
      <label className="admin-diagnostic-check">
        <input
          checked={conditions !== null}
          onChange={(event) => onChange(
            // Une liste vide serait refusee par l'API : le decoche remet `null`,
            // qui signifie explicitement « besoin non exprime ».
            event.target.checked ? [createCondition(questions)] : null,
          )}
          type="checkbox"
        />
        {label}
      </label>
      {conditions ? (
        <ConditionListEditor
          conditions={conditions}
          label={label}
          onChange={(next) => onChange(next.length === 0 ? null : next)}
          questions={questions}
        />
      ) : null}
    </div>
  );
}

function ConditionListEditor({
  conditions,
  label,
  questions,
  onChange,
}: {
  conditions: readonly DiagnosticConditionConfig[];
  label: string;
  questions: readonly DiagnosticQuestionConfig[];
  onChange: (next: DiagnosticConditionConfig[]) => void;
}) {
  return (
    <div className="admin-diagnostic-conditions">
      <span className="admin-diagnostic-subtitle">{label}</span>
      {conditions.length === 0 ? (
        <p className="muted">Aucune condition : la règle s&apos;applique toujours.</p>
      ) : null}
      {conditions.map((condition, index) => {
        const target = questions.find((item) => item.id === condition.questionId) ?? null;
        return (
          <div className="admin-diagnostic-condition" key={`${condition.questionId}-${index}`}>
            <select
              aria-label="Question"
              onChange={(event) => onChange(conditions.map((item, position) =>
                position === index
                  ? { ...item, questionId: event.target.value, values: [] }
                  : item))}
              value={condition.questionId}
            >
              {questions.map((item) => (
                <option key={item.id} value={item.id}>{item.id}</option>
              ))}
            </select>
            <select
              aria-label="Opérateur"
              onChange={(event) => onChange(conditions.map((item, position) =>
                position === index
                  ? {
                      ...item,
                      operator: event.target.value as DiagnosticConditionConfig["operator"],
                      values: event.target.value === "answered" ? [] : item.values,
                    }
                  : item))}
              value={condition.operator}
            >
              {DIAGNOSTIC_CONDITION_OPERATORS.map((operator) => (
                <option key={operator} value={operator}>{operator}</option>
              ))}
            </select>
            {condition.operator === "answered" || target === null ? null : (
              <ValuePicker
                onChange={(values) => onChange(conditions.map((item, position) =>
                  position === index ? { ...item, values } : item))}
                options={target.options}
                single={condition.operator === "equals"}
                values={condition.values}
              />
            )}
            <button
              className="button button-link"
              onClick={() => onChange(conditions.filter((_, position) => position !== index))}
              type="button"
            >
              Retirer
            </button>
          </div>
        );
      })}
      <button
        className="button button-link"
        disabled={questions.length === 0}
        onClick={() => onChange([...conditions, createCondition(questions)])}
        type="button"
      >
        Ajouter une condition
      </button>
    </div>
  );
}

function ValuePicker({
  options,
  values,
  single = false,
  onChange,
}: {
  options: readonly { value: string; label: string }[];
  values: readonly string[];
  single?: boolean;
  onChange: (next: string[]) => void;
}) {
  return (
    <span className="admin-diagnostic-values">
      {options.map((option) => {
        const checked = values.includes(option.value);
        return (
          <label key={option.value}>
            <input
              checked={checked}
              onChange={() => {
                if (single) {
                  onChange(checked ? [] : [option.value]);
                  return;
                }
                onChange(checked
                  ? values.filter((item) => item !== option.value)
                  : [...values, option.value]);
              }}
              type="checkbox"
            />
            {option.value}
          </label>
        );
      })}
    </span>
  );
}

function QuestionSelect({
  label,
  questions,
  value,
  allowNone = false,
  onChange,
}: {
  label: string;
  questions: readonly DiagnosticQuestionConfig[];
  value: string | null;
  allowNone?: boolean;
  onChange: (next: string | null) => void;
}) {
  return (
    <label className="admin-diagnostic-field">
      <span>{label}</span>
      <select
        onChange={(event) => onChange(event.target.value === "" ? null : event.target.value)}
        value={value ?? ""}
      >
        <option value="">{allowNone ? "Aucune" : "— à choisir —"}</option>
        {questions.map((question) => (
          <option key={question.id} value={question.id}>{question.id}</option>
        ))}
      </select>
    </label>
  );
}

function TextField({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (next: string) => void;
}) {
  return (
    <label className="admin-diagnostic-field">
      <span>{label}</span>
      <input onChange={(event) => onChange(event.target.value)} value={value} />
    </label>
  );
}

function TextAreaField({
  label,
  value,
  rows,
  onChange,
}: {
  label: string;
  value: string;
  rows: number;
  onChange: (next: string) => void;
}) {
  return (
    <label className="admin-diagnostic-field">
      <span>{label}</span>
      <textarea onChange={(event) => onChange(event.target.value)} rows={rows} value={value} />
    </label>
  );
}

function createQuestion(index: number): DiagnosticQuestionConfig {
  return {
    id: `question-${index + 1}`,
    legend: "Nouvelle question à formuler",
    summaryLabel: "Nouvelle question",
    mode: "single",
    hint: null,
    when: null,
    options: [
      { value: "option-1", label: "Première option", exclusive: false },
      { value: "option-2", label: "Deuxième option", exclusive: false },
    ],
  };
}

function createRule(contextId: string, index: number): DiagnosticGuidanceRuleConfig {
  const prefix = contextId.toUpperCase().replace(/[^A-Z0-9]/g, "-");
  return {
    id: `DIA-${prefix}-${String((index + 1) * 10).padStart(3, "0")}`,
    when: [],
    title: "Titre du résultat à rédiger",
    body: "Texte affiché au visiteur lorsque cette règle s'applique.",
    points: [],
  };
}

function createCondition(
  questions: readonly DiagnosticQuestionConfig[],
): DiagnosticConditionConfig {
  const first = questions[0];
  return {
    questionId: first?.id ?? "",
    operator: "equals",
    values: [],
  };
}

function createBillingMapping(
  questionIds: readonly string[],
): DiagnosticBillingMappingConfig {
  return {
    requireAll: [],
    usersQuestionId: questionIds.includes("users") ? "users" : null,
    structureQuestionId: questionIds.includes("structure") ? "structure" : null,
    storageQuestionId: null,
    restoreTestQuestionId: null,
    needsRemoteFilesWhen: null,
    needsVpnWhen: null,
    needsWindowsDesktopWhen: null,
    individualDataKind: "personal_documents",
    organisationDataKind: "business_documents",
  };
}
