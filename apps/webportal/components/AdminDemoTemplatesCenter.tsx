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

  return (
    <section aria-label="Modèles de démonstration" className="admin-demo-templates">
      <p className="muted">
        {administered
          ? view.persistent
            ? "Les modèles sont administrés en base MariaDB : ils font autorité."
            : "Mode de démonstration : les modèles administrés disparaissent au redémarrage."
          : "Aucun modèle administré : le registre intégré au code fait autorité. Recopiez-le en base pour pouvoir le modifier."}
      </p>
      <p className="muted">
        Un modèle décrit les services affichés sur un compte de démonstration.
        Le type de service reste borné par le code : un type inconnu serait
        refusé, car ni le provisionnement ni l&apos;affichage ne sauraient le
        traiter. Les conditions commerciales affichées restent «{" "}
        {view.commercialTermsLabel} ».
      </p>

      {message ? (
        <FormMessage
          tone={message.tone}
          title={message.tone === "success" ? "Enregistré" : "Refusé"}
        >
          {message.text}
        </FormMessage>
      ) : null}

      {!administered ? (
        <button
          className="button button-primary"
          disabled={busy}
          onClick={() =>
            void send("/api/admin/settings/demo-templates/import", "POST")
          }
          type="button"
        >
          {busy ? "Recopie…" : "Recopier les modèles du code en base"}
        </button>
      ) : null}

      <ul className="admin-demo-template-list">
        {view.templates.map((template) => (
          <li className="admin-demo-template" key={template.templateKey}>
            <header>
              <h3>{template.label}</h3>
              <p className="muted">
                <code>{template.templateKey}</code> ·{" "}
                {template.source === "code"
                  ? "porté par le code"
                  : `version ${template.version}, modifié le ${formatDate(template.updatedAt)}`}
                {template.enabled ? "" : " · désactivé"}
              </p>
              {template.description ? <p>{template.description}</p> : null}
              {template.usedByProfileKeys.length > 0 ? (
                <p className="muted">
                  Référencé par : {template.usedByProfileKeys.join(", ")}
                </p>
              ) : null}
            </header>

            <section aria-label={`Aperçu — ${template.label}`}>
              <h4>Aperçu du compte de démonstration</h4>
              <ul className="admin-demo-template-services">
                {template.services.map((service) => (
                  <li key={service.name}>
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

            {template.editable ? (
              editingKey === template.templateKey ? (
                <TemplateEditor
                  busy={busy}
                  initial={toDraft(template)}
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
                    onClick={() => setEditingKey(template.templateKey)}
                    type="button"
                  >
                    Modifier
                  </button>
                  <button
                    className="button button-link"
                    disabled={busy || template.usedByProfileKeys.length > 0}
                    onClick={() => void remove(template)}
                    type="button"
                  >
                    Supprimer
                  </button>
                </div>
              )
            ) : (
              <p className="muted">
                Lecture seule tant que les modèles du code n&apos;ont pas été
                recopiés en base.
              </p>
            )}
          </li>
        ))}
      </ul>

      {administered ? (
        creating ? (
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
        ) : (
          <button
            className="button button-primary"
            disabled={busy}
            onClick={() => setCreating(true)}
            type="button"
          >
            Ajouter un modèle
          </button>
        )
      ) : null}

      <section aria-label="Conversion vers un compte client">
        <h3>Conversion vers un compte client</h3>
        <p className="muted">
          Une conversion déplace une vraie identité dans l&apos;annuaire. La
          destination est en lecture seule ici : elle se règle sur la machine
          (<code>{view.conversion.environmentVariable}</code>) et prend effet au
          redémarrage du service.
        </p>
        <dl className="admin-demo-conversion">
          <div>
            <dt>OU cible</dt>
            <dd>
              {view.conversion.targetOrganizationalUnitDn ?? "Non configurée"}
            </dd>
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
        </dl>
        {!view.conversion.configured ? (
          <p role="status">
            Aucune destination configurée : la conversion vers un compte client
            sera refusée.
          </p>
        ) : !view.conversion.withinAllowedRoots ? (
          <p role="status">
            La destination configurée sort des racines autorisées. Le
            déplacement sera refusé au moment de la conversion.
          </p>
        ) : null}
      </section>

      {view.revisions.length > 0 ? (
        <section aria-label="Historique des modèles">
          <h3>Historique</h3>
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
