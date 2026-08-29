"use client";

import type { DirectoryOverview } from "@kermaria/shared";
import { useState } from "react";

const classificationLabels: Record<string, string> = {
  dynamic: "Modifiable à chaud",
  restart_required: "Redémarrage requis",
  secret: "Secret",
  code_invariant: "Fixé par le code",
};

const statusLabels: Record<string, string> = {
  requested: "Demandée",
  running: "En cours",
  succeeded: "Réussie",
  failed: "Échouée",
  skipped: "Sans effet",
};

function formatDate(value: string): string {
  if (!value) return "—";
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? value
    : parsed.toLocaleString("fr-FR", { timeZone: "Europe/Paris" });
}

export function AdminDirectoryTabs({ overview }: { overview: DirectoryOverview }) {
  const [surface, setSurface] = useState<
    "authorities" | "policies" | "roots" | "writes"
  >("authorities");

  return (
    <>
      <div
        aria-label="Rubrique Annuaire et KoXo"
        className="admin-settings-segmented"
        role="tablist"
      >
        <button
          aria-selected={surface === "authorities"}
          className={surface === "authorities" ? "is-active" : undefined}
          onClick={() => setSurface("authorities")}
          role="tab"
          type="button"
        >
          Autorités
        </button>
        <button
          aria-selected={surface === "policies"}
          className={surface === "policies" ? "is-active" : undefined}
          onClick={() => setSurface("policies")}
          role="tab"
          type="button"
        >
          Configuration
        </button>
        <button
          aria-selected={surface === "roots"}
          className={surface === "roots" ? "is-active" : undefined}
          onClick={() => setSurface("roots")}
          role="tab"
          type="button"
        >
          Racines
        </button>
        <button
          aria-selected={surface === "writes"}
          className={surface === "writes" ? "is-active" : undefined}
          onClick={() => setSurface("writes")}
          role="tab"
          type="button"
        >
          Écritures
        </button>
      </div>

      {surface === "authorities" ? (
        <section className="admin-settings-single-surface" aria-labelledby="directory-authorities-title">
          <header>
            <h3 id="directory-authorities-title">Autorités</h3>
            <p>Qui a le mandat, opération par opération.</p>
          </header>
          <div className="admin-runtime-table-scroll">
            <table className="admin-runtime-table">
              <thead>
                <tr>
                  <th scope="col">Opération</th>
                  <th scope="col">Autorité</th>
                  <th scope="col">Ce que cela implique</th>
                </tr>
              </thead>
              <tbody>
                {overview.authorities.map((item) => (
                  <tr key={item.operation}>
                    <th scope="row">{item.operation}</th>
                    <td>{item.authority}</td>
                    <td>{item.note}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ) : null}

      {surface === "policies" ? (
        <section className="admin-settings-single-surface" aria-labelledby="directory-policies-title">
          <header>
            <h3 id="directory-policies-title">Périmètres et configuration</h3>
            <p>Réglages effectifs en lecture seule et classification de sécurité.</p>
          </header>
          <div className="admin-runtime-table-scroll">
            <table className="admin-runtime-table">
              <thead>
                <tr>
                  <th scope="col">Réglage</th>
                  <th scope="col">Valeur</th>
                  <th scope="col">Classification</th>
                  <th scope="col">Redémarrage</th>
                </tr>
              </thead>
              <tbody>
                {overview.policies.map((policy) => (
                  <tr key={policy.key}>
                    <th scope="row">
                      {policy.label}
                      <small><code>{policy.key}</code></small>
                    </th>
                    <td>
                      {policy.value}
                      {policy.sensitive ? <small> (valeur jamais transmise)</small> : null}
                    </td>
                    <td>{classificationLabels[policy.classification] ?? policy.classification}</td>
                    <td>{policy.restartRequired ? "Oui" : "Non"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ) : null}

      {surface === "roots" ? (
        <section className="admin-settings-single-surface" aria-labelledby="directory-roots-title">
          <header>
            <h3 id="directory-roots-title">Racines autorisées</h3>
            <p>Toute écriture hors de ces racines est refusée, quel que soit le mode.</p>
          </header>
          {overview.allowedRoots.length === 0 ? (
            <p className="admin-settings-inline-state" role="status">
              Aucune racine autorisée : les écritures sont refusées.
            </p>
          ) : (
            <ul className="admin-directory-roots">
              {overview.allowedRoots.map((root) => (
                <li key={root}><code>{root}</code></li>
              ))}
            </ul>
          )}
        </section>
      ) : null}

      {surface === "writes" ? (
        <section className="admin-settings-single-surface" aria-labelledby="directory-writes-title">
          <header>
            <h3 id="directory-writes-title">Écritures d’annuaire</h3>
            <p>{overview.writesNotice}</p>
          </header>
          <div className="admin-runtime-table-scroll">
            <table className="admin-runtime-table">
              <thead>
                <tr>
                  <th scope="col">Date</th>
                  <th scope="col">Opération</th>
                  <th scope="col">Demandeur</th>
                  <th scope="col">Cible</th>
                  <th scope="col">Résultat</th>
                  <th scope="col">Référence</th>
                </tr>
              </thead>
              <tbody>
                {overview.writes.length === 0 ? (
                  <tr><td colSpan={6}>Aucune écriture d&apos;annuaire enregistrée.</td></tr>
                ) : (
                  overview.writes.map((entry) => (
                    <tr key={`${entry.correlationId}-${entry.occurredAt}-${entry.operation}`}>
                      <td>{formatDate(entry.occurredAt)}</td>
                      <td>
                        {entry.operation}
                        <small>{entry.engine} · {entry.workflow}</small>
                      </td>
                      <td>
                        {entry.actor ?? "API-INTERNAL"}
                        {entry.customerReference ? <small>{entry.customerReference}</small> : null}
                      </td>
                      <td><code>{entry.targetReference}</code></td>
                      <td>
                        {statusLabels[entry.status] ?? entry.status}
                        {entry.resultCode ? <small><code>{entry.resultCode}</code></small> : null}
                        {entry.changed === false ? <small>Aucun changement</small> : null}
                      </td>
                      <td><code>{entry.correlationId}</code></td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </section>
      ) : null}
    </>
  );
}
