"use client";

import type {
  BillingV2PublicCatalog,
  DiagnosticConfiguration,
  DiagnosticRecommendationConfig,
} from "@kermaria/shared";
import { useMemo, useState } from "react";

import {
  buildAdaptiveDiagnosticOutcome,
  canContextProduceFormula,
} from "@/lib/adaptive-diagnostic";
import { describeSelectionConfiguration } from "@/lib/billing-v2-formules";
import {
  DIAGNOSTIC_CONTEXT_IDS,
  getVisibleDiagnosticQuestions,
  isDiagnosticQuestionAnswered,
  pruneHiddenDiagnosticAnswers,
  resolveDiagnosticContextConfig,
  type DiagnosticAnswerMap,
  type DiagnosticContextId,
} from "@/lib/diagnostic-context";

/**
 * Simulateur d'administration. Il appelle exactement le moteur du parcours
 * public — `getVisibleDiagnosticQuestions` puis
 * `buildAdaptiveDiagnosticOutcome` — sur la configuration en cours d'edition.
 * Il n'existe donc pas de seconde implementation a maintenir, et un ecart
 * entre simulation et production est structurellement impossible.
 *
 * Aucun prix n'est affiche : la tarification reste l'autorite de Billing V2.
 */
export function AdminDiagnosticSimulator({
  catalog,
  configuration,
  recommendationConfig,
}: {
  catalog: BillingV2PublicCatalog;
  configuration: DiagnosticConfiguration;
  recommendationConfig: DiagnosticRecommendationConfig;
}) {
  const [context, setContext] = useState<DiagnosticContextId>("backup");
  const [answers, setAnswers] = useState<DiagnosticAnswerMap>({});

  const definition = useMemo(
    () => resolveDiagnosticContextConfig(context, configuration),
    [context, configuration],
  );
  const visibleQuestions = useMemo(
    () => getVisibleDiagnosticQuestions(context, answers, configuration),
    [context, answers, configuration],
  );
  const outcome = useMemo(
    () => buildAdaptiveDiagnosticOutcome(
      context,
      answers,
      catalog,
      recommendationConfig,
      configuration,
    ),
    [context, answers, catalog, recommendationConfig, configuration],
  );

  const selection = outcome.recommendation?.selection ?? null;
  const preset = selection
    ? catalog.presets.find((item) => item.code === selection.presetCode) ?? null
    : null;

  function setAnswer(questionId: string, value: string, multi: boolean) {
    setAnswers((current) => {
      const previous = current[questionId];
      let next: string | string[] | undefined;
      if (!multi) {
        next = previous === value ? undefined : value;
      } else {
        const list = Array.isArray(previous) ? previous : [];
        next = list.includes(value)
          ? list.filter((item) => item !== value)
          : [...list, value];
        if ((next as string[]).length === 0) next = undefined;
      }

      const updated: DiagnosticAnswerMap = { ...current, [questionId]: next };
      if (next === undefined) delete updated[questionId];
      // Les reponses devenues invisibles sont retirees, exactement comme dans
      // le parcours public : sinon la simulation mentirait sur le resultat.
      return pruneHiddenDiagnosticAnswers(context, updated, configuration);
    });
  }

  return (
    <section aria-label="Simulateur du diagnostic" className="admin-diagnostic-simulator">
      <label className="admin-diagnostic-field">
        <span>Contexte simulé</span>
        <select
          onChange={(event) => {
            setContext(event.target.value as DiagnosticContextId);
            setAnswers({});
          }}
          value={context}
        >
          {DIAGNOSTIC_CONTEXT_IDS.map((id) => (
            <option key={id} value={id}>{id}</option>
          ))}
        </select>
      </label>

      <p className="muted">
        {canContextProduceFormula(context, configuration)
          ? "Ce contexte peut proposer une formule."
          : "Ce contexte sort systématiquement en cadrage/devis."}
      </p>

      <div className="admin-diagnostic-simulator-grid">
        <div>
          <h3>Questions visibles ({visibleQuestions.length})</h3>
          {visibleQuestions.map((question) => (
            <fieldset className="admin-diagnostic-card" key={question.id}>
              <legend>{question.legend}</legend>
              <div className="admin-diagnostic-values">
                {question.options.map((option) => {
                  const raw = answers[question.id];
                  const checked = Array.isArray(raw)
                    ? raw.includes(option.value)
                    : raw === option.value;
                  return (
                    <label key={option.value}>
                      <input
                        checked={checked}
                        onChange={() =>
                          setAnswer(question.id, option.value, question.mode === "multi")}
                        type="checkbox"
                      />
                      {option.label}
                    </label>
                  );
                })}
              </div>
              {!isDiagnosticQuestionAnswered(question, answers) ? (
                <p className="muted">Sans réponse.</p>
              ) : null}
            </fieldset>
          ))}
        </div>

        <div>
          <h3>Résultat</h3>
          <p className="muted">{definition.title}</p>
          <article className="admin-diagnostic-card">
            <h4>{outcome.guidance.title}</h4>
            <p>{outcome.guidance.body}</p>
            {outcome.guidance.points.length > 0 ? (
              <ul>
                {outcome.guidance.points.map((point) => <li key={point}>{point}</li>)}
              </ul>
            ) : null}
          </article>

          <h4>Formule proposée</h4>
          {preset && selection ? (
            <>
              <p>{preset.name}</p>
              <ul>
                {describeSelectionConfiguration(selection, catalog).map((entry) => (
                  <li key={entry.key}>
                    {entry.label} : {entry.value}
                  </li>
                ))}
              </ul>
            </>
          ) : (
            <p className="muted">Aucune formule : cadrage ou devis.</p>
          )}

          <h4>Règles appliquées</h4>
          {outcome.appliedRuleIds.length > 0 ? (
            <ul>
              {outcome.appliedRuleIds.map((id) => <li key={id}><code>{id}</code></li>)}
            </ul>
          ) : (
            <p className="muted">Aucune règle applicable.</p>
          )}
        </div>
      </div>
    </section>
  );
}
