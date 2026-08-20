"use client";

import Link from "next/link";
import { useEffect, useMemo, useRef, useState } from "react";

import type {
  BillingV2PublicCatalog,
  BillingV2PublicQuote,
  BillingV2PublicSelection,
  DiagnosticAnswers,
  DiagnosticDataKind,
  DiagnosticRecommendationReasonCode,
  DiagnosticRecommendationWarningCode,
} from "@kermaria/shared";

import { buildDiagnosticBeforeAfterSummary } from "@/lib/diagnostic-before-after";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { billingV2SelectionToSearchParams } from "@/lib/billing-v2-selection";
import { recommendOffer } from "@/lib/public-diagnostic";

type PublicDiagnosticWizardProps = {
  catalog: BillingV2PublicCatalog;
};

const STEPS = ["Profil", "Données", "Accès", "Reprise"] as const;

const DATA_KIND_OPTIONS: Array<{ value: DiagnosticDataKind; label: string }> = [
  { value: "personal_documents", label: "Documents personnels ou administratifs" },
  { value: "business_documents", label: "Documents professionnels" },
  { value: "photos", label: "Photos" },
  { value: "association_data", label: "Données d'une association" },
  { value: "work_files", label: "Fichiers de travail" },
  { value: "other_important_files", label: "Autres fichiers importants" },
];

const REASON_LABELS: Record<DiagnosticRecommendationReasonCode, string> = {
  simple_backup: "Vous cherchez surtout à protéger des fichiers importants.",
  needs_remote_files: "Vous souhaitez retrouver vos fichiers à distance.",
  needs_vpn: "Vous avez besoin d'un accès sécurisé à distance.",
  needs_windows_desktop:
    "Vous avez besoin d'un bureau Windows accessible à distance.",
  team_or_structure: "Le besoin concerne plusieurs utilisateurs ou une structure.",
  association_context: "Le contexte association demande un cadre plus structuré.",
  storage_within_pack: "Le volume estimé correspond à un palier disponible en ligne.",
  strong_recovery_need:
    "Vous avez indiqué avoir besoin de retrouver rapidement vos fichiers.",
};

const WARNING_MESSAGES: Record<
  DiagnosticRecommendationWarningCode,
  { title: string; body: string }
> = {
  storage_unknown: {
    title: "Confirmer le volume à protéger",
    body:
      "Vous ne connaissez pas encore le volume exact. La formule proposée reste ajustable avant la souscription.",
  },
  backup_frequency_unknown: {
    title: "Vérifier la fréquence de vos sauvegardes",
    body:
      "Vous ne savez pas encore à quelle fréquence vos données importantes sont sauvegardées.",
  },
  storage_requires_quote: {
    title: "Prévoir un cadrage stockage",
    body:
      "Le volume indiqué dépasse les paliers de stockage proposés en ligne. Une vérification est nécessaire avant de chiffrer.",
  },
  users_require_quote: {
    title: "Valider le nombre d'utilisateurs",
    body:
      "Le besoin dépasse 11 utilisateurs au total. Un cadrage permet de dimensionner correctement les comptes et les accès.",
  },
  other_structure_requires_review: {
    title: "Préciser votre contexte",
    body:
      "Votre profil ne correspond pas aux cas simples du diagnostic public. Quelques échanges permettront d'orienter correctement la solution.",
  },
  no_recent_restore_test: {
    title: "Tester une restauration",
    body:
      "Vous n'avez pas indiqué de test récent. Le premier réflexe est de vérifier qu'un fichier peut réellement être restauré.",
  },
  no_continuity_plan: {
    title: "Prévoir quoi faire en cas de panne",
    body:
      "Vous n'avez pas encore indiqué comment vous retrouveriez vos fichiers si votre matériel devenait indisponible.",
  },
};

const INITIAL_ANSWERS: DiagnosticAnswers = {
  customerType: "individual",
  users: 1,
  dataKinds: [],
  estimatedStorageGb: null,
  needsRemoteFiles: null,
  needsVpn: null,
  needsWindowsDesktop: null,
  recoveryImportance: "normal",
  backupFrequency: "unknown",
  restoreTestRecency: "unknown",
  continuityPlan: "unknown",
};

export function PublicDiagnosticWizard({ catalog }: PublicDiagnosticWizardProps) {
  const [answers, setAnswers] = useState<DiagnosticAnswers>(INITIAL_ANSWERS);
  const [step, setStep] = useState(0);
  const [completed, setCompleted] = useState(false);
  const [quote, setQuote] = useState<BillingV2PublicQuote | null>(null);
  const [quotePending, setQuotePending] = useState(false);
  const [quoteError, setQuoteError] = useState<string | null>(null);
  const stepTitleRef = useRef<HTMLLegendElement | null>(null);
  const recommendation = useMemo(
    () => recommendOffer(answers, catalog),
    [answers, catalog],
  );
  const selection = recommendation.selection;
  const recommendedPreset = selection
    ? (catalog.presets.find((preset) => preset.code === selection.presetCode) ?? null)
    : null;
  const canContinue = step !== 1 || answers.dataKinds.length > 0;

  useEffect(() => {
    if (completed) {
      return;
    }

    const stepTitle = stepTitleRef.current;
    const frame = window.requestAnimationFrame(() => {
      stepTitle?.focus({ preventScroll: true });
      stepTitle?.scrollIntoView({ block: "start", inline: "nearest" });
    });

    return () => window.cancelAnimationFrame(frame);
  }, [completed, step]);

  useEffect(() => {
    if (!completed || !selection) {
      return;
    }

    const controller = new AbortController();

    fetch("/api/formules/devis", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(selection),
      signal: controller.signal,
    })
      .then(async (response) => {
        if (!response.ok) {
          throw new Error(String(response.status));
        }

        return (await response.json()) as BillingV2PublicQuote;
      })
      .then((payload) => {
        setQuote(payload);
        setQuotePending(false);
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) {
          return;
        }

        setQuote(null);
        setQuotePending(false);
        setQuoteError(
          error instanceof Error && error.message === "400"
            ? "Cette configuration n'est plus disponible dans le catalogue."
            : "Le tarif est temporairement indisponible. La formule le recalculera avant toute souscription.",
        );
      });

    return () => controller.abort();
  }, [completed, selection]);

  if (completed) {
    const beforeAfterSummary = buildDiagnosticBeforeAfterSummary({
      answers,
      recommendation,
      catalog,
    });
    const formulaHref = selection ? buildFormulaHref(selection) : null;

    return (
      <section className="diagnostic-result" aria-live="polite">
        <div className="diagnostic-result-main">
          <p className="eyebrow">Résultat immédiat</p>
          {recommendedPreset && selection && formulaHref ? (
            <>
              <h2>Votre besoin correspond à la formule {recommendedPreset.name}</h2>
              <p>{recommendedPreset.description}</p>
              <dl className="diagnostic-price">
                <div>
                  <dt>Votre configuration</dt>
                  <dd>
                    <strong>
                      {quotePending
                        ? "Calcul du tarif…"
                        : quote
                          ? `${formatCurrencyFromCents(quote.monthlyAfterDiscountCents)} / mois`
                          : "Tarif à recalculer"}
                    </strong>
                    <span>
                      {quoteError
                        ?? "Montant calculé par Billing V2 à partir de la sélection ci-dessous, hors taxes applicables."}
                    </span>
                  </dd>
                </div>
              </dl>
              <div className="diagnostic-result-actions">
                <Link className="button" href={formulaHref}>
                  Personnaliser cette configuration
                </Link>
                <Link
                  className="text-link"
                  href={`/formules/${recommendedPreset.code}`}
                >
                  Voir la formule
                </Link>
              </div>
            </>
          ) : (
            <>
              <h2>Votre besoin nécessite un cadrage</h2>
              <p>
                Les réponses indiquent un besoin qui dépasse les options
                actuellement proposées en ligne.
              </p>
              <Link className="button" href="/contact">
                Demander un cadrage
              </Link>
            </>
          )}

          <DiagnosticBeforeAfterBlock summary={beforeAfterSummary} />
        </div>

        <div className="diagnostic-result-details">
          <h3>Pourquoi ?</h3>
          <ul className="check-list">
            {recommendation.reasons.slice(0, 4).map((reason) => (
              <li key={reason}>{REASON_LABELS[reason]}</li>
            ))}
          </ul>

          {recommendation.warnings.length ? (
            <>
              <h3>Points à vérifier</h3>
              <ul className="diagnostic-warning-list">
                {recommendation.warnings.map((warning) => (
                  <li key={warning}>
                    <strong>{WARNING_MESSAGES[warning].title}</strong>
                    <span>{WARNING_MESSAGES[warning].body}</span>
                  </li>
                ))}
              </ul>
            </>
          ) : null}

          <button
            className="button button-secondary"
            onClick={() => {
              setAnswers(INITIAL_ANSWERS);
              setStep(0);
              setCompleted(false);
              setQuote(null);
              setQuotePending(false);
              setQuoteError(null);
            }}
            type="button"
          >
            Recommencer le diagnostic
          </button>
        </div>
      </section>
    );
  }

  return (
    <section className="diagnostic-wizard" aria-label="Diagnostic interactif">
      <div className="diagnostic-progress" aria-label="Progression" aria-live="polite">
        {STEPS.map((label, index) => (
          <span
            className={index === step ? "is-active" : index < step ? "is-done" : ""}
            key={label}
          >
            {index + 1}. {label}
          </span>
        ))}
      </div>

      {step === 0 ? (
        <fieldset className="diagnostic-step">
          <legend ref={stepTitleRef} tabIndex={-1}>
            Qui utilisera le service
          </legend>
          <div className="diagnostic-options">
            {[
              ["individual", "Particulier"],
              ["business", "Indépendant / petite entreprise"],
              ["association", "Association"],
              ["other", "Autre structure"],
            ].map(([value, label]) => (
              <label key={value}>
                <input
                  checked={answers.customerType === value}
                  name="customerType"
                  onChange={() =>
                    setAnswers((current) => ({
                      ...current,
                      customerType: value as DiagnosticAnswers["customerType"],
                    }))}
                  type="radio"
                />
                <span>{label}</span>
              </label>
            ))}
          </div>

          <label className="diagnostic-field">
            <span>Nombre d&apos;utilisateurs</span>
            <select
              value={String(answers.users ?? "")}
              onChange={(event) =>
                setAnswers((current) => ({
                  ...current,
                  users: event.target.value ? Number(event.target.value) : null,
                }))}
            >
              {Array.from({ length: 11 }, (_, index) => index + 1).map((users) => (
                <option key={users} value={users}>
                  {users}
                </option>
              ))}
              <option value="12">12 ou plus</option>
            </select>
          </label>
        </fieldset>
      ) : null}

      {step === 1 ? (
        <fieldset
          aria-describedby={!canContinue ? "diagnostic-data-error" : undefined}
          className="diagnostic-step"
        >
          <legend ref={stepTitleRef} tabIndex={-1}>
            Quelles données souhaitez-vous protéger ?
          </legend>
          <div className="diagnostic-options diagnostic-options-multi">
            {DATA_KIND_OPTIONS.map((option) => (
              <label key={option.value}>
                <input
                  checked={answers.dataKinds.includes(option.value)}
                  onChange={() => toggleDataKind(option.value)}
                  type="checkbox"
                />
                <span>{option.label}</span>
              </label>
            ))}
          </div>
          {!canContinue ? (
            <p className="field-error" id="diagnostic-data-error">
              Sélectionnez au moins un type de données.
            </p>
          ) : null}

          <label className="diagnostic-field">
            <span>Volume à protéger</span>
            <select
              value={String(answers.estimatedStorageGb ?? "")}
              onChange={(event) => {
                const value = event.target.value;
                setAnswers((current) => ({
                  ...current,
                  estimatedStorageGb:
                    value === ""
                      ? null
                      : value === "above_public_max"
                        ? "above_public_max"
                        : Number(value),
                }));
              }}
            >
              <option value="">Je ne sais pas</option>
              <option value="16">Jusqu&apos;à 16 Go</option>
              <option value="32">Jusqu&apos;à 32 Go</option>
              <option value="64">Jusqu&apos;à 64 Go</option>
              <option value="128">Jusqu&apos;à 128 Go</option>
              <option value="256">Jusqu&apos;à 256 Go</option>
              <option value="above_public_max">Plus de 256 Go</option>
            </select>
          </label>
        </fieldset>
      ) : null}

      {step === 2 ? (
        <fieldset className="diagnostic-step">
          <legend ref={stepTitleRef} tabIndex={-1}>
            Quels accès sont utiles ?
          </legend>
          <NullableBooleanQuestion
            id="needsRemoteFiles"
            label="Souhaitez-vous accéder à vos fichiers à distance ?"
            value={answers.needsRemoteFiles}
            onChange={(needsRemoteFiles) =>
              setAnswers((current) => ({ ...current, needsRemoteFiles }))}
          />
          <NullableBooleanQuestion
            hint="Un VPN peut servir à créer ce type d'accès lorsque c'est nécessaire."
            id="needsVpn"
            label="Avez-vous besoin d'un accès sécurisé à distance à un réseau ou à des ressources internes ?"
            value={answers.needsVpn}
            onChange={(needsVpn) =>
              setAnswers((current) => ({ ...current, needsVpn }))}
          />
          <NullableBooleanQuestion
            id="needsWindowsDesktop"
            label="Souhaitez-vous disposer d'un bureau Windows accessible à distance avec vos logiciels et vos fichiers ?"
            value={answers.needsWindowsDesktop}
            onChange={(needsWindowsDesktop) =>
              setAnswers((current) => ({
                ...current,
                needsWindowsDesktop,
              }))}
          />
        </fieldset>
      ) : null}

      {step === 3 ? (
        <fieldset className="diagnostic-step">
          <legend ref={stepTitleRef} tabIndex={-1}>
            En cas de panne ou de perte de votre matériel, à quelle vitesse
            souhaitez-vous retrouver vos fichiers
          </legend>
          <label className="diagnostic-field">
            <span>Délai souhaité pour retrouver vos fichiers</span>
            <select
              value={answers.recoveryImportance}
              onChange={(event) =>
                setAnswers((current) => ({
                  ...current,
                  recoveryImportance:
                    event.target.value as DiagnosticAnswers["recoveryImportance"],
                }))}
            >
              <option value="low">Faible - je peux attendre plusieurs jours</option>
              <option value="normal">Normale - retrouver mes fichiers rapidement</option>
              <option value="high">Élevée - j&apos;ai besoin de retrouver mes fichiers très rapidement</option>
            </select>
          </label>

          <label className="diagnostic-field">
            <span>À quelle fréquence vos données importantes sont-elles sauvegardées ?</span>
            <select
              value={answers.backupFrequency}
              onChange={(event) =>
                setAnswers((current) => ({
                  ...current,
                  backupFrequency:
                    event.target.value as DiagnosticAnswers["backupFrequency"],
                }))}
            >
              <option value="daily">Tous les jours</option>
              <option value="weekly">Chaque semaine</option>
              <option value="monthly">Chaque mois</option>
              <option value="rarely">Rarement</option>
              <option value="unknown">Je ne sais pas</option>
            </select>
          </label>

          <label className="diagnostic-field">
            <span>Quand avez-vous testé une restauration pour la dernière fois ?</span>
            <select
              value={answers.restoreTestRecency}
              onChange={(event) =>
                setAnswers((current) => ({
                  ...current,
                  restoreTestRecency:
                    event.target.value as DiagnosticAnswers["restoreTestRecency"],
                }))}
            >
              <option value="less_than_3_months">Il y a moins de 3 mois</option>
              <option value="less_than_12_months">Il y a moins d&apos;un an</option>
              <option value="more_than_12_months">Il y a plus d&apos;un an</option>
              <option value="never">Jamais</option>
              <option value="unknown">Je ne sais pas</option>
            </select>
          </label>

          <label className="diagnostic-field">
            <span>
              Savez-vous comment continuer à accéder à vos données si votre
              matériel devient indisponible ?
            </span>
            <select
              value={answers.continuityPlan}
              onChange={(event) =>
                setAnswers((current) => ({
                  ...current,
                  continuityPlan:
                    event.target.value as DiagnosticAnswers["continuityPlan"],
                }))}
            >
              <option value="yes">Oui</option>
              <option value="partial">En partie</option>
              <option value="no">Non</option>
              <option value="unknown">Je ne sais pas</option>
            </select>
          </label>
        </fieldset>
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
            if (step === STEPS.length - 1) {
              setQuote(null);
              setQuotePending(recommendation.selection !== null);
              setQuoteError(null);
              setCompleted(true);
            } else {
              setStep((current) => current + 1);
            }
          }}
          type="button"
        >
          {step === STEPS.length - 1 ? "Voir mon résultat" : "Continuer"}
        </button>
      </div>
    </section>
  );

  function toggleDataKind(kind: DiagnosticDataKind) {
    setAnswers((current) => ({
      ...current,
      dataKinds: current.dataKinds.includes(kind)
        ? current.dataKinds.filter((item) => item !== kind)
        : [...current.dataKinds, kind],
    }));
  }
}

function buildFormulaHref(selection: BillingV2PublicSelection) {
  const params = billingV2SelectionToSearchParams(selection);
  params.set("source", "diagnostic");
  return `/formules/${selection.presetCode}?${params.toString()}`;
}

function DiagnosticBeforeAfterBlock({
  summary,
}: {
  summary: {
    title: string;
    items: Array<{ before: string; after: string }>;
  };
}) {
  return (
    <section className="diagnostic-before-after" aria-labelledby="before-after-title">
      <div className="diagnostic-before-after-heading">
        <h3 id="before-after-title">{summary.title}</h3>
        <span aria-hidden="true">Avant - Après</span>
      </div>
      <div className="diagnostic-before-after-grid">
        <div>
          <h4>Avant</h4>
          <ul>
            {summary.items.map((item) => (
              <li key={`before-${item.before}`}>{item.before}</li>
            ))}
          </ul>
        </div>
        <div>
          <h4>Après</h4>
          <ul>
            {summary.items.map((item) => (
              <li key={`after-${item.after}`}>{item.after}</li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  );
}

function NullableBooleanQuestion({
  id,
  label,
  hint,
  value,
  onChange,
}: {
  id: string;
  label: string;
  hint?: string;
  value: boolean | null;
  onChange: (value: boolean | null) => void;
}) {
  const labelId = `${id}-label`;
  const hintId = hint ? `${id}-hint` : undefined;

  return (
    <div className="diagnostic-inline-question">
      <span id={labelId}>{label}</span>
      {hint ? (
        <p className="field-hint" id={hintId}>
          {hint}
        </p>
      ) : null}
      <div
        aria-describedby={hintId}
        aria-labelledby={labelId}
        className="diagnostic-yes-no"
        role="radiogroup"
      >
        {[
          ["yes", "Oui"],
          ["no", "Non"],
          ["unknown", "Je ne sais pas"],
        ].map(([optionValue, optionLabel]) => (
          <label key={optionValue}>
            <input
              checked={
                optionValue === "unknown"
                  ? value === null
                  : value === (optionValue === "yes")
              }
              name={id}
              onChange={() =>
                onChange(
                  optionValue === "unknown" ? null : optionValue === "yes",
                )}
              type="radio"
            />
            <span>{optionLabel}</span>
          </label>
        ))}
      </div>
    </div>
  );
}
