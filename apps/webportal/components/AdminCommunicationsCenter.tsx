"use client";

import type {
  CommunicationTemplateCollection,
  CommunicationTemplateRevisionItem,
  CommunicationTemplateRevisionsResponse,
  CommunicationTemplateScope,
  CommunicationTemplateVariable,
  EmailTemplateItem,
  EmailTemplateMutationResponse,
  EmailTemplatePreviewResponse,
  CommunicationTemplateSimpleResponse,
  NotificationTemplateItem,
  NotificationTemplateMutationResponse,
  SystemSnippetItem,
  SystemSnippetMutationResponse,
} from "@kermaria/shared";
import { useCallback, useEffect, useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { requestBffJson } from "@/lib/client-api";

type Banner = { tone: "success" | "error"; text: string };

type TabId = "email" | "notification" | "snippet";

const TABS: { id: TabId; label: string }[] = [
  { id: "email", label: "E-mails transactionnels" },
  { id: "notification", label: "Notifications portail" },
  { id: "snippet", label: "Textes système" },
];

/** Message lisible pour une erreur remontée par le BFF ou l'API. */
function describeFailure(code: string, message: string) {
  switch (code) {
    case "TEMPLATE_VERSION_CONFLICT":
      return "Ce modèle a été modifié ailleurs. Rechargez la page avant de recommencer.";
    case "TEMPLATE_UNKNOWN_VARIABLE":
      return message;
    case "CSRF_FORBIDDEN":
      return "La modification doit être confirmée par un jeton CSRF valide. Rechargez la page.";
    case "ACCESS_DENIED":
      return "Votre compte ne dispose pas de la permission « settings.templates.write ».";
    default:
      return message;
  }
}

export function AdminCommunicationsCenter({
  initialCollection,
}: {
  initialCollection: CommunicationTemplateCollection;
}) {
  const [collection, setCollection] = useState(initialCollection);
  const [tab, setTab] = useState<TabId>("email");
  const [banner, setBanner] = useState<Banner | null>(null);

  const notify = useCallback((next: Banner) => setBanner(next), []);

  return (
    <section aria-label="Messages et communications" className="admin-settings-center">
      <p className="muted">
        {collection.persistent
          ? "Les modèles sont persistés dans MariaDB. Une clé absente retombe sur le modèle intégré au code."
          : "Mode de démonstration : les modifications disparaissent au redémarrage."}
      </p>

      {banner ? (
        <FormMessage
          tone={banner.tone}
          title={banner.tone === "success" ? "Modèle enregistré" : "Modification refusée"}
        >
          {banner.text}
        </FormMessage>
      ) : null}

      <div className="admin-tablist" role="tablist">
        {TABS.map((entry) => (
          <button
            aria-selected={tab === entry.id}
            className={tab === entry.id ? "button button-secondary" : "button button-link"}
            key={entry.id}
            onClick={() => setTab(entry.id)}
            role="tab"
            type="button"
          >
            {entry.label}
          </button>
        ))}
      </div>

      {tab === "email" ? (
        <div className="admin-settings-grid">
          {collection.emailTemplates.map((template) => (
            <EmailTemplateEditor
              key={template.key}
              onError={(text) => notify({ tone: "error", text })}
              onSaved={(next, text) => {
                setCollection((current) => ({
                  ...current,
                  emailTemplates: current.emailTemplates.map((item) =>
                    item.key === next.key ? next : item,
                  ),
                }));
                notify({ tone: "success", text });
              }}
              template={template}
            />
          ))}
        </div>
      ) : null}

      {tab === "notification" ? (
        <div className="admin-settings-grid">
          {collection.notificationTemplates.map((template) => (
            <NotificationTemplateEditor
              key={template.key}
              onError={(text) => notify({ tone: "error", text })}
              onSaved={(next, text) => {
                setCollection((current) => ({
                  ...current,
                  notificationTemplates: current.notificationTemplates.map((item) =>
                    item.key === next.key ? next : item,
                  ),
                }));
                notify({ tone: "success", text });
              }}
              template={template}
            />
          ))}
        </div>
      ) : null}

      {tab === "snippet" ? (
        <div className="admin-settings-grid">
          {collection.snippets.map((snippet) => (
            <SnippetEditor
              key={snippet.key}
              onError={(text) => notify({ tone: "error", text })}
              onSaved={(next, text) => {
                setCollection((current) => ({
                  ...current,
                  snippets: current.snippets.map((item) =>
                    item.key === next.key ? next : item,
                  ),
                }));
                notify({ tone: "success", text });
              }}
              snippet={snippet}
            />
          ))}
        </div>
      ) : null}
    </section>
  );
}

/** Avertit avant de quitter la page si une édition n'est pas enregistrée. */
function useUnsavedGuard(dirty: boolean) {
  useEffect(() => {
    if (!dirty) return;
    const warn = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);
}

function VariableList({
  variables,
  onInsert,
}: {
  variables: CommunicationTemplateVariable[];
  onInsert: (token: string) => void;
}) {
  if (variables.length === 0) {
    return <p className="muted">Ce modèle n&apos;accepte aucune variable.</p>;
  }
  return (
    <div className="admin-template-variables">
      <p className="muted">
        Variables autorisées — toute autre variable fait échouer l&apos;enregistrement.
      </p>
      <ul>
        {variables.map((variable) => (
          <li key={variable.name}>
            <button
              className="button button-link"
              onClick={() => onInsert(`{{${variable.name}}}`)}
              type="button"
            >
              <code>{`{{${variable.name}}}`}</code>
            </button>
            <span> {variable.description}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

function RevisionHistory({
  scope,
  templateKey,
}: {
  scope: CommunicationTemplateScope;
  templateKey: string;
}) {
  const [open, setOpen] = useState(false);
  const [revisions, setRevisions] = useState<CommunicationTemplateRevisionItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setOpen(true);
    if (revisions) return;
    const result = await requestBffJson<CommunicationTemplateRevisionsResponse>(
      `/api/admin/communications/${scope}/${encodeURIComponent(templateKey)}/revisions`,
      { method: "GET" },
    );
    if (!result.ok) {
      setError(result.error.message);
      return;
    }
    setRevisions(result.data.revisions);
  }

  return (
    <div className="admin-template-history">
      {open ? (
        <>
          <button className="button button-link" onClick={() => setOpen(false)} type="button">
            Masquer l&apos;historique
          </button>
          {error ? <p role="status">{error}</p> : null}
          {revisions && revisions.length === 0 ? (
            <p className="muted">Aucune modification enregistrée.</p>
          ) : null}
          {revisions && revisions.length > 0 ? (
            <ul>
              {revisions.map((revision) => (
                <li key={`${revision.version}-${revision.correlationId}`}>
                  v{revision.version} · {revision.outcome === "restored" ? "restauré" : "modifié"} ·{" "}
                  {new Date(revision.createdAt).toLocaleString("fr-FR")} · réf.{" "}
                  <code>{revision.correlationId}</code>
                </li>
              ))}
            </ul>
          ) : null}
        </>
      ) : (
        <button className="button button-link" onClick={() => void load()} type="button">
          Voir l&apos;historique
        </button>
      )}
    </div>
  );
}

function EmailTemplateEditor({
  template,
  onSaved,
  onError,
}: {
  template: EmailTemplateItem;
  onSaved: (template: EmailTemplateItem, message: string) => void;
  onError: (message: string) => void;
}) {
  const [subject, setSubject] = useState(template.subject);
  const [body, setBody] = useState(template.body);
  const [enabled, setEnabled] = useState(template.enabled);
  const [busy, setBusy] = useState(false);
  const [preview, setPreview] = useState<{ subject: string; body: string } | null>(null);
  const bodyRef = useRef<HTMLTextAreaElement>(null);
  const dirty =
    subject !== template.subject || body !== template.body || enabled !== template.enabled;
  useUnsavedGuard(dirty);

  function insertVariable(token: string) {
    const textarea = bodyRef.current;
    if (!textarea) {
      setBody((current) => `${current}${token}`);
      return;
    }
    const start = textarea.selectionStart ?? body.length;
    const end = textarea.selectionEnd ?? body.length;
    setBody(`${body.slice(0, start)}${token}${body.slice(end)}`);
  }

  async function save() {
    if (!dirty || busy) return;
    setBusy(true);
    const result = await requestBffJson<EmailTemplateMutationResponse>(
      `/api/admin/communications/email/${encodeURIComponent(template.key)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ subject, body, enabled, expectedVersion: template.version }),
      },
    );
    setBusy(false);
    if (!result.ok) {
      onError(describeFailure(result.error.code, result.error.message));
      return;
    }
    if (!result.data.template) {
      onError(describeFailure(result.data.code, result.data.message));
      return;
    }
    onSaved(result.data.template, result.data.message);
  }

  async function restoreDefault() {
    if (busy) return;
    if (!window.confirm(`Restaurer le modèle intégré au code pour « ${template.displayName} » ?`)) {
      return;
    }
    setBusy(true);
    const result = await requestBffJson<EmailTemplateMutationResponse>(
      `/api/admin/communications/email/${encodeURIComponent(template.key)}/restore-default`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ expectedVersion: template.version }),
      },
    );
    setBusy(false);
    if (!result.ok) {
      onError(describeFailure(result.error.code, result.error.message));
      return;
    }
    if (!result.data.template) {
      onError(describeFailure(result.data.code, result.data.message));
      return;
    }
    setSubject(result.data.template.subject);
    setBody(result.data.template.body);
    setEnabled(result.data.template.enabled);
    onSaved(result.data.template, result.data.message);
  }

  async function runPreview() {
    setBusy(true);
    const result = await requestBffJson<EmailTemplatePreviewResponse>(
      `/api/admin/communications/email/${encodeURIComponent(template.key)}/preview`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ subject, body }),
      },
    );
    setBusy(false);
    if (!result.ok) {
      onError(describeFailure(result.error.code, result.error.message));
      return;
    }
    if (result.data.subject === null || result.data.body === null) {
      onError(describeFailure(result.data.code, result.data.message));
      return;
    }
    setPreview({ subject: result.data.subject, body: result.data.body });
  }

  async function sendTest(recipient: string) {
    setBusy(true);
    const result = await requestBffJson<CommunicationTemplateSimpleResponse>(
      `/api/admin/communications/email/${encodeURIComponent(template.key)}/test`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ recipient }),
      },
    );
    setBusy(false);
    if (!result.ok) {
      onError(describeFailure(result.error.code, result.error.message));
      return;
    }
    if (result.data.code !== "TEMPLATE_TEST_SENT") {
      onError(result.data.message);
      return;
    }
    onSaved(template, result.data.message);
  }

  return (
    <section className="admin-settings-card">
      <header>
        <p className="eyebrow">E-mail transactionnel</p>
        <h2>{template.displayName}</h2>
        <p>{template.description}</p>
        <small>
          <code>{template.key}</code> ·{" "}
          {template.customized ? "Personnalisé" : "Valeur par défaut"} · v{template.version}
          {template.updatedAt
            ? ` · modifié le ${new Date(template.updatedAt).toLocaleString("fr-FR")}`
            : ""}
        </small>
      </header>

      <form
        className="admin-template-form"
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
      >
        <label htmlFor={`${template.key}-subject`}>Objet</label>
        <input
          id={`${template.key}-subject`}
          onChange={(event) => setSubject(event.target.value)}
          type="text"
          value={subject}
        />

        <label htmlFor={`${template.key}-body`}>Corps (texte brut)</label>
        <textarea
          id={`${template.key}-body`}
          onChange={(event) => setBody(event.target.value)}
          ref={bodyRef}
          rows={12}
          value={body}
        />

        <label htmlFor={`${template.key}-enabled`}>Modèle personnalisé actif</label>
        <select
          id={`${template.key}-enabled`}
          onChange={(event) => setEnabled(event.target.value === "true")}
          value={String(enabled)}
        >
          <option value="true">Actif — le texte ci-dessus est envoyé</option>
          <option value="false">Inactif — repli sur le modèle intégré au code</option>
        </select>

        <VariableList onInsert={insertVariable} variables={template.variables} />

        <div className="admin-settings-control">
          <button className="button button-secondary" disabled={!dirty || busy} type="submit">
            {busy ? "Enregistrement…" : "Enregistrer"}
          </button>
          <button className="button button-link" disabled={busy} onClick={() => void runPreview()} type="button">
            Aperçu
          </button>
          <button className="button button-link" disabled={busy} onClick={() => void restoreDefault()} type="button">
            Restaurer le modèle par défaut
          </button>
          {dirty ? (
            <button
              className="button button-link"
              onClick={() => {
                setSubject(template.subject);
                setBody(template.body);
                setEnabled(template.enabled);
              }}
              type="button"
            >
              Annuler
            </button>
          ) : null}
        </div>
      </form>

      {template.testSendSupported ? (
        <TestSendForm busy={busy} onSend={(recipient) => void sendTest(recipient)} templateKey={template.key} />
      ) : (
        <p className="muted">
          L&apos;envoi de test est désactivé pour ce modèle : il dépend d&apos;un document
          commercial réel.
        </p>
      )}

      {preview ? (
        <div className="admin-template-preview">
          <h3>Aperçu</h3>
          <p>
            <strong>{preview.subject}</strong>
          </p>
          <pre>{preview.body}</pre>
        </div>
      ) : null}

      <RevisionHistory scope="email" templateKey={template.key} />
    </section>
  );
}

function TestSendForm({
  templateKey,
  busy,
  onSend,
}: {
  templateKey: string;
  busy: boolean;
  onSend: (recipient: string) => void;
}) {
  const [recipient, setRecipient] = useState("");
  return (
    <form
      className="admin-settings-control"
      onSubmit={(event) => {
        event.preventDefault();
        if (recipient.trim().length > 0) onSend(recipient.trim());
      }}
    >
      <label htmlFor={`${templateKey}-test`}>
        Envoi de test (votre propre adresse uniquement)
      </label>
      <input
        id={`${templateKey}-test`}
        onChange={(event) => setRecipient(event.target.value)}
        type="email"
        value={recipient}
      />
      <button className="button button-link" disabled={busy} type="submit">
        Envoyer un test
      </button>
    </form>
  );
}

function NotificationTemplateEditor({
  template,
  onSaved,
  onError,
}: {
  template: NotificationTemplateItem;
  onSaved: (template: NotificationTemplateItem, message: string) => void;
  onError: (message: string) => void;
}) {
  const [title, setTitle] = useState(template.title);
  const [message, setMessage] = useState(template.message);
  const [enabled, setEnabled] = useState(template.enabled);
  const [busy, setBusy] = useState(false);
  const dirty =
    title !== template.title || message !== template.message || enabled !== template.enabled;
  useUnsavedGuard(dirty);

  async function save() {
    if (!dirty || busy) return;
    setBusy(true);
    const result = await requestBffJson<NotificationTemplateMutationResponse>(
      `/api/admin/communications/notification/${encodeURIComponent(template.key)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ title, message, enabled, expectedVersion: template.version }),
      },
    );
    setBusy(false);
    if (!result.ok) {
      onError(describeFailure(result.error.code, result.error.message));
      return;
    }
    if (!result.data.template) {
      onError(describeFailure(result.data.code, result.data.message));
      return;
    }
    onSaved(result.data.template, result.data.message);
  }

  async function restoreDefault() {
    if (busy) return;
    if (!window.confirm(`Restaurer le texte par défaut pour « ${template.displayName} » ?`)) return;
    setBusy(true);
    const result = await requestBffJson<NotificationTemplateMutationResponse>(
      `/api/admin/communications/notification/${encodeURIComponent(template.key)}/restore-default`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ expectedVersion: template.version }),
      },
    );
    setBusy(false);
    if (!result.ok) {
      onError(describeFailure(result.error.code, result.error.message));
      return;
    }
    if (!result.data.template) {
      onError(describeFailure(result.data.code, result.data.message));
      return;
    }
    setTitle(result.data.template.title);
    setMessage(result.data.template.message);
    setEnabled(result.data.template.enabled);
    onSaved(result.data.template, result.data.message);
  }

  return (
    <section className="admin-settings-card">
      <header>
        <p className="eyebrow">Notification portail</p>
        <h2>{template.displayName}</h2>
        <p>{template.description}</p>
        <small>
          <code>{template.key}</code> ·{" "}
          {template.customized ? "Personnalisé" : "Valeur par défaut"} · v{template.version}
        </small>
      </header>

      <form
        className="admin-template-form"
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
      >
        <label htmlFor={`${template.key}-title`}>Titre</label>
        <input
          id={`${template.key}-title`}
          onChange={(event) => setTitle(event.target.value)}
          type="text"
          value={title}
        />

        <label htmlFor={`${template.key}-message`}>Message</label>
        <textarea
          id={`${template.key}-message`}
          onChange={(event) => setMessage(event.target.value)}
          rows={4}
          value={message}
        />

        <label htmlFor={`${template.key}-enabled`}>Texte personnalisé actif</label>
        <select
          id={`${template.key}-enabled`}
          onChange={(event) => setEnabled(event.target.value === "true")}
          value={String(enabled)}
        >
          <option value="true">Actif</option>
          <option value="false">Inactif — repli sur le texte de code</option>
        </select>

        <VariableList
          onInsert={(token) => setMessage((current) => `${current}${token}`)}
          variables={template.variables}
        />

        <div className="admin-settings-control">
          <button className="button button-secondary" disabled={!dirty || busy} type="submit">
            {busy ? "Enregistrement…" : "Enregistrer"}
          </button>
          <button className="button button-link" disabled={busy} onClick={() => void restoreDefault()} type="button">
            Restaurer le texte par défaut
          </button>
        </div>
      </form>

      <RevisionHistory scope="notification" templateKey={template.key} />
    </section>
  );
}

function SnippetEditor({
  snippet,
  onSaved,
  onError,
}: {
  snippet: SystemSnippetItem;
  onSaved: (snippet: SystemSnippetItem, message: string) => void;
  onError: (message: string) => void;
}) {
  const [body, setBody] = useState(snippet.body);
  const [busy, setBusy] = useState(false);
  const dirty = body !== snippet.body;
  useUnsavedGuard(dirty);

  async function save() {
    if (!dirty || busy) return;
    setBusy(true);
    const result = await requestBffJson<SystemSnippetMutationResponse>(
      `/api/admin/communications/snippet/${encodeURIComponent(snippet.key)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ body, expectedVersion: snippet.version }),
      },
    );
    setBusy(false);
    if (!result.ok) {
      onError(describeFailure(result.error.code, result.error.message));
      return;
    }
    if (!result.data.snippet) {
      onError(describeFailure(result.data.code, result.data.message));
      return;
    }
    onSaved(result.data.snippet, result.data.message);
  }

  async function restoreDefault() {
    if (busy) return;
    if (!window.confirm(`Restaurer le texte par défaut pour « ${snippet.displayName} » ?`)) return;
    setBusy(true);
    const result = await requestBffJson<SystemSnippetMutationResponse>(
      `/api/admin/communications/snippet/${encodeURIComponent(snippet.key)}/restore-default`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ expectedVersion: snippet.version }),
      },
    );
    setBusy(false);
    if (!result.ok) {
      onError(describeFailure(result.error.code, result.error.message));
      return;
    }
    if (!result.data.snippet) {
      onError(describeFailure(result.data.code, result.data.message));
      return;
    }
    setBody(result.data.snippet.body);
    onSaved(result.data.snippet, result.data.message);
  }

  return (
    <section className="admin-settings-card">
      <header>
        <p className="eyebrow">Texte système</p>
        <h2>{snippet.displayName}</h2>
        <p>{snippet.description}</p>
        <small>
          <code>{snippet.key}</code> · {snippet.customized ? "Personnalisé" : "Valeur par défaut"} ·
          v{snippet.version} · {body.length}/{snippet.maxLength} caractères
        </small>
      </header>

      <form
        className="admin-template-form"
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
      >
        <label htmlFor={`${snippet.key}-body`}>Texte</label>
        <textarea
          id={`${snippet.key}-body`}
          maxLength={snippet.maxLength}
          onChange={(event) => setBody(event.target.value)}
          rows={4}
          value={body}
        />

        <div className="admin-settings-control">
          <button className="button button-secondary" disabled={!dirty || busy} type="submit">
            {busy ? "Enregistrement…" : "Enregistrer"}
          </button>
          <button className="button button-link" disabled={busy} onClick={() => void restoreDefault()} type="button">
            Restaurer le texte par défaut
          </button>
          {dirty ? (
            <button className="button button-link" onClick={() => setBody(snippet.body)} type="button">
              Annuler
            </button>
          ) : null}
        </div>
      </form>

      <RevisionHistory scope="snippet" templateKey={snippet.key} />
    </section>
  );
}
