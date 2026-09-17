"use client";

import Link from "next/link";
import type {
  BillingV2PublicCatalog,
  BillingV2PublicQuote,
  BillingV2PublicSelection,
  PreDiagnosticConfiguration,
} from "@kermaria/shared";
import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type CSSProperties,
  type FormEvent,
  type ReactNode,
} from "react";

import { FormMessage } from "@/components/FormMessage";
import { requestBffJson } from "@/lib/client-api";
import type { DiagnosticCallbackFieldErrors } from "@/lib/diagnostic-callback";
import {
  evaluatePreDiagnostic,
  evaluatePreDiagnosticWithConfiguration,
  configuredQuestionsForProfile,
  questionsForProfile,
  scoringAnswersForProfile,
  type PreDiagnosticAnswers,
  type PreDiagnosticProfile,
} from "@/lib/pre-diagnostic";
import {
  commercialQuestionsForProfile,
  isPreDiagnosticHumanReviewContext,
  recommendPreDiagnosticOffer,
  type CommercialRecommendation,
} from "@/lib/pre-diagnostic-commerce";
import type { DiagnosticContextId } from "@/lib/diagnostic-context";
import { billingV2SelectionToSearchParams } from "@/lib/billing-v2-selection";
import { formatCurrencyFromCents } from "@/lib/formatters";

type CallbackState = "idle" | "open" | "submitting" | "success" | "error";
type CallbackForm = {
  name: string;
  phone: string;
  email: string;
  organisation: string;
  preferredTime: string;
  comment: string;
  consent: boolean;
  website: string;
};

const EMPTY_CALLBACK_FORM: CallbackForm = {
  name: "",
  phone: "",
  email: "",
  organisation: "",
  preferredTime: "",
  comment: "",
  consent: false,
  website: "",
};

type PublicDiagnosticWizardProps = {
  catalog: BillingV2PublicCatalog;
  context: DiagnosticContextId;
  /** null tant qu'aucune configuration v2 n'est publiee. */
  preDiagnosticConfiguration: PreDiagnosticConfiguration | null;
  /** Snapshot publié effectivement rendu au visiteur. */
  diagnosticConfigurationVersion: number;
};

export function PublicDiagnosticWizard({
  catalog,
  context,
  preDiagnosticConfiguration,
  diagnosticConfigurationVersion,
}: PublicDiagnosticWizardProps) {
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [step, setStep] = useState(0);
  const [completed, setCompleted] = useState(false);
  const stepTitleRef = useRef<HTMLLegendElement>(null);
  const profile = (answers.profile ?? null) as PreDiagnosticProfile | null;
  const questions = useMemo(
    () => {
      const healthQuestions = preDiagnosticConfiguration
        ? configuredQuestionsForProfile(preDiagnosticConfiguration, profile, answers)
        : questionsForProfile(profile);
      const commercialQuestions = commercialQuestionsForProfile(profile, context, preDiagnosticConfiguration ?? undefined);
      // Un lien depuis une page Réseau, Hébergement ou Domaine doit être
      // reconnu dès le début, même si son issue reste volontairement humaine.
      return isPreDiagnosticHumanReviewContext(context, preDiagnosticConfiguration ?? undefined)
        ? [healthQuestions[0], ...commercialQuestions, ...healthQuestions.slice(1)]
        : [...healthQuestions, ...commercialQuestions];
    },
    [answers, context, preDiagnosticConfiguration, profile],
  );
  const current = questions[step] ?? null;
  const result = completed && profile
    ? preDiagnosticConfiguration
      ? evaluatePreDiagnosticWithConfiguration(preDiagnosticConfiguration, scoringAnswersForProfile(answers, profile, preDiagnosticConfiguration))
      : evaluatePreDiagnostic(scoringAnswersForProfile(answers, profile))
    : null;
  const commercialRecommendation = completed && profile
    ? recommendPreDiagnosticOffer(answers, profile, context, catalog, preDiagnosticConfiguration ?? undefined)
    : null;

  useEffect(() => {
    if (step > 0) {
      stepTitleRef.current?.focus();
    }
  }, [step]);

  function changeAnswer(id: string, value: string) {
    setAnswers((currentAnswers) =>
      id === "profile" ? { profile: value } : { ...currentAnswers, [id]: value },
    );
    if (id === "profile") {
      setStep(0);
    }
  }

  if (completed && result && profile) {
    return (
      <DiagnosticResult
        answers={scoringAnswersForProfile(answers, profile, preDiagnosticConfiguration ?? undefined)}
        commercialAnswers={answers}
        catalog={catalog}
        diagnosticConfigurationVersion={diagnosticConfigurationVersion}
        context={context}
        commercialRecommendation={commercialRecommendation ?? {
          kind: "human_review",
          title: "Ce besoin mérite un échange avant de choisir une offre",
          reason: "Aucune orientation automatique n'a pu être déterminée.",
          need: null,
          selection: null,
          offerName: null,
          selectedStorageGb: null,
          commercialProfileId: null,
        }}
        onRestart={() => {
          setAnswers({});
          setStep(0);
          setCompleted(false);
        }}
        result={result}
      />
    );
  }

  if (!current) return null;

  const hasProfile = profile !== null;
  const progress = hasProfile
    ? Math.round(((step + 1) / questions.length) * 100)
    : 0;
  const progressText = hasProfile
    ? `Étape ${step + 1} sur ${questions.length}`
    : "Étape 1 — choisissez votre profil";

  return (
    <div className="diagnostic-page">
      <header className="diagnostic-header">
        <div className="diagnostic-header-copy">
          <span aria-hidden="true" className="diagnostic-header-icon">✓</span>
          <div>
            <p className="eyebrow">Pré-diagnostic informatique</p>
            <h1>Faites le point, simplement.</h1>
            <p>Quelques questions concrètes pour identifier vos points solides et les priorités à examiner.</p>
          </div>
        </div>
        <div aria-label="Fonctionnement" className="diagnostic-benefits">
          <article>
            <span className="diagnostic-benefit-icon">1</span>
            <div><h2>Adapté à votre situation</h2><p>Particulier, activité professionnelle ou association.</p></div>
          </article>
          <article>
            <span className="diagnostic-benefit-icon">2</span>
            <div><h2>Résultat immédiat</h2><p>Des priorités lisibles, sans jargon inutile.</p></div>
          </article>
        </div>
      </header>

      <section aria-label="Questionnaire de pré-diagnostic" className="diagnostic-wizard">
        <div className="diagnostic-wizard-toolbar">
          <div>
            <span className="diagnostic-context-badge">{current.category}</span>
            <p aria-live="polite" className="diagnostic-progress-text">{progressText}</p>
          </div>
          <span className="diagnostic-change-context">Vos réponses restent modifiables</span>
        </div>
        <div
          aria-label={`Progression : ${progress} %`}
          aria-valuemax={hasProfile ? questions.length : 1}
          aria-valuemin={0}
          aria-valuenow={hasProfile ? step + 1 : 0}
          className="diagnostic-progress-bar"
          role="progressbar"
          style={{ "--diagnostic-progress": `${progress}%` } as CSSProperties}
        ><span /></div>
        <fieldset className="diagnostic-step">
          <legend ref={stepTitleRef} tabIndex={-1}>{current.label}</legend>
          {current.hint ? <p className="field-hint">{current.hint}</p> : null}
          <div className="diagnostic-options">
            {current.options.map((option) => {
              const checked = answers[current.id] === option.value;
              return (
                <label className="diagnostic-option" data-selected={checked ? "true" : "false"} key={option.value}>
                  <input checked={checked} name={current.id} onChange={() => changeAnswer(current.id, option.value)} type="radio" value={option.value} />
                  <span>{option.label}</span>
                </label>
              );
            })}
          </div>
        </fieldset>
        {!answers[current.id] ? <p className="diagnostic-answer-hint" role="status">Choisissez une réponse pour continuer.</p> : null}
        <div className="diagnostic-actions">
          <button className="button button-secondary" disabled={step === 0} onClick={() => setStep((value) => Math.max(0, value - 1))} type="button">Précédent</button>
          <button className="button" disabled={current.required !== false && !answers[current.id]} onClick={() => {
            if (step === questions.length - 1) {
              setCompleted(true);
              return;
            }
            setStep((value) => value + 1);
          }} type="button">{step === questions.length - 1 ? "Voir mon résultat" : "Continuer"}</button>
        </div>
      </section>
    </div>
  );
}

function DiagnosticResult({
  answers,
  commercialAnswers,
  catalog,
  diagnosticConfigurationVersion,
  context,
  commercialRecommendation,
  result,
  onRestart,
}: {
  answers: PreDiagnosticAnswers;
  commercialAnswers: Record<string, string>;
  catalog: BillingV2PublicCatalog;
  diagnosticConfigurationVersion: number;
  context: DiagnosticContextId;
  commercialRecommendation: CommercialRecommendation;
  result: ReturnType<typeof evaluatePreDiagnostic>;
  onRestart: () => void;
}) {
  const [callbackState, setCallbackState] = useState<CallbackState>("idle");
  const [errors, setErrors] = useState<DiagnosticCallbackFieldErrors>({});
  const [form, setForm] = useState<CallbackForm>(EMPTY_CALLBACK_FORM);
  const callbackHeadingRef = useRef<HTMLHeadingElement>(null);
  const messageRef = useRef<HTMLDivElement>(null);
  const organisationVisible = answers.profile !== "individual";

  useEffect(() => {
    if (callbackState === "open") {
      callbackHeadingRef.current?.focus();
    }
  }, [callbackState]);

  function updateForm<Key extends keyof CallbackForm>(key: Key, value: CallbackForm[Key]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (callbackState === "submitting") return;

    setCallbackState("submitting");
    setErrors({});
    const response = await requestBffJson<
      { message: string },
      { field_errors?: DiagnosticCallbackFieldErrors }
    >("/api/diagnostic/callback", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ ...form, answers, configurationVersion: diagnosticConfigurationVersion }),
    });

    if (!response.ok) {
      setErrors(response.details?.field_errors ?? {});
      setCallbackState("error");
      requestAnimationFrame(() => messageRef.current?.focus());
      return;
    }

    setCallbackState("success");
    requestAnimationFrame(() => messageRef.current?.focus());
  }

  return (
    <div className="diagnostic-page">
      <section aria-live="polite" className="diagnostic-result">
        <div className="diagnostic-result-main">
          <div className="diagnostic-result-heading">
            <span aria-hidden="true" className="diagnostic-result-icon">✓</span>
            <div><p className="eyebrow">Votre première estimation</p><h1>{result.level}</h1></div>
          </div>
          <div aria-label={`Score global : ${result.score} sur 100`} className="diagnostic-score"><strong>{result.score}</strong><span>/ 100</span></div>
          <p className="diagnostic-result-lead">Ce résultat met en évidence les sujets à regarder en premier selon vos réponses. Il ne remplace pas une vérification technique sur place.</p>
          {result.priorities.length ? <section className="diagnostic-priorities"><h2>Vos priorités</h2><ol>{result.priorities.map((priority) => <li key={priority.title}><strong>{priority.title}</strong><span>{priority.body}</span></li>)}</ol></section> : null}
          {result.positives.length ? <section className="diagnostic-positives"><h2>Points positifs</h2><ul className="check-list">{result.positives.map((positive) => <li key={positive}>{positive}</li>)}</ul></section> : null}
          {commercialRecommendation.kind === "standard" && commercialRecommendation.selection ? <DiagnosticOffer
            catalog={catalog}
            commercialAnswers={commercialAnswers}
            context={context}
            diagnosticConfigurationVersion={diagnosticConfigurationVersion}
            profile={answers.profile as PreDiagnosticProfile}
            recommendation={commercialRecommendation}
          /> : null}
          {commercialRecommendation.kind === "human_review" ? <section className="diagnostic-human-review">
            <h2>{commercialRecommendation.title}</h2>
            <p>{commercialRecommendation.reason}</p>
            {callbackState === "idle" ? <button aria-controls="etre-rappele" className="button diagnostic-callback-cta" onClick={() => setCallbackState("open")} type="button">Être rappelé</button> : null}
          </section> : null}
          <button className="button button-secondary diagnostic-restart" onClick={onRestart} type="button">Recommencer le diagnostic</button>
        </div>
        <aside className="diagnostic-result-details">
          <h2>Analyse par catégorie</h2>
          <dl className="diagnostic-category-scores">{result.categories.map((category) => <div key={category.id}><dt>{category.label}</dt><dd><strong>{category.score}</strong><span>/ 100</span></dd></div>)}</dl>
          <p className="diagnostic-disclaimer">Cette première estimation s&apos;appuie uniquement sur vos réponses. Certaines situations nécessitent une vérification technique.</p>
        </aside>
      </section>
      {callbackState !== "idle" ? <CallbackFormPanel
        callbackState={callbackState}
        errors={errors}
        form={form}
        headingRef={callbackHeadingRef}
        messageRef={messageRef}
        onSubmit={submit}
        organisationVisible={organisationVisible}
        setCallbackState={setCallbackState}
        updateForm={updateForm}
      /> : null}
    </div>
  );
}

function DiagnosticOffer({
  catalog,
  commercialAnswers,
  context,
  diagnosticConfigurationVersion,
  profile,
  recommendation,
}: {
  catalog: BillingV2PublicCatalog;
  commercialAnswers: Record<string, string>;
  context: DiagnosticContextId;
  diagnosticConfigurationVersion: number;
  profile: PreDiagnosticProfile;
  recommendation: CommercialRecommendation;
}) {
  const [selection, setSelection] = useState<BillingV2PublicSelection | null>(null);
  const [quote, setQuote] = useState<BillingV2PublicQuote | null>(null);
  const [quoteError, setQuoteError] = useState(false);

  useEffect(() => {
    let active = true;
    // Les états affichés appartiennent à la précédente requête asynchrone :
    // ils doivent être vidés avant d'amorcer la recommandation suivante.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setSelection(null);
    setQuote(null);
    setQuoteError(false);
    const healthAnswers = Object.fromEntries(Object.entries(commercialAnswers).filter(([key]) => !key.startsWith("commercial")));
    const commercial = Object.fromEntries(Object.entries(commercialAnswers).filter(([key]) => key.startsWith("commercial")));
    void requestBffJson<{ kind: string; selection: BillingV2PublicSelection | null }>("/api/diagnostic/recommendation", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ configurationVersion: diagnosticConfigurationVersion, profile, context, healthAnswers, commercialAnswers: commercial }),
    }).then((response) => {
      if (!active) return;
      if (!response.ok || response.data.kind !== "standard" || !response.data.selection) { setQuoteError(true); return; }
      setSelection(response.data.selection);
    });
    return () => { active = false; };
  }, [commercialAnswers, context, diagnosticConfigurationVersion, profile]);

  useEffect(() => {
    if (!selection) return;
    let active = true;
    void requestBffJson<BillingV2PublicQuote>("/api/formules/devis", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(selection) }).then((response) => {
      if (!active) return;
      if (response.ok) setQuote(response.data); else setQuoteError(true);
    });
    return () => { active = false; };
  }, [selection]);

  const preset = catalog.presets.find((item) => item.code === selection?.presetCode) ?? null;
  const quoteAvailable = quote?.checkoutAvailable === true;
  const href = preset && selection
    ? `/formules/${encodeURIComponent(preset.code)}?${billingV2SelectionToSearchParams(selection).toString()}&source=diagnostic`
    : null;

  return <section className="diagnostic-offer" aria-live="polite">
    <p className="eyebrow">Orientation commerciale</p>
    <h2>{recommendation.title}</h2>
    <h3>{recommendation.offerName ?? preset?.name ?? "Offre recommandée"}</h3>
    <p>{recommendation.reason}</p>
    {selection && recommendation.selectedStorageGb ? <p className="diagnostic-offer-description">Capacité retenue dans le catalogue : {recommendation.selectedStorageGb} Go.</p> : null}
    {quote ? <div className="diagnostic-offer-price">
      <span>Tarif calculé</span>
      <strong>{formatCurrencyFromCents(quote.monthlyAfterDiscountCents)} / mois</strong>
      {quote.oneTimeCents > 0 ? <small>{formatCurrencyFromCents(quote.oneTimeCents)} à la mise en service.</small> : null}
    </div> : quoteError ? <p className="diagnostic-offer-error">Le tarif n&apos;a pas pu être calculé pour le moment. Réessayez dans un instant.</p> : <p className="diagnostic-offer-loading">Calcul du tarif à partir du catalogue actuel…</p>}
    {href && quoteAvailable ? <Link className="button diagnostic-offer-cta" href={href}>Voir cette offre</Link> : null}
    {quote && !quoteAvailable ? <p className="diagnostic-offer-error">Cette offre n&apos;est plus disponible à la souscription en ligne. Recommencez le diagnostic ou contactez-nous pour faire le point.</p> : null}
  </section>;
}

function CallbackFormPanel({
  callbackState,
  errors,
  form,
  headingRef,
  messageRef,
  onSubmit,
  organisationVisible,
  setCallbackState,
  updateForm,
}: {
  callbackState: CallbackState;
  errors: DiagnosticCallbackFieldErrors;
  form: CallbackForm;
  headingRef: React.RefObject<HTMLHeadingElement | null>;
  messageRef: React.RefObject<HTMLDivElement | null>;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  organisationVisible: boolean;
  setCallbackState: (state: CallbackState) => void;
  updateForm: <Key extends keyof CallbackForm>(key: Key, value: CallbackForm[Key]) => void;
}) {
  return (
    <section className="diagnostic-contact-panel" id="etre-rappele">
      <div className="diagnostic-contact-copy">
        <p className="eyebrow">Faire le point</p>
        <h2 ref={headingRef} tabIndex={-1}>Être rappelé</h2>
        <p>Vous souhaitez examiner ces priorités ? Laissez vos coordonnées : elles sont utilisées uniquement pour répondre à cette demande. Consultez notre <Link href="/politique-confidentialite">politique de confidentialité</Link>.</p>
      </div>
      <form className="form-card contact-form" noValidate onSubmit={onSubmit}>
        {callbackState === "success" ? <FormMessage ref={messageRef} title="Demande envoyée" tone="success"><p>Votre demande a bien été envoyée. Je vous recontacterai dès que possible.</p></FormMessage> : null}
        {callbackState === "error" ? <FormMessage ref={messageRef} title="Envoi impossible" tone="error"><p>Votre demande n&apos;a pas pu être transmise. Vérifiez les champs puis réessayez.</p></FormMessage> : null}
        <CallbackField error={errors.name} label="Nom / prénom" required><input autoComplete="name" maxLength={120} onChange={(event) => updateForm("name", event.target.value)} value={form.name} /></CallbackField>
        <CallbackField error={errors.phone} label="Téléphone" required><input autoComplete="tel" inputMode="tel" maxLength={40} onChange={(event) => updateForm("phone", event.target.value)} type="tel" value={form.phone} /></CallbackField>
        <CallbackField error={errors.email} label="E-mail (facultatif)"><input autoComplete="email" maxLength={254} onChange={(event) => updateForm("email", event.target.value)} type="email" value={form.email} /></CallbackField>
        {organisationVisible ? <CallbackField error={errors.organisation} label="Entreprise / organisation"><input maxLength={160} onChange={(event) => updateForm("organisation", event.target.value)} value={form.organisation} /></CallbackField> : null}
        <CallbackField error={errors.preferredTime} label="Moment préféré pour être rappelé (facultatif)"><input maxLength={160} onChange={(event) => updateForm("preferredTime", event.target.value)} value={form.preferredTime} /></CallbackField>
        <CallbackField error={errors.comment} label="Commentaire (facultatif)"><textarea maxLength={1200} onChange={(event) => updateForm("comment", event.target.value)} rows={4} value={form.comment} /></CallbackField>
        <label className="diagnostic-consent"><input checked={form.consent} onChange={(event) => updateForm("consent", event.target.checked)} required type="checkbox" /><span>J&apos;accepte que Zachary IT utilise ces informations afin de me recontacter au sujet de cette demande.</span></label>
        {errors.consent ? <span className="field-error">{errors.consent}</span> : null}
        <label aria-hidden="true" className="diagnostic-honeypot">Ne pas remplir ce champ<input autoComplete="off" onChange={(event) => updateForm("website", event.target.value)} tabIndex={-1} value={form.website} /></label>
        <div className="diagnostic-callback-actions"><button className="button" disabled={callbackState === "submitting"} type="submit">{callbackState === "submitting" ? "Envoi en cours…" : "Envoyer ma demande"}</button>{callbackState === "open" || callbackState === "error" ? <button className="text-link" onClick={() => setCallbackState("idle")} type="button">Annuler</button> : null}</div>
      </form>
    </section>
  );
}

function CallbackField({ children, error, label, required = false }: { children: ReactNode; error?: string; label: string; required?: boolean }) {
  return <label>{label}{required ? " *" : ""}{children}{error ? <span className="field-error">{error}</span> : null}</label>;
}
