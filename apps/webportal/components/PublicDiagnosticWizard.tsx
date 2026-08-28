"use client";

import Link from "next/link";
import { useEffect, useMemo, useRef, useState, type CSSProperties, type RefObject } from "react";

import type {
  BillingV2PublicCatalog,
  BillingV2PublicQuote,
  BillingV2PublicSelection,
} from "@kermaria/shared";

import { ContactForm } from "@/components/ContactForm";
import { buildAdaptiveDiagnosticOutcome } from "@/lib/adaptive-diagnostic";
import { billingV2SelectionToSearchParams } from "@/lib/billing-v2-selection";
import {
  GENERAL_CONTEXT_CHOICES,
  buildDiagnosticContactMessage,
  buildDiagnosticHref,
  describeDiagnosticAnswers,
  getDiagnosticContextDefinition,
  getVisibleDiagnosticQuestions,
  isDiagnosticQuestionAnswered,
  pruneHiddenDiagnosticAnswers,
  type DiagnosticAnswerMap,
  type DiagnosticContextId,
  type DiagnosticQuestion,
} from "@/lib/diagnostic-context";
import { formatCurrencyFromCents } from "@/lib/formatters";

type PublicDiagnosticWizardProps = {
  catalog: BillingV2PublicCatalog;
  initialContext: DiagnosticContextId;
};

const BENEFITS = [
  { title: "Questions ciblées", body: "Seulement ce qui est utile pour votre situation." },
  { title: "Sans jargon", body: "Décrivez vos usages, pas une solution technique." },
  { title: "Sans engagement", body: "Aucun compte ni achat n'est nécessaire pour commencer." },
] as const;

export function PublicDiagnosticWizard({
  catalog,
  initialContext,
}: PublicDiagnosticWizardProps) {
  const definition = getDiagnosticContextDefinition(initialContext);
  const [answers, setAnswers] = useState<DiagnosticAnswerMap>({});
  const [step, setStep] = useState(0);
  const [completed, setCompleted] = useState(false);
  const [quote, setQuote] = useState<BillingV2PublicQuote | null>(null);
  const [quotePending, setQuotePending] = useState(false);
  const [quoteError, setQuoteError] = useState<string | null>(null);
  const stepTitleRef = useRef<HTMLLegendElement>(null);

  const visibleQuestions = useMemo(
    () => getVisibleDiagnosticQuestions(initialContext, answers),
    [initialContext, answers],
  );
  const currentQuestion = visibleQuestions[step] ?? null;
  const outcome = useMemo(
    () => buildAdaptiveDiagnosticOutcome(initialContext, answers, catalog),
    [initialContext, answers, catalog],
  );
  const selection = outcome.recommendation?.selection ?? null;
  const recommendedPreset = selection
    ? catalog.presets.find((preset) => preset.code === selection.presetCode) ?? null
    : null;

  useEffect(() => {
    if (completed || !currentQuestion) return;
    const frame = window.requestAnimationFrame(() => {
      stepTitleRef.current?.focus({ preventScroll: true });
      stepTitleRef.current?.scrollIntoView({ block: "start", inline: "nearest" });
    });
    return () => window.cancelAnimationFrame(frame);
  }, [completed, currentQuestion, step]);

  useEffect(() => {
    if (!completed || !selection) return;
    const controller = new AbortController();

    fetch("/api/formules/devis", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(selection),
      signal: controller.signal,
    })
      .then(async (response) => {
        if (!response.ok) throw new Error(String(response.status));
        return (await response.json()) as BillingV2PublicQuote;
      })
      .then((payload) => {
        setQuote(payload);
        setQuotePending(false);
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        setQuote(null);
        setQuotePending(false);
        setQuoteError(
          error instanceof Error && error.message === "400"
            ? "Cette configuration n'est plus disponible dans le catalogue."
            : "Le tarif est temporairement indisponible. Il sera recalculé avant toute souscription.",
        );
      });

    return () => controller.abort();
  }, [completed, selection]);

  if (initialContext === "general") {
    return (
      <div className="diagnostic-page">
        <DiagnosticHeader definition={definition} />
        <section className="diagnostic-context-picker" aria-labelledby="diagnostic-context-title">
          <div className="diagnostic-context-picker-heading">
            <p className="eyebrow">Votre besoin</p>
            <h2 id="diagnostic-context-title">{"Qu'est-ce qui vous am\u00e8ne ici ?"}</h2>
            <p>Choisissez le sujet le plus proche. Vous pourrez toujours préciser votre situation ensuite.</p>
          </div>
          <div className="diagnostic-context-grid">
            {GENERAL_CONTEXT_CHOICES.map((choice) => (
              <Link
                className="diagnostic-context-card"
                href={buildDiagnosticHref(choice.context)}
                key={choice.context}
              >
                <strong>{choice.title}</strong>
                <span>{choice.description}</span>
                <span className="diagnostic-context-card-action">Commencer →</span>
              </Link>
            ))}
          </div>
        </section>
      </div>
    );
  }

  if (completed) {
    return (
      <div className="diagnostic-page">
        <DiagnosticHeader definition={definition} compact />
        <DiagnosticResult
          answers={answers}
          context={initialContext}
          definition={definition}
          outcome={outcome}
          quote={quote}
          quoteError={quoteError}
          quotePending={quotePending}
          recommendedPreset={recommendedPreset}
          selection={selection}
          onRestart={() => {
            setAnswers({});
            setStep(0);
            setCompleted(false);
            setQuote(null);
            setQuotePending(false);
            setQuoteError(null);
          }}
        />
      </div>
    );
  }

  if (!currentQuestion) {
    return (
      <div className="diagnostic-page">
        <DiagnosticHeader definition={definition} />
        <section className="diagnostic-wizard">
          <p>Ce parcours est temporairement indisponible.</p>
          <Link className="button" href="/contact">Nous contacter</Link>
        </section>
      </div>
    );
  }

  const canContinue = isDiagnosticQuestionAnswered(currentQuestion, answers);

  return (
    <div className="diagnostic-page">
      <DiagnosticHeader definition={definition} />

      <section className="diagnostic-wizard" aria-label={`Diagnostic ${definition.label}`}>
        <div className="diagnostic-wizard-toolbar">
          <div>
            <span className="diagnostic-context-badge">{definition.label}</span>
            <p className="diagnostic-progress-text" aria-live="polite">
              Question {step + 1} sur {visibleQuestions.length}
            </p>
          </div>
          <Link className="diagnostic-change-context" href="/diagnostic">
            Changer de sujet
          </Link>
        </div>

        <div
          aria-hidden="true"
          className="diagnostic-progress-bar"
          style={{
            "--diagnostic-progress": `${((step + 1) / visibleQuestions.length) * 100}%`,
          } as CSSProperties}
        >
          <span />
        </div>

        <DiagnosticQuestionFieldset
          answers={answers}
          legendRef={stepTitleRef}
          question={currentQuestion}
          onChange={(nextValue) => {
            setAnswers((current) => {
              const next = { ...current, [currentQuestion.id]: nextValue };
              return pruneHiddenDiagnosticAnswers(initialContext, next);
            });
          }}
        />

        {!canContinue ? (
          <p className="diagnostic-answer-hint" role="status">
            Choisissez une réponse pour continuer.
          </p>
        ) : null}

        <div className="diagnostic-actions">
          <button
            className="button button-secondary"
            disabled={step === 0}
            onClick={() => setStep((current) => Math.max(0, current - 1))}
            type="button"
          >
            Précédent
          </button>
          <button
            className="button"
            disabled={!canContinue}
            onClick={() => {
              if (step >= visibleQuestions.length - 1) {
                setQuote(null);
                setQuotePending(selection !== null);
                setQuoteError(null);
                setCompleted(true);
                return;
              }
              setStep((current) => current + 1);
            }}
            type="button"
          >
            {step >= visibleQuestions.length - 1 ? "Voir mon orientation" : "Continuer"}
          </button>
        </div>
      </section>
    </div>
  );
}

function DiagnosticHeader({
  definition,
  compact = false,
}: {
  definition: ReturnType<typeof getDiagnosticContextDefinition>;
  compact?: boolean;
}) {
  return (
    <header className={`diagnostic-header${compact ? " diagnostic-header-compact" : ""}`}>
      <div>
        <p className="eyebrow">{definition.eyebrow}</p>
        <h1>{definition.title}</h1>
        <p>{definition.intro}</p>
      </div>
      {!compact ? (
        <div className="diagnostic-benefits" aria-label="Bénéfices du diagnostic">
          {BENEFITS.map((benefit) => (
            <article key={benefit.title}>
              <span aria-hidden="true">✓</span>
              <h2>{benefit.title}</h2>
              <p>{benefit.body}</p>
            </article>
          ))}
        </div>
      ) : null}
    </header>
  );
}

function DiagnosticQuestionFieldset({
  question,
  answers,
  onChange,
  legendRef,
}: {
  question: DiagnosticQuestion;
  answers: DiagnosticAnswerMap;
  onChange: (value: string | readonly string[]) => void;
  legendRef: RefObject<HTMLLegendElement | null>;
}) {
  const value = answers[question.id];
  const selectedValues = Array.isArray(value)
    ? value
    : typeof value === "string"
      ? [value]
      : [];
  const hintId = question.hint ? `${question.id}-hint` : undefined;

  return (
    <fieldset className="diagnostic-step" aria-describedby={hintId}>
      <legend ref={legendRef} tabIndex={-1}>{question.legend}</legend>
      {question.hint ? <p className="field-hint" id={hintId}>{question.hint}</p> : null}
      <div className={`diagnostic-options${question.mode === "multi" ? " diagnostic-options-multi" : ""}`}>
        {question.options.map((option) => {
          const checked = selectedValues.includes(option.value);
          return (
            <label key={option.value}>
              <input
                checked={checked}
                name={question.id}
                onChange={(event) => {
                  if (question.mode === "single") {
                    onChange(option.value);
                    return;
                  }
                  onChange(updateMultiSelection(
                    question,
                    selectedValues,
                    option.value,
                    event.target.checked,
                  ));
                }}
                type={question.mode === "single" ? "radio" : "checkbox"}
                value={option.value}
              />
              <span>{option.label}</span>
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}

function updateMultiSelection(
  question: DiagnosticQuestion,
  current: readonly string[],
  changedValue: string,
  checked: boolean,
): readonly string[] {
  const changedOption = question.options.find((option) => option.value === changedValue);
  if (!changedOption) return current;
  if (!checked) return current.filter((value) => value !== changedValue);
  if (changedOption.exclusive) return [changedValue];

  const exclusiveValues = new Set(
    question.options.filter((option) => option.exclusive).map((option) => option.value),
  );
  return [...current.filter((value) => !exclusiveValues.has(value)), changedValue];
}

type ResultProps = {
  answers: DiagnosticAnswerMap;
  context: DiagnosticContextId;
  definition: ReturnType<typeof getDiagnosticContextDefinition>;
  outcome: ReturnType<typeof buildAdaptiveDiagnosticOutcome>;
  quote: BillingV2PublicQuote | null;
  quoteError: string | null;
  quotePending: boolean;
  recommendedPreset: BillingV2PublicCatalog["presets"][number] | null;
  selection: BillingV2PublicSelection | null;
  onRestart: () => void;
};

function DiagnosticResult({
  answers,
  context,
  definition,
  outcome,
  quote,
  quoteError,
  quotePending,
  recommendedPreset,
  selection,
  onRestart,
}: ResultProps) {
  const summary = describeDiagnosticAnswers(context, answers);
  const formulaHref = selection ? buildFormulaHref(selection) : null;
  const hasFormula = outcome.recommendation?.status === "standard"
    && recommendedPreset !== null
    && formulaHref !== null;

  return (
    <section className="diagnostic-result" aria-live="polite">
      <div className="diagnostic-result-main">
        <p className="eyebrow">Votre orientation</p>
        <h2>{outcome.guidance.title}</h2>
        <p>{outcome.guidance.body}</p>

        {outcome.guidance.points.length > 0 ? (
          <ul className="check-list diagnostic-guidance-points">
            {outcome.guidance.points.map((point) => <li key={point}>{point}</li>)}
          </ul>
        ) : null}

        {hasFormula ? (
          <div className="diagnostic-formula-card">
            <p className="card-kicker">Parcours standard disponible</p>
            <h3>{recommendedPreset.name}</h3>
            <p>{recommendedPreset.description}</p>
            <div className="diagnostic-price">
              <strong>
                {quotePending
                  ? "Calcul du tarif…"
                  : quote
                    ? `${formatCurrencyFromCents(quote.monthlyAfterDiscountCents)} / mois`
                    : "Tarif à recalculer"}
              </strong>
              <span>
                {quoteError ?? "Le montant est recalculé à partir du catalogue avant toute souscription."}
              </span>
            </div>
            <div className="diagnostic-result-actions">
              <Link className="button" href={formulaHref}>Personnaliser cette formule</Link>
              <Link className="text-link" href={`/formules/${recommendedPreset.code}`}>Voir la formule</Link>
            </div>
          </div>
        ) : (
          <div className="diagnostic-cadrage-card">
            <p className="card-kicker">Suite recommandée</p>
            <h3>Un échange est préférable avant de chiffrer.</h3>
            <p>
              {"Vos r\u00e9ponses d\u00e9crivent un environnement qui m\u00e9rite d'\u00eatre v\u00e9rifi\u00e9 avant de proposer une solution ou un tarif."}
            </p>
          </div>
        )}
      </div>

      <aside className="diagnostic-result-details" aria-labelledby="diagnostic-summary-title">
        <h3 id="diagnostic-summary-title">Résumé de vos réponses</h3>
        <dl className="diagnostic-answer-summary">
          {summary.map((item) => (
            <div key={item.label}>
              <dt>{item.label}</dt>
              <dd>{item.value}</dd>
            </div>
          ))}
        </dl>
        <button className="button button-secondary" onClick={onRestart} type="button">
          Recommencer ce diagnostic
        </button>
        <Link className="text-link" href="/diagnostic">Choisir un autre sujet</Link>
      </aside>

      <div className="diagnostic-contact-panel">
        <div className="diagnostic-contact-copy">
          <p className="eyebrow">{"Besoin d'un avis humain ?"}</p>
          <h2>Transmettez ce diagnostic avec vos coordonnées.</h2>
          <p>
            {"Le r\u00e9sum\u00e9 est d\u00e9j\u00e0 pr\u00e9par\u00e9. Vous pouvez le compl\u00e9ter avant l'envoi ; aucune souscription n'est cr\u00e9\u00e9e par ce formulaire."}
          </p>
        </div>
        <ContactForm
          defaultMessage={buildDiagnosticContactMessage(context, answers)}
          defaultSubject={definition.contactSubject}
          formuleCode={hasFormula ? recommendedPreset.code : null}
          submitLabel="Envoyer mon diagnostic"
        />
      </div>
    </section>
  );
}

function buildFormulaHref(selection: BillingV2PublicSelection) {
  const params = billingV2SelectionToSearchParams(selection);
  params.set("source", "diagnostic");
  return `/formules/${selection.presetCode}?${params.toString()}`;
}
