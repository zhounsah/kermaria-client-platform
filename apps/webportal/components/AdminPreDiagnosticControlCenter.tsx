"use client";

import type {
  BillingV2PublicCatalog,
  DiagnosticConfigurationAdminView,
  DiagnosticConfigurationMutationResponse,
  DiagnosticConfigurationRevisionItem,
  DiagnosticConfigurationRevisionsResponse,
  PreDiagnosticConfiguration,
  PreDiagnosticProfileId,
  PreDiagnosticQuestionConfig,
} from "@kermaria/shared";
import { useEffect, useId, useMemo, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { requestBffJson } from "@/lib/client-api";
import { validateDiagnosticConfiguration } from "@/lib/diagnostic-configuration-validation";
import { DIAGNOSTIC_CONTEXT_IDS, type DiagnosticContextId } from "@/lib/diagnostic-context";
import { commercialQuestionsForProfile, recommendPreDiagnosticOffer } from "@/lib/pre-diagnostic-commerce";
import {
  configuredQuestionsForProfile,
  DEFAULT_PRE_DIAGNOSTIC_CONFIGURATION_V2,
  evaluatePreDiagnosticWithConfiguration,
  type PreDiagnosticProfile,
  type PreDiagnosticQuestion,
  type PreDiagnosticResult,
} from "@/lib/pre-diagnostic";

type Tab = "questionnaire" | "scoring" | "commerce" | "simulator" | "history";
type Banner = { tone: "success" | "error" | "info"; text: string };

const profileIds = ["individual", "professional", "association"] as const;
const PROFILE_LABELS: Record<PreDiagnosticProfileId, string> = {
  individual: "Particulier",
  professional: "Entreprise / activité professionnelle",
  association: "Association",
};
const TABS: { id: Tab; label: string }[] = [
  { id: "questionnaire", label: "Questionnaire" },
  { id: "scoring", label: "Scoring" },
  { id: "commerce", label: "Orientation commerciale" },
  { id: "simulator", label: "Simulateur" },
  { id: "history", label: "Historique" },
];
const copyDefault = () => structuredClone(DEFAULT_PRE_DIAGNOSTIC_CONFIGURATION_V2);
const v2 = (value: DiagnosticConfigurationAdminView["draft"]["configuration"]): PreDiagnosticConfiguration | null => value?.schemaVersion === 2 ? value : null;

export function AdminPreDiagnosticControlCenter({ catalog, initialView }: { catalog: BillingV2PublicCatalog; initialView: DiagnosticConfigurationAdminView }) {
  const initialDraft = () => v2(initialView.draft.configuration) ?? v2(initialView.published.configuration) ?? copyDefault();
  const [view, setView] = useState(initialView);
  const [draft, setDraft] = useState<PreDiagnosticConfiguration>(initialDraft);
  const [savedDraft, setSavedDraft] = useState<PreDiagnosticConfiguration>(initialDraft);
  const [tab, setTab] = useState<Tab>("questionnaire");
  const [pending, setPending] = useState(false);
  const [banner, setBanner] = useState<Banner | null>(null);
  const [serverErrors, setServerErrors] = useState<string[]>([]);
  const tabId = useId();
  const validation = useMemo(() => validateDiagnosticConfiguration(draft), [draft]);
  const dirty = JSON.stringify(draft) !== JSON.stringify(savedDraft);
  const publishBlocker = dirty
    ? "Enregistrez les modifications locales avant de publier."
    : validation.errors.length > 0
      ? "Corrigez les erreurs de validation avant de publier."
      : view.draft.version === 0 ? "Enregistrez d'abord un brouillon versionné." : null;

  useEffect(() => {
    if (!dirty) return undefined;
    const beforeUnload = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ""; };
    window.addEventListener("beforeunload", beforeUnload);
    return () => window.removeEventListener("beforeunload", beforeUnload);
  }, [dirty]);

  async function send(path: `/api/${string}`, init: RequestInit, expectedCode: string, successText: string) {
    setPending(true);
    const response = await requestBffJson<DiagnosticConfigurationMutationResponse>(path, init);
    setPending(false);
    if (!response.ok) { setBanner({ tone: "error", text: response.error.message }); return; }
    setServerErrors(response.data.errors ?? []);
    if (response.data.view) setView(response.data.view);
    if (response.data.code !== expectedCode) { setBanner({ tone: "error", text: response.data.message }); return; }
    const next = response.data.view && v2(response.data.view.draft.configuration);
    if (next) {
      setDraft(next);
      setSavedDraft(next);
    }
    setBanner({ tone: "success", text: successText });
  }
  const save = () => void send("/api/admin/diagnostic/draft", { method: "PUT", headers: { "content-type": "application/json" }, body: JSON.stringify({ configuration: draft, expectedVersion: view.draft.version }) }, "DIAGNOSTIC_DRAFT_SAVED", "Brouillon enregistré.");
  const validateOnServer = () => void send("/api/admin/diagnostic/validate", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ configuration: draft }) }, "DIAGNOSTIC_VALID", "Configuration valide côté API-INTERNAL.");
  const publish = () => {
    if (publishBlocker) { setBanner({ tone: "error", text: publishBlocker }); return; }
    if (!window.confirm("Publier cette configuration ? Le pré-diagnostic public basculera immédiatement sur cette version.")) return;
    void send("/api/admin/diagnostic/publish", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ expectedDraftVersion: view.draft.version, expectedPublishedVersion: view.published.version }) }, "DIAGNOSTIC_PUBLISHED", "Configuration publiée.");
  };
  const resetToPublished = () => {
    setDraft(v2(view.published.configuration) ?? copyDefault());
    setServerErrors([]);
    setBanner({ tone: "info", text: "Les modifications locales ont été remplacées par les valeurs actuellement publiées." });
  };

  return <section className="admin-pre-diagnostic-center" aria-label="Centre de pilotage du diagnostic">
    <header className="admin-pre-diagnostic-intro"><div><span className="card-kicker">Configuration versionnée v2</span><h2>Diagnostic administrable</h2><p>Le brouillon est simulé ici ; seul le document publié est lu par <code>/diagnostic</code> et recalculé par API-INTERNAL.</p></div><span className="admin-settings-persistence-note">{view.persistent ? "Persisté dans MariaDB" : "Mode temporaire"}</span></header>
    <section className="admin-pre-diagnostic-workflow" aria-label="État et publication du brouillon">
      <dl className="admin-diagnostic-status"><div><dt>Brouillon</dt><dd>{view.draft.version ? `v${view.draft.version}` : "Valeurs initiales v2"}</dd></div><div><dt>Publié</dt><dd>{view.published.version ? `v${view.published.version}` : "Comportement validé v1"}</dd></div><div><dt>État</dt><dd>{dirty ? "Modifications non enregistrées" : view.draftDiffers ? "Brouillon prêt à publier" : "Identique au publié"}</dd></div></dl>
      <div className="admin-pre-diagnostic-actions"><div className="admin-pre-diagnostic-action-group"><button className="button" disabled={pending || validation.errors.length > 0} onClick={save} type="button">Enregistrer le brouillon</button><button className="button button-secondary" disabled={pending} onClick={validateOnServer} type="button">Valider côté API</button><button className="button button-secondary admin-pre-diagnostic-publish" disabled={pending || Boolean(publishBlocker)} onClick={publish} type="button">Publier</button></div><button className="button button-link" disabled={pending || !dirty} onClick={resetToPublished} type="button">Repartir des valeurs actuelles</button></div>
      {publishBlocker ? <p className="admin-pre-diagnostic-publish-note" role="status">Publication indisponible : {publishBlocker}</p> : null}
    </section>
    {banner ? <FormMessage tone={banner.tone} title={banner.tone === "success" ? "Mise à jour effectuée" : banner.tone === "info" ? "Brouillon réinitialisé" : "Opération refusée"}>{banner.text}</FormMessage> : null}
    {validation.errors.length || serverErrors.length ? <div className="admin-diagnostic-errors" role="alert"><p>La publication est bloquée tant que ces erreurs subsistent :</p><ul>{[...validation.errors, ...serverErrors].slice(0, 30).map((error) => <li key={error}>{error}</li>)}</ul></div> : null}
    <div className="admin-pre-diagnostic-tabs" role="tablist" aria-label="Sections du diagnostic">{TABS.map((item) => <button aria-controls={`${tabId}-${item.id}-panel`} aria-selected={tab === item.id} className={tab === item.id ? "admin-pre-diagnostic-tab is-active" : "admin-pre-diagnostic-tab"} id={`${tabId}-${item.id}-tab`} key={item.id} onClick={() => setTab(item.id)} role="tab" tabIndex={tab === item.id ? 0 : -1} type="button">{item.label}</button>)}</div>
    <div aria-labelledby={`${tabId}-${tab}-tab`} className="admin-pre-diagnostic-panel" id={`${tabId}-${tab}-panel`} role="tabpanel">
      {tab === "questionnaire" && <Questionnaire configuration={draft} onChange={setDraft} />}
      {tab === "scoring" && <Scoring configuration={draft} onChange={setDraft} />}
      {tab === "commerce" && <Commerce catalog={catalog} configuration={draft} onChange={setDraft} />}
      {tab === "simulator" && <Simulator configuration={draft} catalog={catalog} />}
      {tab === "history" && <History />}
    </div>
  </section>;
}

function Questionnaire({ configuration, onChange }: { configuration: PreDiagnosticConfiguration; onChange: (next: PreDiagnosticConfiguration) => void }) {
  const changeQuestion = (index: number, value: PreDiagnosticQuestionConfig) => onChange({ ...configuration, questions: configuration.questions.map((item, current) => current === index ? value : item) });
  const changeProfile = (index: number, patch: Partial<PreDiagnosticConfiguration["profiles"][number]>) => onChange({ ...configuration, profiles: configuration.profiles.map((item, current) => current === index ? { ...item, ...patch } : item) });
  return <div className="admin-pre-diagnostic-question-list">
    <section className="admin-pre-diagnostic-section"><header><div><span className="card-kicker">Entrée du parcours</span><h3>Profils</h3><p>Les identifiants techniques restent stables ; les libellés, descriptions, ordre et disponibilité sont administrables.</p></div></header><div className="admin-pre-diagnostic-profile-list">{configuration.profiles.slice().sort((left, right) => left.order - right.order).map((profile) => { const index = configuration.profiles.indexOf(profile); return <article className="admin-pre-diagnostic-profile" key={profile.id}><header><div><h4>{profile.label}</h4><p>{profile.description}</p></div><code>{profile.id}</code></header><div className="admin-pre-diagnostic-profile-fields"><label>Libellé<input value={profile.label} onChange={(event) => changeProfile(index, { label: event.target.value })} /></label><label>Description<input value={profile.description} onChange={(event) => changeProfile(index, { description: event.target.value })} /></label><label>Ordre<input min="1" type="number" value={profile.order} onChange={(event) => changeProfile(index, { order: Number(event.target.value) })} /></label><label className="admin-pre-diagnostic-check"><input checked={profile.active} type="checkbox" onChange={(event) => changeProfile(index, { active: event.target.checked })} />Actif</label></div></article> })}</div></section>
    <section className="admin-pre-diagnostic-section"><header className="admin-pre-diagnostic-section-heading"><div><span className="card-kicker">Questions réellement posées</span><h3>Questionnaire santé</h3><p>Ouvrez une question pour modifier ses textes, son ciblage, sa visibilité et les effets de score de ses réponses.</p></div><button className="button button-secondary" onClick={() => onChange({ ...configuration, questions: [...configuration.questions, newQuestion(configuration.questions.length)] })} type="button">Ajouter une question</button></header><div className="admin-pre-diagnostic-question-cards">{configuration.questions.slice().sort((left, right) => left.order - right.order).map((question) => { const index = configuration.questions.indexOf(question); const category = question.categoryId ? configuration.categories.find((item) => item.id === question.categoryId)?.label ?? question.categoryId : "Profil"; return <details className="admin-pre-diagnostic-question" key={question.id}><summary><span className="admin-pre-diagnostic-question-summary"><span className="card-kicker">{category}</span><strong>{question.label}</strong><small>{question.profiles.length} profils · {question.options.length} réponses · {question.required ? "Obligatoire" : "Facultative"}</small></span><span className={question.active ? "admin-pre-diagnostic-state is-active" : "admin-pre-diagnostic-state"}>{question.active ? "Active" : "Inactive"}</span></summary><div className="admin-pre-diagnostic-question-editor"><div className="admin-pre-diagnostic-fields"><label>Libellé<input value={question.label} onChange={(event) => changeQuestion(index, { ...question, label: event.target.value })} /></label><label>Texte d&apos;aide<input value={question.hint ?? ""} onChange={(event) => changeQuestion(index, { ...question, hint: event.target.value || null })} /></label><label>Ordre<input type="number" value={question.order} onChange={(event) => changeQuestion(index, { ...question, order: Number(event.target.value) })} /></label></div><fieldset className="admin-pre-diagnostic-choice-group"><legend>Profils concernés</legend>{profileIds.map((profile) => <label key={profile}><input checked={question.profiles.includes(profile)} type="checkbox" onChange={(event) => changeQuestion(index, { ...question, profiles: event.target.checked ? [...question.profiles, profile] : question.profiles.filter((item) => item !== profile) })} />{PROFILE_LABELS[profile]}</label>)}</fieldset><div className="admin-pre-diagnostic-switches"><label><input checked={question.required} type="checkbox" onChange={(event) => changeQuestion(index, { ...question, required: event.target.checked })} />Obligatoire</label><label><input checked={question.active} type="checkbox" onChange={(event) => changeQuestion(index, { ...question, active: event.target.checked })} />Active</label></div><p className="admin-pre-diagnostic-condition">Visibilité : {question.when.length === 0 ? "toujours visible pour les profils sélectionnés" : `${question.when.length} condition${question.when.length > 1 ? "s" : ""} déclarative${question.when.length > 1 ? "s" : ""}`}</p><Options question={question} onChange={(next) => changeQuestion(index, next)} /></div></details> })}</div></section>
  </div>;
}

const newQuestion = (count: number): PreDiagnosticQuestionConfig => ({ id: `question-${count + 1}`, categoryId: "equipment", label: "Nouvelle question", hint: null, profiles: [...profileIds], required: true, active: true, order: (count + 1) * 10, when: [], options: [{ value: "yes", label: "Oui", active: true, order: 10, effects: [] }, { value: "no", label: "Non", active: true, order: 20, effects: [] }] });

function Options({ question, onChange }: { question: PreDiagnosticQuestionConfig; onChange: (next: PreDiagnosticQuestionConfig) => void }) {
  return <section className="admin-pre-diagnostic-options" aria-label={`Réponses pour ${question.label}`}><header><div><h4>Réponses et impacts de score</h4><p>Les effets sont appliqués par le moteur avec la configuration publiée.</p></div><button className="button button-link" onClick={() => onChange({ ...question, options: [...question.options, { value: `option_${question.options.length + 1}`, label: "Nouvelle réponse", active: true, order: (question.options.length + 1) * 10, effects: [] }] })} type="button">Ajouter une réponse</button></header><div className="admin-pre-diagnostic-option-list">{question.options.map((option, index) => <div className="admin-pre-diagnostic-option" key={option.value}><code>{option.value}</code><label>Libellé<input aria-label={`Libellé ${option.value}`} value={option.label} onChange={(event) => onChange({ ...question, options: question.options.map((item, current) => current === index ? { ...item, label: event.target.value } : item) })} /></label><label className="admin-pre-diagnostic-check"><input checked={option.active} type="checkbox" onChange={(event) => onChange({ ...question, options: question.options.map((item, current) => current === index ? { ...item, active: event.target.checked } : item) })} />Active</label><div className="admin-pre-diagnostic-effect-list">{option.effects.length === 0 ? <span className="muted">Aucun impact de score</span> : option.effects.map((effect, effectIndex) => <label key={`${effect.mode}-${effectIndex}`}>{effect.mode === "absolute" ? "Score absolu" : "Déduction"}<input aria-label={`Valeur ${option.value}`} max="100" min="0" type="number" value={effect.value} onChange={(event) => onChange({ ...question, options: question.options.map((item, current) => current === index ? { ...item, effects: item.effects.map((entry, currentEffect) => currentEffect === effectIndex ? { ...entry, value: Number(event.target.value) } : entry) } : item) })} /></label>)}</div></div>)}</div></section>;
}

function Scoring({ configuration, onChange }: { configuration: PreDiagnosticConfiguration; onChange: (next: PreDiagnosticConfiguration) => void }) {
  const total = (profile: PreDiagnosticProfileId) => configuration.categories.filter((category) => category.active).reduce((sum, category) => sum + category.weights[profile], 0);
  const updateCategory = (index: number, patch: Partial<PreDiagnosticConfiguration["categories"][number]>) => onChange({ ...configuration, categories: configuration.categories.map((item, current) => current === index ? { ...item, ...patch } : item) });
  return <div className="admin-pre-diagnostic-scoring">
    <section className="admin-pre-diagnostic-section"><header><div><span className="card-kicker">Calcul global</span><h3>Catégories et pondérations</h3><p>Chaque profil doit totaliser 100 %. Les déductions et scores absolus sont réglés dans les réponses du questionnaire.</p></div></header><div className="admin-pre-diagnostic-weight-summaries">{profileIds.map((profile) => <article className={total(profile) === 100 ? "admin-pre-diagnostic-weight-summary" : "admin-pre-diagnostic-weight-summary is-invalid"} key={profile}><h4>{PROFILE_LABELS[profile]}</h4><dl>{configuration.categories.filter((category) => category.active).map((category) => <div key={category.id}><dt>{category.label}</dt><dd>{category.weights[profile]} %</dd></div>)}<div className="admin-pre-diagnostic-weight-total"><dt>Total</dt><dd>{total(profile)} % {total(profile) === 100 ? "✓" : "— à corriger"}</dd></div></dl></article>)}</div><div className="admin-pre-diagnostic-category-list">{configuration.categories.map((category, index) => <article className="admin-pre-diagnostic-category" key={category.id}><header><code>{category.id}</code><label className="admin-pre-diagnostic-check"><input checked={category.active} type="checkbox" onChange={(event) => updateCategory(index, { active: event.target.checked })} />Active</label></header><div className="admin-pre-diagnostic-category-fields"><label>Libellé<input value={category.label} onChange={(event) => updateCategory(index, { label: event.target.value })} /></label><label>Ordre<input type="number" value={category.order} onChange={(event) => updateCategory(index, { order: Number(event.target.value) })} /></label>{profileIds.map((profile) => <label key={profile}>{PROFILE_LABELS[profile]}<span className="admin-pre-diagnostic-input-suffix"><input max="100" min="0" type="number" value={category.weights[profile]} onChange={(event) => updateCategory(index, { weights: { ...category.weights, [profile]: Number(event.target.value) } })} /><span>%</span></span></label>)}</div></article>)}</div></section>
    <section className="admin-pre-diagnostic-section"><header><div><span className="card-kicker">Restitution</span><h3>Niveaux globaux</h3><p>Le premier seuil atteint détermine le niveau affiché au visiteur.</p></div></header><div className="admin-pre-diagnostic-level-list">{configuration.levels.map((level, index) => <article key={level.id}><code>{level.id}</code><label>Libellé<input value={level.label} onChange={(event) => onChange({ ...configuration, levels: configuration.levels.map((item, current) => current === index ? { ...item, label: event.target.value } : item) })} /></label><label>Score minimum<input max="100" min="0" type="number" value={level.minimumScore} onChange={(event) => onChange({ ...configuration, levels: configuration.levels.map((item, current) => current === index ? { ...item, minimumScore: Number(event.target.value) } : item) })} /></label></article>)}</div><label className="admin-pre-diagnostic-limit">Nombre maximum de priorités<input max="5" min="1" type="number" value={configuration.maximumPriorities} onChange={(event) => onChange({ ...configuration, maximumPriorities: Number(event.target.value) })} /></label></section>
    <RuleLists configuration={configuration} onChange={onChange} />
  </div>;
}

function RuleLists({ configuration, onChange }: { configuration: PreDiagnosticConfiguration; onChange: (next: PreDiagnosticConfiguration) => void }) {
  const categoryLabel = (id: string) => configuration.categories.find((category) => category.id === id)?.label ?? id;
  return <div className="admin-pre-diagnostic-rule-columns"><section className="admin-pre-diagnostic-section"><header><div><span className="card-kicker">À améliorer</span><h3>Priorités</h3><p>Une priorité apparaît lorsque le score de sa catégorie descend sous son seuil.</p></div></header><div className="admin-pre-diagnostic-rule-list">{configuration.priorities.map((rule, index) => <article className="admin-pre-diagnostic-rule" key={`${rule.categoryId}-${index}`}><header><strong>{categoryLabel(rule.categoryId)}</strong><code>{rule.categoryId}</code></header><label>Seuil d&apos;activation<input max="100" min="0" type="number" value={rule.threshold} onChange={(event) => onChange({ ...configuration, priorities: configuration.priorities.map((item, current) => current === index ? { ...item, threshold: Number(event.target.value) } : item) })} /></label><label>Titre<input value={rule.title} onChange={(event) => onChange({ ...configuration, priorities: configuration.priorities.map((item, current) => current === index ? { ...item, title: event.target.value } : item) })} /></label><label>Texte<textarea value={rule.body} onChange={(event) => onChange({ ...configuration, priorities: configuration.priorities.map((item, current) => current === index ? { ...item, body: event.target.value } : item) })} /></label></article>)}</div></section><section className="admin-pre-diagnostic-section"><header><div><span className="card-kicker">Ce qui va bien</span><h3>Points positifs</h3><p>Un point positif apparaît lorsque le score de sa catégorie atteint son seuil.</p></div></header><div className="admin-pre-diagnostic-rule-list">{configuration.positives.map((rule, index) => <article className="admin-pre-diagnostic-rule" key={`${rule.categoryId}-${index}`}><header><strong>{categoryLabel(rule.categoryId)}</strong><code>{rule.categoryId}</code></header><label>Seuil d&apos;activation<input max="100" min="0" type="number" value={rule.threshold} onChange={(event) => onChange({ ...configuration, positives: configuration.positives.map((item, current) => current === index ? { ...item, threshold: Number(event.target.value) } : item) })} /></label><label>Texte<textarea value={rule.text} onChange={(event) => onChange({ ...configuration, positives: configuration.positives.map((item, current) => current === index ? { ...item, text: event.target.value } : item) })} /></label></article>)}</div></section></div>;
}

function Commerce({ catalog, configuration, onChange }: { catalog: BillingV2PublicCatalog; configuration: PreDiagnosticConfiguration; onChange: (next: PreDiagnosticConfiguration) => void }) {
  const commerce = configuration.commerce;
  const setCommerce = (next: PreDiagnosticConfiguration["commerce"]) => onChange({ ...configuration, commerce: next });
  const setBinding = (profileId: typeof commerce.catalogBindings[number]["profileId"], patch: Partial<typeof commerce.catalogBindings[number]>) => setCommerce({ ...commerce, catalogBindings: commerce.catalogBindings.map((binding) => binding.profileId === profileId ? { ...binding, ...patch } : binding) });
  return <div className="admin-pre-diagnostic-commerce">
    <section className="admin-pre-diagnostic-commerce-flow" aria-label="Fonctionnement de l&apos;orientation commerciale"><span>Diagnostic</span><b>→</b><span>Besoin détecté</span><b>→</b><span>Catalogue Billing V2</span><b>→</b><span>Abonnement proposé</span></section>
    <section className="admin-pre-diagnostic-section"><header><div><span className="card-kicker">Point d&apos;entrée</span><h3>Contextes</h3><p>Un contexte actif peut autoriser le self-service ou orienter d&apos;emblée vers un cadrage humain.</p></div></header><div className="admin-pre-diagnostic-context-list">{configuration.contexts.slice().sort((left, right) => left.order - right.order).map((context) => { const index = configuration.contexts.indexOf(context); return <article className="admin-pre-diagnostic-context" key={context.id}><header><div><h4>{context.label}</h4><code>{context.id}</code></div><span className={context.allowsSelfService ? "admin-pre-diagnostic-state is-active" : "admin-pre-diagnostic-state"}>{context.allowsSelfService ? "Self-service autorisé" : "Cadrage humain"}</span></header><div className="admin-pre-diagnostic-context-fields"><label>Libellé<input value={context.label} onChange={(event) => onChange({ ...configuration, contexts: configuration.contexts.map((item, current) => current === index ? { ...item, label: event.target.value } : item) })} /></label><label>Texte<input value={context.text} onChange={(event) => onChange({ ...configuration, contexts: configuration.contexts.map((item, current) => current === index ? { ...item, text: event.target.value } : item) })} /></label><label>Ordre<input type="number" value={context.order} onChange={(event) => onChange({ ...configuration, contexts: configuration.contexts.map((item, current) => current === index ? { ...item, order: Number(event.target.value) } : item) })} /></label><div className="admin-pre-diagnostic-switches"><label><input checked={context.active} type="checkbox" onChange={(event) => onChange({ ...configuration, contexts: configuration.contexts.map((item, current) => current === index ? { ...item, active: event.target.checked } : item) })} />Actif</label><label><input checked={context.allowsSelfService} type="checkbox" onChange={(event) => onChange({ ...configuration, contexts: configuration.contexts.map((item, current) => current === index ? { ...item, allowsSelfService: event.target.checked } : item) })} />Autorise le self-service</label></div></div></article> })}</div></section>
    <section className="admin-pre-diagnostic-section"><header><div><span className="card-kicker">Qualification</span><h3>Limites self-service</h3><p>Ces limites déterminent si le besoin est représentable par le catalogue ; elles n&apos;emportent aucun prix.</p></div></header><div className="admin-pre-diagnostic-limits">{(["minimumStorageGb", "maximumStorageGb", "minimumUsers", "maximumUsers", "maximumSites"] as const).map((key) => <label key={key}>{labelForCommerceLimit(key)}<input min="1" type="number" value={commerce[key]} onChange={(event) => setCommerce({ ...commerce, [key]: Number(event.target.value) })} /></label>)}</div><p className="admin-pre-diagnostic-condition">Scopes compatibles : {commerce.compatibleScopes.join(", ") || "aucun"}. Intentions en cadrage humain : {commerce.humanReviewIntents.join(", ") || "aucune"}.</p></section>
    <section className="admin-pre-diagnostic-section"><header><div><span className="card-kicker">Pont vers Billing V2</span><h3>Correspondances catalogue</h3><p>Le besoin référence des services ; les capacités, disponibilités et tarifs restent lus dans le catalogue vivant.</p></div></header><div className="admin-pre-diagnostic-binding-list">{commerce.profiles.map((profile, index) => { const binding = commerce.catalogBindings.find((item) => item.profileId === profile.id); if (!binding) return null; const storageService = catalog.services.find((service) => service.code === binding.storageServiceCode); return <article className="admin-pre-diagnostic-binding" key={profile.id}><header><div><h4>{profile.label}</h4><p><code>{profile.id}</code></p></div><label className="admin-pre-diagnostic-check"><input checked={profile.active} type="checkbox" onChange={(event) => setCommerce({ ...commerce, profiles: commerce.profiles.map((item, current) => current === index ? { ...item, active: event.target.checked } : item) })} />Éligible au diagnostic</label></header><label>Service de stockage<select value={binding.storageServiceCode} onChange={(event) => setBinding(profile.id, { storageServiceCode: event.target.value })}>{catalog.services.filter((service) => service.tiers.some((tier) => tier.numericValue !== null)).map((service) => <option key={service.code} value={service.code}>{service.name} ({service.code})</option>)}</select></label><fieldset className="admin-pre-diagnostic-choice-group"><legend>Composants requis</legend>{catalog.services.map((service) => <label key={service.code}><input checked={binding.requiredServiceCodes.includes(service.code)} type="checkbox" onChange={(event) => setBinding(profile.id, { requiredServiceCodes: event.target.checked ? [...binding.requiredServiceCodes, service.code] : binding.requiredServiceCodes.filter((code) => code !== service.code) })} />{service.name}<code>{service.code}</code></label>)}</fieldset>{storageService ? <div className="admin-pre-diagnostic-offer-list" aria-label={`Paliers ${storageService.name}`}>{storageService.tiers.filter((tier) => tier.numericValue !== null).sort((left, right) => (left.numericValue ?? 0) - (right.numericValue ?? 0)).map((tier) => <article key={tier.code}><strong>{storageService.name} — {tier.label}</strong><code>{storageService.code}-{tier.code}</code><span>{tier.numericValue} Go · {tier.publicSelectable ? "Éligible diagnostic" : "Non publié"}</span></article>)}</div> : <p className="admin-pre-diagnostic-alert">Le service de stockage sélectionné n&apos;est pas disponible dans le catalogue actuel.</p>}</article> })}</div></section>
  </div>;
}

function labelForCommerceLimit(key: keyof PreDiagnosticConfiguration["commerce"]) {
  const labels: Partial<Record<keyof PreDiagnosticConfiguration["commerce"], string>> = { minimumStorageGb: "Stockage minimum (Go)", maximumStorageGb: "Stockage maximum (Go)", minimumUsers: "Utilisateurs minimum", maximumUsers: "Utilisateurs maximum", maximumSites: "Sites maximum" };
  return labels[key] ?? key;
}

function Simulator({ configuration, catalog }: { configuration: PreDiagnosticConfiguration; catalog: BillingV2PublicCatalog }) {
  const [profile, setProfile] = useState<PreDiagnosticProfile>("individual");
  const [context, setContext] = useState<DiagnosticContextId>("general");
  const [answers, setAnswers] = useState<Record<string, string>>({ profile: "individual" });
  const healthQuestions = configuredQuestionsForProfile(configuration, profile, answers);
  const commercialQuestions = commercialQuestionsForProfile(profile, context, configuration);
  const incompleteHealth = healthQuestions.some((question) => question.id !== "profile" && question.required && !answers[question.id]);
  const incompleteCommerce = commercialQuestions.some((question) => !answers[question.id]);
  const complete = !incompleteHealth && !incompleteCommerce;
  const result = complete ? evaluatePreDiagnosticWithConfiguration(configuration, { ...answers, profile }) : null;
  const orientation = complete ? recommendPreDiagnosticOffer(answers, profile, context, catalog, configuration) : null;
  const healthGroups = groupQuestionsByCategory(healthQuestions.filter((question) => question.id !== "profile"));
  const resetAnswers = (nextProfile = profile) => setAnswers({ profile: nextProfile });
  const setAnswer = (questionId: string, value: string) => setAnswers((current) => ({ ...current, [questionId]: value }));
  return <div className="admin-pre-diagnostic-simulator"><header className="admin-pre-diagnostic-section-heading"><div><span className="card-kicker">Brouillon courant</span><h3>Simulateur</h3><p>Il utilise le brouillon en cours, jamais la version publiée. Aucun prix n&apos;est calculé ici.</p></div></header><div className="admin-pre-diagnostic-simulator-grid"><section className="admin-pre-diagnostic-simulator-inputs" aria-label="Réponses simulées"><div className="admin-pre-diagnostic-fields"><label>Profil<select value={profile} onChange={(event) => { const next = event.target.value as PreDiagnosticProfile; setProfile(next); resetAnswers(next); }}>{configuration.profiles.filter((item) => item.active).map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}</select></label><label>Contexte<select value={context} onChange={(event) => { setContext(event.target.value as DiagnosticContextId); resetAnswers(); }}>{DIAGNOSTIC_CONTEXT_IDS.filter((id) => configuration.contexts.some((item) => item.id === id && item.active)).map((id) => <option key={id} value={id}>{configuration.contexts.find((item) => item.id === id)?.label ?? id}</option>)}</select></label></div>{healthGroups.map(([category, questions]) => <fieldset className="admin-pre-diagnostic-simulator-question-set" key={category}><legend>{category}</legend>{questions.map((question) => <label key={question.id}>{question.label}<select value={answers[question.id] ?? ""} onChange={(event) => setAnswer(question.id, event.target.value)}><option value="">Choisir…</option>{question.options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select>{question.hint ? <small>{question.hint}</small> : null}</label>)}</fieldset>)}{commercialQuestions.length > 0 ? <fieldset className="admin-pre-diagnostic-simulator-question-set"><legend>Qualification commerciale</legend>{commercialQuestions.map((question) => <label key={question.id}>{question.label}<select value={answers[question.id] ?? ""} onChange={(event) => setAnswer(question.id, event.target.value)}><option value="">Choisir…</option>{question.options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select>{question.hint ? <small>{question.hint}</small> : null}</label>)}</fieldset> : null}</section><section className="admin-pre-diagnostic-simulator-result" aria-live="polite" aria-label="Résultat de simulation">{!complete ? <div className="admin-pre-diagnostic-empty-result"><span className="card-kicker">Simulation en attente</span><h3>Complétez le questionnaire pour obtenir une simulation.</h3><p>Le score, les priorités et l&apos;orientation commerciale apparaîtront uniquement après les réponses nécessaires.</p></div> : result && orientation ? <SimulationResult orientation={orientation} result={result} /> : null}</section></div></div>;
}

function groupQuestionsByCategory(questions: readonly PreDiagnosticQuestion[]) {
  const groups = new Map<string, PreDiagnosticQuestion[]>();
  for (const question of questions) groups.set(question.category, [...(groups.get(question.category) ?? []), question]);
  return [...groups.entries()];
}

function SimulationResult({ orientation, result }: { orientation: ReturnType<typeof recommendPreDiagnosticOffer>; result: PreDiagnosticResult }) {
  return <><div className="admin-pre-diagnostic-result-score"><span>Score</span><strong>{result.score} <small>/ 100</small></strong><b>{result.level}</b></div><section><h4>Catégories</h4><dl className="admin-pre-diagnostic-score-list">{result.categories.map((category) => <div key={category.id}><dt>{category.label}</dt><dd>{category.score}/100</dd></div>)}</dl></section>{result.priorities.length > 0 ? <section><h4>Priorités</h4><ol>{result.priorities.map((item) => <li key={item.title}>{item.title}</li>)}</ol></section> : null}{result.positives.length > 0 ? <section><h4>Points positifs</h4><ul>{result.positives.map((item) => <li key={item}>{item}</li>)}</ul></section> : null}<section className={orientation.kind === "standard" ? "admin-pre-diagnostic-offer-result" : "admin-pre-diagnostic-human-result"}><span className="card-kicker">{orientation.kind === "standard" ? "Offre proposée" : "Cadrage humain requis"}</span><h4>{orientation.kind === "standard" ? orientation.offerName ?? orientation.title : orientation.title}</h4><p>{orientation.reason}</p>{orientation.need ? <dl><div><dt>Besoin détecté</dt><dd>{orientation.need.family}</dd></div><div><dt>Capacité requise</dt><dd>{orientation.need.requiredStorageGb} Go</dd></div></dl> : null}{orientation.kind === "standard" && orientation.selectedStorageGb ? <p><code>Palier catalogue : {orientation.selectedStorageGb} Go</code></p> : null}</section></>;
}

function History() {
  const [revisions, setRevisions] = useState<DiagnosticConfigurationRevisionItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => { void requestBffJson<DiagnosticConfigurationRevisionsResponse>("/api/admin/diagnostic/revisions", { method: "GET" }).then((result) => result.ok ? setRevisions(result.data.revisions) : setError(result.error.message)); }, []);
  if (error) return <p role="status">{error}</p>;
  if (!revisions) return <p>Chargement…</p>;
  return <ul className="admin-diagnostic-history">{revisions.map((item) => <li key={`${item.state}-${item.version}-${item.correlationId}`}><strong>{item.state === "published" ? "Publié" : "Brouillon"}</strong><span>v{item.version} · {new Date(item.createdAt).toLocaleString("fr-FR")}</span></li>)}</ul>;
}
