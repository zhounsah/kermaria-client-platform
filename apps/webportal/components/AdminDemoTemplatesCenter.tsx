"use client";

import type {
  DemoContentTemplateAdminView,
  DemoContentTemplateItem,
  DemoContentTemplateMutationResponse,
  DemoContentTemplateSavePayload,
  DemoContentTemplateServicePayload,
} from "@kermaria/shared";
import { useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { requestBffJson } from "@/lib/client-api";

const serviceTypeLabels: Record<string, string> = {
  personal_hosting: "Hébergement personnel",
  storage: "Stockage",
  backup: "Sauvegarde",
  vpn: "VPN",
  rds: "Bureau distant / RDS",
  support: "Support",
  cloud: "Cloud",
  documentation: "Documentation",
  monitoring: "Supervision",
  user: "Utilisateur",
  other: "Autre",
};

function formatDate(value: string | null): string {
  if (!value) return "—";
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? value
    : parsed.toLocaleString("fr-FR", { timeZone: "Europe/Paris" });
}

function emptyService(): DemoContentTemplateServicePayload {
  return { serviceType: "personal_hosting", name: "", description: "", scope: "" };
}

function toDraft(template: DemoContentTemplateItem): DemoContentTemplateSavePayload {
  return {
    templateKey: template.templateKey,
    label: template.label,
    description: template.description,
    enabled: template.enabled,
    displayOrder: template.displayOrder,
    expectedVersion: template.version,
    services: template.services.map((service) => ({ ...service })),
  };
}

export function AdminDemoTemplatesCenter({
  initialView,
}: {
  initialView: DemoContentTemplateAdminView;
}) {
  const [view, setView] = useState(initialView);
  const [message, setMessage] = useState<
    { tone: "success" | "error"; text: string } | null
  >(null);
  const [editingKey, setEditingKey] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [busy, setBusy] = useState(false);
  const [surface, setSurface] = useState<"templates" | "conversion" | "history">(
    "templates",
  );
  const [selectedKey, setSelectedKey] = useState(
    initialView.templates[0]?.templateKey ?? "",
  );

  function applyResult(result: DemoContentTemplateMutationResponse) {
    if (result.view) setView(result.view);
    const succeeded = result.code.endsWith("_SAVED")
      || result.code.endsWith("_DELETED")
      || result.code.endsWith("_IMPORTED");
    setMessage({ tone: succeeded ? "success" : "error", text: result.message });
    if (succeeded) {
      setEditingKey(null);
      setCreating(false);
    }
  }

  async function send(
    path: `/api/${string}`,
    method: "PUT" | "DELETE" | "POST",
    body?: DemoContentTemplateSavePayload,
  ) {
    setBusy(true);
    const result = await requestBffJson<DemoContentTemplateMutationResponse>(
      path,
      body
        ? {
            method,
            headers: { "content-type": "application/json" },
            body: JSON.stringify(body),
          }
        : { method },
    );
    setBusy(false);
    applyResult(
      result.ok
        ? result.data
        : {
            code: result.error.code,
            message: result.error.message,
            view: null,
            correlationId: result.error.correlationId ?? "",
          },
    );
  }

  async function remove(template: DemoContentTemplateItem) {
    if (
      !window.confirm(
        `Supprimer définitivement le modèle « ${template.label} » ? Les comptes de démonstration déjà créés ne sont pas modifiés.`,
      )
    ) {
      return;
    }
    await send(
      `/api/admin/settings/demo-templates/${template.templateKey}?expectedVersion=${template.version}`,
      "DELETE",
    );
  }

  const administered = view.authority === "database";
  const selectedTemplate = view.templates.find(
    (template) => template.templateKey === selectedKey,
  ) ?? view.templates[0] ?? null;

  return (
    <section
      aria-label="Modèles de démonstration"
      className="content-panel section-card admin-settings-surface admin-settings-focused-page admin-demo-templates"
    >
      <header className="admin-settings-focused-header">
        <div>
          <h2>Démonstrations</h2>
          <p>
            Gérez les modèles de compte sans mélanger l’édition, les paramètres de
            conversion et l’historique.
          </p>
        </div>
        <span className="admin-settings-persistence-note">
          {administered
            ? view.persistent
              ? "Autorité MariaDB"
              : "Autorité temporaire"
            : "Autorité du code"}
        </span>
      </header>

      {message ? (
        <FormMessage
          tone={message.tone}
          title={message.tone === "success" ? "Enregistré" : "Refusé"}
        >
          {message.text}
        </FormMessage>
      ) : null}

      {!administered ? (
        <div className="admin-settings-callout">
          <div>
            <strong>Les modèles du code font actuellement autorité.</strong>
            <p>
              Recopiez-les en base pour pouvoir les modifier depuis le Centre de
              configuration.
            </p>
          </div>
          <button
            className="button button-primary"
            disabled={busy}
            onClick={() => void send("/api/admin/settings/demo-templates/import", "POST")}
            type="button"
          >
            {busy ? "Recopie…" : "Recopier les modèles en base"}
          </button>
        </div>
      ) : null}

      <div
        aria-label="Rubrique des démonstrations"
        className="admin-settings-segmented"
        role="tablist"
      >
        <button
          aria-selected={surface === "templates"}
          className={surface === "templates" ? "is-active" : undefined}
          onClick={() => setSurface("templates")}
          role="tab"
          type="button"
        >
          Modèles
        </button>
        <button
          aria-selected={surface === "conversion"}
          className={surface === "conversion" ? "is-active" : undefined}
          onClick={() => setSurface("conversion")}
          role="tab"
          type="button"
        >
          Conversion
        </button>
        <button
          aria-selected={surface === "history"}
          className={surface === "history" ? "is-active" : undefined}
          onClick={() => setSurface("history")}
          role="tab"
          type="button"
        >
          Historique
        </button>
      </div>

      {surface === "templates" ? (
        <div className="admin-settings-workspace admin-settings-demo-workspace">
          <aside aria-label="Modèles disponibles" className="admin-settings-selector">
            <div className="admin-settings-selector-heading">
              <strong>{view.templates.length} modèle{view.templates.length > 1 ? "s" : ""}</strong>
              <span>Sélectionnez le compte de démonstration à examiner.</span>
            </div>
            {administered ? (
              <button
                className="button button-secondary admin-settings-selector-create"
                disabled={busy}
                onClick={() => {
                  setCreating(true);
                  setEditingKey(null);
                }}
                type="button"
              >
                Ajouter un modèle
              </button>
            ) : null}
            <div className="admin-settings-selector-list">
              {view.templates.map((template) => (
                <button
                  aria-current={
                    !creating && selectedTemplate?.templateKey === template.templateKey
                      ? "true"
                      : undefined
                  }
                  className="admin-settings-selector-item"
                  key={template.templateKey}
                  onClick={() => {
                    setSelectedKey(template.templateKey);
                    setCreating(false);
                    setEditingKey(null);
                    setMessage(null);
                  }}
                  type="button"
                >
                  <span>{template.label}</span>
                  <small>
                    {template.enabled ? "Actif" : "Désactivé"}
                    {" · "}
                    {template.source === "code" ? "Code" : `v${template.version}`}
                  </small>
                </button>
              ))}
            </div>
          </aside>

          <div className="admin-settings-detail-panel">
            {creating ? (
              <section className="admin-settings-single-surface">
                <header>
                  <h3>Nouveau modèle</h3>
                  <p>Définissez le modèle puis les services présentés au compte.</p>
                </header>
                <TemplateEditor
                  busy={busy}
                  initial={{
                    templateKey: "",
                    label: "",
                    description: "",
                    enabled: true,
                    displayOrder: 100,
                    expectedVersion: 0,
                    services: [emptyService()],
                  }}
                  knownServiceTypes={view.knownServiceTypes}
                  lockKey={false}
                  onCancel={() => setCreating(false)}
                  onSubmit={(payload) =>
                    send("/api/admin/settings/demo-templates", "PUT", payload)
                  }
                />
              </section>
            ) : selectedTemplate ? (
              <article className="admin-demo-template admin-settings-selected-entity">
                <header>
                  <div>
                    <p className="eyebrow">Modèle sélectionné</p>
                    <h3>{selectedTemplate.label}</h3>
                    {selectedTemplate.description ? <p>{selectedTemplate.description}</p> : null}
                    <small>
                      <code>{selectedTemplate.templateKey}</code>
                      {" · "}
                      {selectedTemplate.source === "code"
                        ? "porté par le code"
                        : `version ${selectedTemplate.version}, modifié le ${formatDate(selectedTemplate.updatedAt)}`}
                    </small>
                  </div>
                </header>

                {selectedTemplate.usedByProfileKeys.length > 0 ? (
                  <p className="admin-settings-entity-note">
                    Référencé par : {selectedTemplate.usedByProfileKeys.join(", ")}
                  </p>
                ) : null}

                <section
                  aria-label={`Aperçu — ${selectedTemplate.label}`}
                  className="admin-settings-detail-section"
                >
                  <header>
                    <h4>Aperçu du compte</h4>
                    <p>
                      {selectedTemplate.services.length} service
                      {selectedTemplate.services.length > 1 ? "s" : ""} présenté
                      {selectedTemplate.services.length > 1 ? "s" : ""}.
                    </p>
                  </header>
                  <ul className="admin-demo-template-services">
                    {selectedTemplate.services.map((service) => (
                      <li key={`${service.serviceType}-${service.name}`}>
                        <strong>{service.name}</strong>
                        <small>
                          {serviceTypeLabels[service.serviceType] ?? service.serviceType}
                          {" · "}
                          {view.commercialTermsLabel}
                        </small>
                        <span>{service.description}</span>
                        <small>{service.scope}</small>
                      </li>
                    ))}
                  </ul>
                </section>

                {selectedTemplate.editable ? (
                  editingKey === selectedTemplate.templateKey ? (
                    <TemplateEditor
                      busy={busy}
                      initial={toDraft(selectedTemplate)}
                      knownServiceTypes={view.knownServiceTypes}
                      lockKey
                      onCancel={() => setEditingKey(null)}
                      onSubmit={(payload) =>
                        send("/api/admin/settings/demo-templates", "PUT", payload)
                      }
                    />
                  ) : (
                    <div className="admin-demo-template-actions">
                      <button
                        className="button button-secondary"
                        disabled={busy}
                        onClick={() => setEditingKey(selectedTemplate.templateKey)}
                        type="button"
                      >
                        Modifier ce modèle
                      </button>
                      <button
                        className="button button-link"
                        disabled={busy || selectedTemplate.usedByProfileKeys.length > 0}
                        onClick={() => void remove(selectedTemplate)}
                        type="button"
                      >
                        Supprimer
                      </button>
                    </div>
                  )
                ) : (
                  <p className="admin-settings-inline-state muted">
                    Lecture seule tant que les modèles du code n&apos;ont pas été
                    recopiés en base.
                  </p>
                )}
              </article>
            ) : (
              <p className="admin-settings-inline-state muted">Aucun modèle disponible.</p>
            )}
          </div>
        </div>
      ) : null}

      {surface === "conversion" ? (
        <section
          aria-labelledby="demo-conversion-title"
          className="admin-settings-single-surface"
        >
          <header>
            <h3 id="demo-conversion-title">Conversion vers un compte client</h3>
            <p>
              La destination d’annuaire reste volontairement en lecture seule et se
              configure sur la machine.
            </p>
          </header>
          <dl className="admin-demo-conversion">
            <div>
              <dt>OU cible</dt>
              <dd>{view.conversion.targetOrganizationalUnitDn ?? "Non configurée"}</dd>
            </div>
            <div>
              <dt>Racines autorisées</dt>
              <dd>
                {view.conversion.allowedRoots.length > 0
                  ? view.conversion.allowedRoots.join(" · ")
                  : "Aucune"}
              </dd>
            </div>
            <div>
              <dt>Mode annuaire</dt>
              <dd>{view.conversion.adIntegrationMode}</dd>
            </div>
            <div>
              <dt>Variable d’environnement</dt>
              <dd><code>{view.conversion.environmentVariable}</code></dd>
            </div>
          </dl>
          {!view.conversion.configured ? (
            <p role="status">
              Aucune destination configurée : la conversion vers un compte client sera
              refusée.
            </p>
          ) : !view.conversion.withinAllowedRoots ? (
            <p role="status">
              La destination configurée sort des racines autorisées. Le déplacement sera
              refusé.
            </p>
          ) : null}
        </section>
      ) : null}

      {surface === "history" ? (
        <section
          aria-labelledby="demo-history-title"
          className="admin-settings-single-surface"
        >
          <header>
            <h3 id="demo-history-title">Historique des modèles</h3>
            <p>Dernières versions et résultats d’enregistrement.</p>
          </header>
          {view.revisions.length > 0 ? (
            <ul className="admin-demo-template-history">
              {view.revisions.map((revision) => (
                <li key={`${revision.templateKey}-${revision.version}-${revision.createdAt}`}>
                  <strong>{revision.templateKey}</strong>
                  <small>
                    version {revision.version} · {revision.outcome} ·{" "}
                    {formatDate(revision.createdAt)}
                  </small>
                  <small>Référence : {revision.correlationId}</small>
                </li>
              ))}
            </ul>
          ) : (
            <p className="admin-settings-inline-state muted">
              Aucune révision enregistrée.
            </p>
          )}
        </section>
      ) : null}
    </section>
  );
}

function TemplateEditor({
  busy,
  initial,
  knownServiceTypes,
  lockKey,
  onCancel,
  onSubmit,
}: {
  busy: boolean;
  initial: DemoContentTemplateSavePayload;
  knownServiceTypes: string[];
  lockKey: boolean;
  onCancel: () => void;
  onSubmit: (payload: DemoContentTemplateSavePayload) => Promise<void>;
}) {
  const [draft, setDraft] = useState(initial);

  function updateService(
    index: number,
    patch: Partial<DemoContentTemplateServicePayload>,
  ) {
    setDraft((current) => ({
      ...current,
      services: current.services.map((service, position) =>
        position === index ? { ...service, ...patch } : service,
      ),
    }));
  }

  function move(index: number, offset: number) {
    setDraft((current) => {
      const target = index + offset;
      if (target < 0 || target >= current.services.length) return current;
      const services = [...current.services];
      [services[index], services[target]] = [services[target], services[index]];
      return { ...current, services };
    });
  }

  return (
    <form
      className="admin-demo-template-editor"
      onSubmit={(event) => {
        event.preventDefault();
        void onSubmit(draft);
      }}
    >
      <label htmlFor={`${initial.templateKey || "new"}-key`}>Clé</label>
      <input
        disabled={lockKey}
        id={`${initial.templateKey || "new"}-key`}
        onChange={(event) =>
          setDraft({ ...draft, templateKey: event.target.value })
        }
        pattern="[a-z0-9][a-z0-9-]{1,63}"
        required
        value={draft.templateKey}
      />
      <p className="muted">
        Minuscules, chiffres et tirets. La clé est définitive : elle est
        référencée par les profils de démonstration.
      </p>

      <label htmlFor={`${initial.templateKey || "new"}-label`}>Libellé</label>
      <input
        id={`${initial.templateKey || "new"}-label`}
        maxLength={120}
        onChange={(event) => setDraft({ ...draft, label: event.target.value })}
        required
        value={draft.label}
      />

      <label htmlFor={`${initial.templateKey || "new"}-description`}>
        Description
      </label>
      <textarea
        id={`${initial.templateKey || "new"}-description`}
        maxLength={500}
        onChange={(event) =>
          setDraft({ ...draft, description: event.target.value })
        }
        rows={2}
        value={draft.description}
      />

      <label htmlFor={`${initial.templateKey || "new"}-order`}>
        Ordre d&apos;affichage
      </label>
      <input
        id={`${initial.templateKey || "new"}-order`}
        min={0}
        onChange={(event) =>
          setDraft({ ...draft, displayOrder: Number(event.target.value) })
        }
        type="number"
        value={draft.displayOrder}
      />

      <label className="admin-demo-template-toggle">
        <input
          checked={draft.enabled}
          onChange={(event) =>
            setDraft({ ...draft, enabled: event.target.checked })
          }
          type="checkbox"
        />
        Modèle proposé à la création d&apos;un compte de démonstration
      </label>

      <fieldset>
        <legend>Services</legend>
        {draft.services.map((service, index) => (
          // L'ordre est l'identite d'une ligne en cours d'edition : le nom peut
          // etre vide ou en doublon tant que la saisie n'est pas terminee.
          <div className="admin-demo-template-service" key={index}>
            <label htmlFor={`service-${index}-type`}>Type</label>
            <select
              id={`service-${index}-type`}
              onChange={(event) =>
                updateService(index, { serviceType: event.target.value })
              }
              value={service.serviceType}
            >
              {knownServiceTypes.map((type) => (
                <option key={type} value={type}>
                  {serviceTypeLabels[type] ?? type}
                </option>
              ))}
            </select>

            <label htmlFor={`service-${index}-name`}>Nom</label>
            <input
              id={`service-${index}-name`}
              maxLength={160}
              onChange={(event) =>
                updateService(index, { name: event.target.value })
              }
              required
              value={service.name}
            />

            <label htmlFor={`service-${index}-description`}>Description</label>
            <input
              id={`service-${index}-description`}
              maxLength={500}
              onChange={(event) =>
                updateService(index, { description: event.target.value })
              }
              required
              value={service.description}
            />

            <label htmlFor={`service-${index}-scope`}>Périmètre</label>
            <input
              id={`service-${index}-scope`}
              maxLength={300}
              onChange={(event) =>
                updateService(index, { scope: event.target.value })
              }
              required
              value={service.scope}
            />

            <div className="admin-demo-template-service-actions">
              <button
                className="button button-link"
                disabled={index === 0}
                onClick={() => move(index, -1)}
                type="button"
              >
                Monter
              </button>
              <button
                className="button button-link"
                disabled={index === draft.services.length - 1}
                onClick={() => move(index, 1)}
                type="button"
              >
                Descendre
              </button>
              <button
                className="button button-link"
                disabled={draft.services.length === 1}
                onClick={() =>
                  setDraft({
                    ...draft,
                    services: draft.services.filter(
                      (_, position) => position !== index,
                    ),
                  })
                }
                type="button"
              >
                Retirer
              </button>
            </div>
          </div>
        ))}
        <button
          className="button button-secondary"
          onClick={() =>
            setDraft({ ...draft, services: [...draft.services, emptyService()] })
          }
          type="button"
        >
          Ajouter un service
        </button>
      </fieldset>

      <div className="admin-demo-template-actions">
        <button className="button button-primary" disabled={busy} type="submit">
          {busy ? "Enregistrement…" : "Enregistrer"}
        </button>
        <button
          className="button button-link"
          disabled={busy}
          onClick={onCancel}
          type="button"
        >
          Annuler
        </button>
      </div>
    </form>
  );
}
