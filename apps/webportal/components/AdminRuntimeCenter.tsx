"use client";

import type { RuntimeOverview, RuntimeParameterItem } from "@kermaria/shared";
import { useState } from "react";

import { StatusBadge } from "@/components/StatusBadge";

const sourceLabels: Record<RuntimeParameterItem["source"], string> = {
  environment: "Variable d'environnement",
  json: "Fichier de configuration",
  default: "Valeur par défaut",
  database: "Base de données",
};

const classificationLabels: Record<
  RuntimeParameterItem["classification"],
  string
> = {
  dynamic: "Modifiable à chaud",
  restart_required: "Redémarrage requis",
  secret: "Secret",
  code_invariant: "Fixé par le code",
};

const stateLabels: Record<string, string> = {
  healthy: "Opérationnel",
  warning: "À surveiller",
  critical: "Bloquant",
  info: "Information",
};

function formatUptime(seconds: number): string {
  if (seconds <= 0) return "—";
  const days = Math.floor(seconds / 86400);
  const hours = Math.floor((seconds % 86400) / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (days > 0) return `${days} j ${hours} h`;
  if (hours > 0) return `${hours} h ${minutes} min`;
  return `${minutes} min`;
}

function formatDate(value: string): string {
  if (!value) return "—";
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? value
    : parsed.toLocaleString("fr-FR", { timeZone: "Europe/Paris" });
}

export function AdminRuntimeCenter({
  overview,
}: {
  overview: RuntimeOverview;
}) {
  const [selectedKey, setSelectedKey] = useState(overview.sections[0]?.key ?? "");
  const selectedSection = overview.sections.find((section) => section.key === selectedKey)
    ?? overview.sections[0]
    ?? null;

  return (
    <section
      aria-label="Infrastructure et runtime"
      className="content-panel section-card admin-settings-surface admin-settings-focused-page admin-runtime"
    >
      <header className="admin-settings-focused-header">
        <div>
          <h2>Infrastructure & Runtime</h2>
          <p>
            Consultez l’environnement puis ouvrez uniquement le domaine runtime à
            diagnostiquer. Tous les réglages restent en lecture seule.
          </p>
        </div>
        <span className="admin-settings-persistence-note">
          {overview.environment} · {overview.version}
        </span>
      </header>

      <div className="admin-settings-summary-strip admin-runtime-summary-strip">
        <div>
          <span>Démarré le</span>
          <strong>{formatDate(overview.startedAt)}</strong>
        </div>
        <div>
          <span>Uptime</span>
          <strong>{formatUptime(overview.uptimeSeconds)}</strong>
        </div>
        <div>
          <span>Configuration</span>
          <strong>{overview.configurationFilePresent ? "Fichier présent" : "Fichier absent"}</strong>
        </div>
        <div>
          <span>Domaines</span>
          <strong>{overview.sections.length}</strong>
        </div>
      </div>

      {overview.configurationPath ? (
        <p className="admin-settings-entity-note">
          Fichier de configuration : <code>{overview.configurationPath}</code>
        </p>
      ) : null}

      <div className="admin-settings-workspace admin-settings-runtime-workspace">
        <aside aria-label="Domaines runtime" className="admin-settings-selector">
          <div className="admin-settings-selector-heading">
            <strong>{overview.sections.length} domaine{overview.sections.length > 1 ? "s" : ""}</strong>
            <span>Un seul groupe de paramètres est affiché à la fois.</span>
          </div>
          <div className="admin-settings-selector-list">
            {overview.sections.map((section) => (
              <button
                aria-current={selectedSection?.key === section.key ? "true" : undefined}
                className="admin-settings-selector-item"
                key={section.key}
                onClick={() => setSelectedKey(section.key)}
                type="button"
              >
                <span>{section.label}</span>
                <small>
                  {section.parameters.length} paramètre
                  {section.parameters.length > 1 ? "s" : ""}
                </small>
                <StatusBadge
                  label={stateLabels[section.state] ?? section.state}
                  tone={
                    section.state === "healthy"
                      ? "success"
                      : section.state === "warning"
                        ? "warning"
                        : section.state === "critical"
                          ? "danger"
                          : "info"
                  }
                />
              </button>
            ))}
          </div>
        </aside>

        <div className="admin-settings-detail-panel">
          {selectedSection ? (
            <section
              aria-label={selectedSection.label}
              className={`admin-runtime-section admin-runtime-${selectedSection.state}`}
            >
              <header className="admin-runtime-section-heading">
                <div>
                  <p className="eyebrow">Domaine sélectionné</p>
                  <h3>{selectedSection.label}</h3>
                  <p className="muted">
                    {selectedSection.parameters.length} paramètre
                    {selectedSection.parameters.length > 1 ? "s" : ""} résolu
                    {selectedSection.parameters.length > 1 ? "s" : ""} au runtime.
                  </p>
                </div>
                <StatusBadge
                  label={stateLabels[selectedSection.state] ?? selectedSection.state}
                  tone={
                    selectedSection.state === "healthy"
                      ? "success"
                      : selectedSection.state === "warning"
                        ? "warning"
                        : selectedSection.state === "critical"
                          ? "danger"
                          : "info"
                  }
                />
              </header>

              {selectedSection.warning ? (
                <p role="status">{selectedSection.warning}</p>
              ) : null}

              <div className="admin-runtime-table-scroll">
                <table className="admin-runtime-table">
                  <thead>
                    <tr>
                      <th scope="col">Paramètre</th>
                      <th scope="col">Valeur</th>
                      <th scope="col">Source</th>
                      <th scope="col">Classification</th>
                      <th scope="col">Redémarrage</th>
                    </tr>
                  </thead>
                  <tbody>
                    {selectedSection.parameters.map((parameter) => (
                      <tr key={parameter.key}>
                        <th scope="row">
                          {parameter.label}
                          <small><code>{parameter.key}</code></small>
                        </th>
                        <td>
                          {parameter.value}
                          {parameter.sensitive ? (
                            <small> (valeur jamais transmise)</small>
                          ) : null}
                        </td>
                        <td>{sourceLabels[parameter.source] ?? parameter.source}</td>
                        <td>
                          {classificationLabels[parameter.classification]
                            ?? parameter.classification}
                        </td>
                        <td>{parameter.restartRequired ? "Oui" : "Non"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          ) : (
            <p className="admin-settings-inline-state muted">
              Aucun domaine runtime disponible.
            </p>
          )}
        </div>
      </div>
    </section>
  );
}
