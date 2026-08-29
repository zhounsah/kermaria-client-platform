"use client";

import type {
  BillingV2PublicCatalog,
  DiagnosticConfiguration,
  DiagnosticConfigurationAdminView,
  DiagnosticConfigurationMutationResponse,
  DiagnosticConfigurationRevisionItem,
  DiagnosticConfigurationRevisionsResponse,
  DiagnosticContextConfig,
  DiagnosticRecommendationConfig,
} from "@kermaria/shared";
import { Activity, ArrowRight, History, PencilLine, PlayCircle } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import { AdminDiagnosticContextEditor } from "@/components/AdminDiagnosticEditor";
import { AdminDiagnosticSimulator } from "@/components/AdminDiagnosticSimulator";
import { FormMessage } from "@/components/FormMessage";
import { requestBffJson } from "@/lib/client-api";
import { validateDiagnosticConfiguration } from "@/lib/diagnostic-configuration-validation";
import {
  DEFAULT_DIAGNOSTIC_CONFIGURATION,
  type DiagnosticContextId,
} from "@/lib/diagnostic-context";

type Banner = { tone: "success" | "error"; text: string };
type TabId = "editor" | "simulator" | "history";

const TABS: { id: TabId; label: string }[] = [
  { id: "editor", label: "Édition" },
  { id: "simulator", label: "Simulateur" },
  { id: "history", label: "Historique" },
];

/** Message lisible pour une erreur remontée par le BFF ou l'API. */
function describeFailure(code: string, message: string) {
  switch (code) {
    case "DIAGNOSTIC_VERSION_CONFLICT":
      return "La configuration a été modifiée ailleurs. Rechargez la page avant de recommencer.";
    case "CSRF_FORBIDDEN":
      return "La modification doit être confirmée par un jeton CSRF valide. Rechargez la page.";
    case "ACCESS_DENIED":
      return "Votre compte ne dispose pas de la permission « settings.diagnostic.write ».";
    default:
      return message;
  }
}

export function AdminDiagnosticCenter({
  catalog,
  initialView,
  recommendationConfig,
}: {
  catalog: BillingV2PublicCatalog;
  initialView: DiagnosticConfigurationAdminView;
  recommendationConfig: DiagnosticRecommendationConfig;
}) {
  const [view, setView] = useState(initialView);
  const [tab, setTab] = useState<TabId>("editor");
  const [banner, setBanner] = useState<Banner | null>(null);
  const [serverErrors, setServerErrors] = useState<string[]>([]);
  const [pending, setPending] = useState(false);
  const [contextId, setContextId] = useState<DiagnosticContextId>("backup");

  // Le brouillon absent en base retombe sur la configuration integree au code :
  // l'administrateur part du parcours reellement en production plutot que d'un
  // formulaire vide.
  const [draft, setDraft] = useState<DiagnosticConfiguration>(
    () => initialView.draft.configuration
      ?? initialView.published.configuration
      ?? DEFAULT_DIAGNOSTIC_CONFIGURATION,
  );

  const localValidation = useMemo(
    () => validateDiagnosticConfiguration(draft),
    [draft],
  );
  const dirty = useMemo(
    () => JSON.stringify(draft) !== JSON.stringify(view.draft.configuration ?? null),
    [draft, view.draft.configuration],
  );

  useEffect(() => {
    if (!dirty) return;
    const warn = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);

  const context = draft.contexts.find((item) => item.id === contextId)
    ?? draft.contexts[0]
    ?? null;

  function applyResponse(
    result: DiagnosticConfigurationMutationResponse,
    successCode: string,
    successText: string,
  ) {
    setServerErrors(result.errors ?? []);
    if (result.view) setView(result.view);
    if (result.code === successCode) {
      if (result.view?.draft.configuration) setDraft(result.view.draft.configuration);
      setBanner({ tone: "success", text: successText });
      return;
    }
    setBanner({ tone: "error", text: describeFailure(result.code, result.message) });
  }

  async function save() {
    setPending(true);
    const result = await requestBffJson<DiagnosticConfigurationMutationResponse>(
      "/api/admin/diagnostic/draft",
      {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          configuration: draft,
          expectedVersion: view.draft.version,
        }),
      },
    );
    setPending(false);
    if (!result.ok) {
      setBanner({
        tone: "error",
        text: describeFailure(result.error.code, result.error.message),
      });
      return;
    }
    applyResponse(result.data, "DIAGNOSTIC_DRAFT_SAVED", "Brouillon enregistré.");
  }

  async function validateOnServer() {
    setPending(true);
    const result = await requestBffJson<DiagnosticConfigurationMutationResponse>(
      "/api/admin/diagnostic/validate",
      {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ configuration: draft }),
      },
    );
    setPending(false);
    if (!result.ok) {
      setBanner({
        tone: "error",
        text: describeFailure(result.error.code, result.error.message),
      });
      return;
    }
    setServerErrors(result.data.errors ?? []);
    setBanner(
      result.data.code === "DIAGNOSTIC_VALID"
        ? { tone: "success", text: "Configuration valide." }
        : { tone: "error", text: result.data.message },
    );
  }

  async function publish() {
    if (dirty) {
      setBanner({
        tone: "error",
        text: "Enregistrez le brouillon avant de le publier.",
      });
      return;
    }
    if (!window.confirm(
      "Publier ce brouillon ? Le parcours public bascule immédiatement sur cette version.",
    )) {
      return;
    }

    setPending(true);
    const result = await requestBffJson<DiagnosticConfigurationMutationResponse>(
      "/api/admin/diagnostic/publish",
      {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          expectedDraftVersion: view.draft.version,
          expectedPublishedVersion: view.published.version,
        }),
      },
    );
    setPending(false);
    if (!result.ok) {
      setBanner({
        tone: "error",
        text: describeFailure(result.error.code, result.error.message),
      });
      return;
    }
    applyResponse(result.data, "DIAGNOSTIC_PUBLISHED", "Configuration publiée.");
  }

  return (
    <section
      aria-label="Diagnostic administrable"
      className="content-panel section-card admin-settings-surface admin-settings-focused-page"
    >
      <header className="admin-settings-focused-header">
        <div>
          <h2>Diagnostic</h2>
          <p>
            Modifiez un contexte à la fois. Le brouillon, la validation et la
            publication restent séparés pour que l&apos;impact de chaque action soit
            explicite.
          </p>
        </div>
        <span className="admin-settings-persistence-note">
          {view.persistent ? "Persisté dans MariaDB" : "Mode temporaire"}
        </span>
      </header>

      <dl className="admin-settings-summary-strip admin-diagnostic-status">
        <div>
          <dt>Brouillon</dt>
          <dd>
            {view.draft.source === "code"
              ? "Aucun brouillon enregistré"
              : `v${view.draft.version} · ${formatDate(view.draft.updatedAt)}`}
          </dd>
        </div>
        <div>
          <dt>Publié</dt>
          <dd>
            {view.published.source === "code"
              ? "Configuration intégrée au code"
              : `v${view.published.version} · ${formatDate(view.published.updatedAt)}`}
          </dd>
        </div>
        <div>
          <dt>État</dt>
          <dd>
            {dirty
              ? "Modifications non enregistrées"
              : view.draftDiffers
                ? "Brouillon différent de la version publiée"
                : "Brouillon identique à la version publiée"}
          </dd>
        </div>
      </dl>

      {banner ? (
        <FormMessage
          tone={banner.tone}
          title={
            banner.tone === "success"
              ? "Opération effectuée"
              : "Opération refusée"
          }
        >
          {banner.text}
        </FormMessage>
      ) : null}

      {localValidation.errors.length > 0 ? (
        <div className="admin-diagnostic-errors" role="status">
          <p>La configuration n&apos;est pas encore valide :</p>
          <ul>
            {localValidation.errors.slice(0, 20).map((error) => (
              <li key={error}>{error}</li>
            ))}
          </ul>
        </div>
      ) : null}

      {serverErrors.length > 0 ? (
        <div className="admin-diagnostic-errors" role="status">
          <p>Refus d&apos;API-INTERNAL :</p>
          <ul>
            {serverErrors.slice(0, 20).map((error) => (
              <li key={error}>{error}</li>
            ))}
          </ul>
        </div>
      ) : null}

      <div className="admin-settings-command-bar">
        <div>
          <button
            className="button"
            disabled={pending || localValidation.errors.length > 0}
            onClick={save}
            type="button"
          >
            Enregistrer le brouillon
          </button>
          <button
            className="button button-secondary"
            disabled={pending}
            onClick={validateOnServer}
            type="button"
          >
            Valider côté API
          </button>
          <button
            className="button button-secondary"
            disabled={pending || dirty || view.draft.source === "code"}
            onClick={publish}
            type="button"
          >
            Publier
          </button>
        </div>
        <button
          className="button button-link"
          disabled={pending}
          onClick={() => {
            if (
              !window.confirm(
                "Remplacer le brouillon en cours d'édition par la configuration intégrée au code ?",
              )
            ) {
              return;
            }
            setDraft(DEFAULT_DIAGNOSTIC_CONFIGURATION);
            setServerErrors([]);
            setBanner({
              tone: "success",
              text: "Brouillon réinitialisé sur la configuration du code. Enregistrez pour le conserver.",
            });
          }}
          type="button"
        >
          Repartir de la configuration du code
        </button>
      </div>

      <div
        aria-label="Mode de travail"
        className="admin-settings-segmented"
        role="tablist"
      >
        {TABS.map((entry) => {
          const Icon =
            entry.id === "editor"
              ? PencilLine
              : entry.id === "simulator"
                ? PlayCircle
                : History;
          return (
            <button
              aria-selected={tab === entry.id}
              className={tab === entry.id ? "is-active" : undefined}
              key={entry.id}
              onClick={() => setTab(entry.id)}
              role="tab"
              type="button"
            >
              <Icon aria-hidden="true" size={17} />
              {entry.label}
            </button>
          );
        })}
      </div>

      {tab === "editor" ? (
        <div className="admin-settings-workspace admin-settings-diagnostic-workspace">
          <aside
            aria-label="Contextes du diagnostic"
            className="admin-settings-selector"
          >
            <div className="admin-settings-selector-heading">
              <strong>
                {draft.contexts.length} contexte{draft.contexts.length > 1 ? "s" : ""}
              </strong>
              <span>Sélectionnez le parcours à modifier.</span>
            </div>
            <div className="admin-settings-selector-list">
              {draft.contexts.map((item) => (
                <button
                  aria-current={context?.id === item.id ? "true" : undefined}
                  className="admin-settings-selector-item"
                  key={item.id}
                  onClick={() => setContextId(item.id as DiagnosticContextId)}
                  type="button"
                >
                  <span>{item.label || item.id}</span>
                  <small>
                    {item.questions.length} question
                    {item.questions.length > 1 ? "s" : ""}
                  </small>
                  <ArrowRight aria-hidden="true" size={16} />
                </button>
              ))}
            </div>
          </aside>

          <div className="admin-settings-detail-panel admin-settings-diagnostic-detail">
            {context ? (
              <section className="admin-settings-single-editor">
                <header>
                  <div className="admin-settings-editor-icon">
                    <Activity aria-hidden="true" size={20} />
                  </div>
                  <div>
                    <p className="eyebrow">Contexte sélectionné</p>
                    <h3>{context.label}</h3>
                    <p>{context.title}</p>
                  </div>
                </header>
                <AdminDiagnosticContextEditor
                  context={context}
                  onChange={(next) =>
                    setDraft((current) => replaceContext(current, next))
                  }
                />
              </section>
            ) : (
              <p className="admin-settings-inline-state muted">
                Contexte introuvable dans le brouillon.
              </p>
            )}
          </div>
        </div>
      ) : null}

      {tab === "simulator" ? (
        <div className="admin-settings-detail-panel admin-settings-standalone-detail">
          <AdminDiagnosticSimulator
            catalog={catalog}
            configuration={
              localValidation.configuration ?? DEFAULT_DIAGNOSTIC_CONFIGURATION
            }
            recommendationConfig={recommendationConfig}
          />
        </div>
      ) : null}

      {tab === "history" ? (
        <div className="admin-settings-detail-panel admin-settings-standalone-detail">
          <DiagnosticHistory />
        </div>
      ) : null}
    </section>
  );
}

function replaceContext(
  configuration: DiagnosticConfiguration,
  next: DiagnosticContextConfig,
): DiagnosticConfiguration {
  return {
    ...configuration,
    contexts: configuration.contexts.map((item) => item.id === next.id ? next : item),
  };
}

function DiagnosticHistory() {
  const [revisions, setRevisions] = useState<DiagnosticConfigurationRevisionItem[] | null>(
    null,
  );
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      const result = await requestBffJson<DiagnosticConfigurationRevisionsResponse>(
        "/api/admin/diagnostic/revisions",
        { method: "GET" },
      );
      if (cancelled) return;
      if (!result.ok) {
        setError(result.error.message);
        return;
      }
      setRevisions(result.data.revisions);
    })();
    return () => { cancelled = true; };
  }, []);

  if (error) return <p className="admin-settings-inline-state admin-settings-inline-state-error" role="status">{error}</p>;
  if (revisions === null) return <p aria-busy="true" className="admin-settings-inline-state muted">Chargement de l&apos;historique…</p>;
  if (revisions.length === 0) {
    return <p className="admin-settings-inline-state muted">Aucune modification enregistrée.</p>;
  }

  return (
    <ul className="admin-diagnostic-history">
      {revisions.map((revision) => (
        <li key={`${revision.state}-${revision.version}-${revision.correlationId}`}>
          {revision.state === "published" ? "Publication" : "Brouillon"} v{revision.version}
          {" · "}
          {formatDate(revision.createdAt)}
          {" · réf. "}
          <code>{revision.correlationId}</code>
        </li>
      ))}
    </ul>
  );
}

function formatDate(value: string | null) {
  return value ? new Date(value).toLocaleString("fr-FR") : "—";
}
