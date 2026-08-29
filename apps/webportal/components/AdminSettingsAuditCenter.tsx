"use client";

import type {
  SettingsAuditView,
  SettingsPermissionOverview,
} from "@kermaria/shared";
import Link from "next/link";
import { useState } from "react";

import { StatusBadge } from "@/components/StatusBadge";

const riskLabels: Record<string, string> = {
  low: "Faible",
  medium: "Moyen",
  high: "Élevé",
  critical: "Critique",
};

const outcomeLabels: Record<string, string> = {
  success: "Appliqué",
  refused: "Refusé",
  error: "Erreur",
};

function formatDate(value: string): string {
  if (!value) return "—";
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? value
    : parsed.toLocaleString("fr-FR", { timeZone: "Europe/Paris" });
}

/**
 * Le formulaire est un GET natif : la recherche vit alors dans l'URL, donc elle
 * se partage et se recharge telle quelle. Une reference d'incident se retrouve
 * ainsi sans avoir a rejouer la saisie.
 */
function Filters({ audit }: { audit: SettingsAuditView }) {
  const { filters } = audit;
  return (
    <form className="admin-audit-filters" method="get">
      <div>
        <label htmlFor="audit-from">Depuis</label>
        <input
          defaultValue={filters.from ?? ""}
          id="audit-from"
          name="from"
          type="datetime-local"
        />
      </div>
      <div>
        <label htmlFor="audit-to">Jusqu&apos;à</label>
        <input
          defaultValue={filters.to ?? ""}
          id="audit-to"
          name="to"
          type="datetime-local"
        />
      </div>
      <div>
        <label htmlFor="audit-actor">Acteur</label>
        <input
          defaultValue={filters.actor ?? ""}
          id="audit-actor"
          name="actor"
          placeholder="Nom affiché ou service"
          type="text"
        />
      </div>
      <div>
        <label htmlFor="audit-category">Domaine</label>
        <select
          defaultValue={filters.category ?? ""}
          id="audit-category"
          name="category"
        >
          <option value="">Tous</option>
          {audit.categories.map((category) => (
            <option key={category.key} value={category.key}>
              {category.label}
            </option>
          ))}
        </select>
      </div>
      <div>
        <label htmlFor="audit-risk">Niveau de risque</label>
        <select defaultValue={filters.risk ?? ""} id="audit-risk" name="risk">
          <option value="">Tous</option>
          {audit.risks.map((risk) => (
            <option key={risk} value={risk}>
              {riskLabels[risk] ?? risk}
            </option>
          ))}
        </select>
      </div>
      <div>
        <label htmlFor="audit-outcome">Résultat</label>
        <select
          defaultValue={filters.outcome ?? ""}
          id="audit-outcome"
          name="outcome"
        >
          <option value="">Tous</option>
          {audit.outcomes.map((outcome) => (
            <option key={outcome} value={outcome}>
              {outcomeLabels[outcome] ?? outcome}
            </option>
          ))}
        </select>
      </div>
      <div>
        <label htmlFor="audit-target">Clé ou cible</label>
        <input
          defaultValue={filters.target ?? ""}
          id="audit-target"
          name="target"
          placeholder="Clé de paramètre, modèle…"
          type="text"
        />
      </div>
      <div>
        <label htmlFor="audit-correlation">Référence</label>
        <input
          defaultValue={filters.correlationId ?? ""}
          id="audit-correlation"
          name="correlationId"
          placeholder="correlation_id exact"
          type="text"
        />
      </div>
      <div className="admin-audit-filters-actions">
        <button className="button" type="submit">
          Filtrer
        </button>
        <Link className="button ghost" href="/admin/settings/audit">
          Réinitialiser
        </Link>
      </div>
    </form>
  );
}

export function AdminSettingsAuditCenter({
  audit,
  permissions,
}: {
  audit: SettingsAuditView;
  permissions: SettingsPermissionOverview;
}) {
  const [surface, setSurface] = useState<"events" | "permissions">("events");

  return (
    <section
      aria-label="Audit de la configuration"
      className="content-panel section-card admin-settings-surface admin-settings-focused-page admin-audit"
    >
      <header className="admin-settings-focused-header">
        <div>
          <h2>Audit & permissions</h2>
          <p>
            Consultez soit les événements du Centre, soit les droits qui permettent
            d’agir. Les deux vues sont volontairement séparées.
          </p>
        </div>
        <span className="admin-settings-persistence-note">
          {audit.persistent ? "Journal persistant" : "Journal temporaire"}
        </span>
      </header>

      {!audit.persistent ? (
        <p role="status">
          Persistance non durable : les événements affichés ne survivent pas au
          redémarrage du service.
        </p>
      ) : null}
      {audit.warning ? <p role="status">{audit.warning}</p> : null}

      <div
        aria-label="Rubrique audit et permissions"
        className="admin-settings-segmented"
        role="tablist"
      >
        <button
          aria-selected={surface === "events"}
          className={surface === "events" ? "is-active" : undefined}
          onClick={() => setSurface("events")}
          role="tab"
          type="button"
        >
          Journal d’audit
        </button>
        <button
          aria-selected={surface === "permissions"}
          className={surface === "permissions" ? "is-active" : undefined}
          onClick={() => setSurface("permissions")}
          role="tab"
          type="button"
        >
          Permissions
        </button>
      </div>

      {surface === "events" ? (
        <section className="admin-settings-single-surface" aria-labelledby="settings-audit-events-title">
          <header>
            <div>
              <h3 id="settings-audit-events-title">Événements de configuration</h3>
              <p>
                Recherchez une période, un acteur ou un domaine avant d’ouvrir le
                détail dans le tableau.
              </p>
            </div>
            <StatusBadge
              label={`${audit.entries.length} événement${audit.entries.length > 1 ? "s" : ""}`}
              tone="neutral"
            />
          </header>

          <Filters audit={audit} />

          {audit.truncated ? (
            <p className="admin-settings-entity-note">
              Résultat tronqué à {audit.filters.limit} événements. Resserrez la
              période pour voir les plus anciens.
            </p>
          ) : null}

          <div className="admin-audit-table-scroll">
            <table className="admin-audit-table">
              <thead>
                <tr>
                  <th scope="col">Date</th>
                  <th scope="col">Acteur</th>
                  <th scope="col">Action</th>
                  <th scope="col">Cible</th>
                  <th scope="col">Résultat</th>
                  <th scope="col">Référence</th>
                </tr>
              </thead>
              <tbody>
                {audit.entries.length === 0 ? (
                  <tr>
                    <td colSpan={6}>
                      Aucun événement de configuration pour ces critères.
                    </td>
                  </tr>
                ) : (
                  audit.entries.map((entry) => (
                    <tr
                      className={`admin-audit-risk-${entry.risk}`}
                      key={`${entry.correlationId}-${entry.occurredAt}-${entry.action}`}
                    >
                      <td>{formatDate(entry.occurredAt)}</td>
                      <td>{entry.actor}</td>
                      <td>
                        {entry.actionLabel}
                        <small>
                          {entry.category} · risque {riskLabels[entry.risk] ?? entry.risk}
                        </small>
                      </td>
                      <td>
                        {entry.targetReference ?? "—"}
                        {entry.targetType ? <small>{entry.targetType}</small> : null}
                      </td>
                      <td>
                        <StatusBadge
                          label={outcomeLabels[entry.outcome] ?? entry.outcome}
                          tone={
                            entry.outcome === "success"
                              ? "success"
                              : entry.outcome === "refused"
                                ? "warning"
                                : entry.outcome === "error"
                                  ? "danger"
                                  : "neutral"
                          }
                        />
                        {entry.reasonCode ? (
                          <small><code>{entry.reasonCode}</code></small>
                        ) : null}
                      </td>
                      <td>
                        <code>{entry.correlationId}</code>
                        {entry.sourceAddress ? <small>{entry.sourceAddress}</small> : null}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </section>
      ) : (
        <section
          aria-labelledby="settings-permissions-title"
          className="admin-settings-single-surface"
        >
          <header>
            <div>
              <h3 id="settings-permissions-title">Permissions du Centre</h3>
              <p>{permissions.notice}</p>
            </div>
            <StatusBadge
              label={`${permissions.permissions.length} permission${permissions.permissions.length > 1 ? "s" : ""}`}
              tone="info"
            />
          </header>

          <div className="admin-audit-table-scroll">
            <table className="admin-audit-table">
              <thead>
                <tr>
                  <th scope="col">Permission</th>
                  <th scope="col">Portée</th>
                  <th scope="col">Risque</th>
                  <th scope="col">État</th>
                </tr>
              </thead>
              <tbody>
                {permissions.permissions.map((permission) => (
                  <tr key={permission.code}>
                    <th scope="row">
                      {permission.label}
                      <small><code>{permission.code}</code></small>
                    </th>
                    <td>
                      {permission.description}
                      <small>{permission.surfaces.join(" · ")}</small>
                    </td>
                    <td>{riskLabels[permission.risk] ?? permission.risk}</td>
                    <td>
                      <StatusBadge
                        label={
                          permission.state === "granted"
                            ? `Attribuée (${permission.grantCount})`
                            : "Refusée sans attribution"
                        }
                        tone={permission.state === "granted" ? "success" : "neutral"}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </section>
  );
}
