import type { DirectoryOverview } from "@kermaria/shared";

const stateLabels: Record<string, string> = {
  healthy: "Opérationnel",
  warning: "À surveiller",
  critical: "Bloquant",
  info: "Information",
};

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

export function AdminDirectoryCenter({
  overview,
}: {
  overview: DirectoryOverview;
}) {
  return (
    <section
      aria-label="Annuaire et KoXo"
      className={`admin-directory admin-runtime-${overview.state}`}
    >
      <p className="muted">
        Cette page sépare deux choses que le mode <code>controlled_write</code>{" "}
        confondait : <strong>qui a le mandat</strong> sur une opération
        d&apos;annuaire, et <strong>ce que cette application s&apos;autorise</strong>.
        Une capacité technique n&apos;est pas une autorité.
      </p>
      <p className="muted">
        Tout y est en lecture. Rendre le mode annuaire modifiable depuis une page
        web permettrait d&apos;élargir la portée d&apos;écriture sur un annuaire
        de production depuis un navigateur — précisément ce que le bornage par
        racines autorisées existe pour empêcher.
      </p>

      <p className="muted">
        Mode : <strong>{overview.mode}</strong> ·{" "}
        {stateLabels[overview.state] ?? overview.state}
        {overview.configurationValid ? "" : " · configuration invalide"}
      </p>
      {overview.warning ? <p role="status">{overview.warning}</p> : null}

      <section aria-label="Autorités">
        <h3>Autorités</h3>
        <div className="admin-runtime-table-scroll">
          <table className="admin-runtime-table">
            <caption>Qui a le mandat, opération par opération</caption>
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

      <section aria-label="Périmètres d'écriture et configuration">
        <h3>Périmètres et configuration</h3>
        <div className="admin-runtime-table-scroll">
          <table className="admin-runtime-table">
            <caption>Réglages appliqués — lecture seule</caption>
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
                    <small>
                      <code>{policy.key}</code>
                    </small>
                  </th>
                  <td>
                    {policy.value}
                    {policy.sensitive ? (
                      <small> (valeur jamais transmise)</small>
                    ) : null}
                  </td>
                  <td>
                    {classificationLabels[policy.classification]
                      ?? policy.classification}
                  </td>
                  <td>{policy.restartRequired ? "Oui" : "Non"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section aria-label="Racines autorisées">
        <h3>Racines autorisées</h3>
        <p className="muted">
          Toute écriture hors de ces racines est refusée, quel que soit le mode.
        </p>
        {overview.allowedRoots.length === 0 ? (
          <p role="status">
            Aucune racine autorisée : les écritures sont refusées.
          </p>
        ) : (
          <ul className="admin-directory-roots">
            {overview.allowedRoots.map((root) => (
              <li key={root}>
                <code>{root}</code>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-label="Écritures d'annuaire">
        <h3>Écritures d&apos;annuaire</h3>
        <p className="muted">{overview.writesNotice}</p>
        <div className="admin-runtime-table-scroll">
          <table className="admin-runtime-table">
            <caption>
              Qui a écrit dans l&apos;annuaire, quoi, quand, pour quel parcours (
              {overview.writes.length})
            </caption>
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
                <tr>
                  <td colSpan={6}>Aucune écriture d&apos;annuaire enregistrée.</td>
                </tr>
              ) : (
                overview.writes.map((entry) => (
                  <tr key={`${entry.correlationId}-${entry.occurredAt}-${entry.operation}`}>
                    <td>{formatDate(entry.occurredAt)}</td>
                    <td>
                      {entry.operation}
                      <small>
                        {entry.engine} · {entry.workflow}
                      </small>
                    </td>
                    <td>
                      {entry.actor ?? "API-INTERNAL"}
                      {entry.customerReference ? (
                        <small>{entry.customerReference}</small>
                      ) : null}
                    </td>
                    <td>
                      <code>{entry.targetReference}</code>
                    </td>
                    <td>
                      {statusLabels[entry.status] ?? entry.status}
                      {entry.resultCode ? (
                        <small>
                          <code>{entry.resultCode}</code>
                        </small>
                      ) : null}
                      {entry.changed === false ? (
                        <small>Aucun changement</small>
                      ) : null}
                    </td>
                    <td>
                      <code>{entry.correlationId}</code>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </section>
    </section>
  );
}
