"use client";

import Link from "next/link";
import { useEffect, useMemo, useRef, useState } from "react";

import type {
  DiagnosticAnswers,
  DiagnosticDataKind,
  DiagnosticRecommendationReasonCode,
  DiagnosticRecommendationWarningCode,
  ResolvedPublicPackManifest,
} from "@kermaria/shared";

import {
  formatCommercialAmountFromCents,
  formatFiscalMention,
} from "@/lib/fiscal-formatters";
import { buildDiagnosticBeforeAfterSummary } from "@/lib/diagnostic-before-after";
import { configurationToQueryString } from "@/lib/public-configurator";
import { recommendOffer } from "@/lib/public-diagnostic";

type PublicDiagnosticWizardProps = {
  packs: ResolvedPublicPackManifest[];
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
  storage_within_pack: "Le volume estimé reste compatible avec le pack proposé.",
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
      "Vous ne connaissez pas encore le volume exact. L'estimation reste valable, mais il faudra le vérifier avant activation.",
  },
  backup_frequency_unknown: {
    title: "Vérifier la fréquence de vos sauvegardes",
    body:
      "Vous ne savez pas encore à quelle fréquence vos données importantes sont sauvegardées.",
  },
  storage_requires_quote: {
    title: "Prévoir un cadrage stockage",
    body:
      "Le volume indiqué dépasse les variantes standards proposées en ligne. Une vérification est nécessaire avant de chiffrer.",
  },
  users_require_quote: {
    title: "Valider le nombre d'utilisateurs",
    body:
      "Le nombre d'utilisateurs indiqué sort des packs standards. Un cadrage permet d'éviter une configuration sous-dimensionnée.",
  },
  windows_storage_requires_quote: {
    title: "Cadrer le volume pour le bureau Windows",
    body:
      "Le volume indiqué dépasse la variante standard du bureau Windows à distance proposée en ligne.",
  },
  windows_team_requires_quote: {
    title: "Cadrer le bureau Windows partagé",
    body:
      "Un bureau Windows distant pour plusieurs personnes demande une vérification technique avant proposition.",
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

export function PublicDiagnosticWizard({ packs }: PublicDiagnosticWizardProps) {
  const [answers, setAnswers] = useState<DiagnosticAnswers>(INITIAL_ANSWERS);
  const [step, setStep] = useState(0);
  const [completed, setCompleted] = useState(false);
  const stepTitleRef = useRef<HTMLLegendElement | null>(null);
  const recommendation = useMemo(
    () => recommendOffer(answers, packs),
    [answers, packs],
  );
  const recommendedPack =
    recommendation.offerId
      ? (packs.find((pack) => pack.key === recommendation.offerId) ?? null)
      : null;
  const defaultVariant = recommendedPack?.variantsByCommitment[1].monthly;
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

  if (completed) {
    const fiscalRegime = defaultVariant?.offer.fiscalRegime ?? "franchise_base";
    const fiscalMention = formatFiscalMention(
      fiscalRegime,
      defaultVariant?.offer.fiscalMention,
    );
    const beforeAfterSummary = buildDiagnosticBeforeAfterSummary({
      answers,
      recommendation,
      pack: recommendedPack,
    });

    return (
      <section className="diagnostic-result" aria-live="polite">
        <div className="diagnostic-result-main">
          <p className="eyebrow">Résultat immédiat</p>
          {recommendedPack && defaultVariant ? (
            <>
              <h2>Votre besoin correspond au {recommendedPack.label}</h2>
              <p>{recommendedPack.description}</p>
              <dl className="diagnostic-price">
                <div>
                  <dt>À partir de</dt>
                  <dd>
                    <strong>
                      {formatCommercialAmountFromCents(
                        defaultVariant.monthlyPriceAmountCents,
                        { fiscalRegime, suffix: " / mois" },
                      )}
                    </strong>
                    <span>{fiscalMention}</span>
                  </dd>
                </div>
                <div>
                  <dt>Mise en service</dt>
                  <dd>
                    <strong>
                      {formatCommercialAmountFromCents(
                        defaultVariant.setupFeeAmountCents,
                        { fiscalRegime },
                      )}
                    </strong>
                    <span>{fiscalMention}</span>
                  </dd>
                </div>
              </dl>
              <div className="diagnostic-result-actions">
                {recommendation.configuration ? (
                  <Link
                    className="button"
                    href={`/configurer?${configurationToQueryString(
                      recommendation.configuration,
                    )}&source=diagnostic`}
                  >
                    Personnaliser cette configuration
                  </Link>
                ) : null}
                <Link className="text-link" href={`/offres/${recommendedPack.slug}`}>
                  Voir la fiche complète
                </Link>
              </div>
            </>
          ) : (
            <>
              <h2>Votre besoin nécessite un cadrage</h2>
              <p>
                Les réponses indiquent un besoin qui ne correspond pas à une
                variante standard proposée en ligne.
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
            Qui utilisera le service ?
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
              <option value="1">1</option>
              <option value="2">2</option>
              <option value="3">3-5</option>
              <option value="6">6+</option>
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
            Quelles données souhaitez-vous protéger ?
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
              onChange={(event) =>
                setAnswers((current) => ({
                  ...current,
                  estimatedStorageGb: event.target.value
                    ? Number(event.target.value)
                    : null,
                }))}
            >
              <option value="">Je ne sais pas</option>
              <option value="8">Moins de 10 Go</option>
              <option value="32">10 à 30 Go</option>
              <option value="64">30 à 60 Go</option>
              <option value="64">Plus de 60 Go</option>
            </select>
          </label>
        </fieldset>
      ) : null}

      {step === 2 ? (
        <fieldset className="diagnostic-step">
          <legend ref={stepTitleRef} tabIndex={-1}>
            Quels accès sont utiles ?
          </legend>
          <NullableBooleanQuestion
            id="needsRemoteFiles"
            label="Souhaitez-vous accéder à vos fichiers à distance ?"
            value={answers.needsRemoteFiles}
            onChange={(needsRemoteFiles) =>
              setAnswers((current) => ({ ...current, needsRemoteFiles }))}
          />
          <NullableBooleanQuestion
            hint="Un VPN peut servir à créer ce type d'accès lorsque c'est nécessaire."
            id="needsVpn"
            label="Avez-vous besoin d'un accès sécurisé à distance à un réseau ou à des ressources internes ?"
            value={answers.needsVpn}
            onChange={(needsVpn) =>
              setAnswers((current) => ({ ...current, needsVpn }))}
          />
          <NullableBooleanQuestion
            id="needsWindowsDesktop"
            label="Souhaitez-vous disposer d'un bureau Windows accessible à distance avec vos logiciels et vos fichiers ?"
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
            souhaitez-vous retrouver vos fichiers ?
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
            <span>À quelle fréquence vos données importantes sont-elles sauvegardées ?</span>
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
            <span>Quand avez-vous testé une restauration pour la dernière fois ?</span>
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
        <span aria-hidden="true">Avant → Après</span>
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
