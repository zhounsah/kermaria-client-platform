import type { RuntimeOverview, RuntimeParameterItem } from "@kermaria/shared";

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
  return (
    <section aria-label="Infrastructure et runtime" className="admin-runtime">
      <p className="muted">
        Cette vue rend l&apos;exploitation lisible : chaque paramètre porte
        d&apos;où vient réellement sa valeur, sa classification et le fait
        qu&apos;un redémarrage soit nécessaire. Elle n&apos;expose pas le
        contenu du fichier de configuration, et aucun secret n&apos;y figure —
        la chaîne de connexion n&apos;est jamais renvoyée, seuls ses composants
        non sensibles le sont.
      </p>
      <p className="muted">
        Tout y est en lecture seule : ces réglages sont résolus au démarrage du
        service et se corrigent sur la machine.
      </p>

      <dl className="admin-runtime-summary">
        <div>
          <dt>Environnement</dt>
          <dd>{overview.environment}</dd>
        </div>
        <div>
          <dt>Version</dt>
          <dd>{overview.version}</dd>
        </div>
        <div>
          <dt>Démarré le</dt>
          <dd>{formatDate(overview.startedAt)}</dd>
        </div>
        <div>
          <dt>Depuis</dt>
          <dd>{formatUptime(overview.uptimeSeconds)}</dd>
        </div>
        <div>
          <dt>Fichier de configuration</dt>
          <dd>
            {overview.configurationFilePresent ? "Présent" : "Absent"}
            {overview.configurationPath
              ? ` — ${overview.configurationPath}`
              : ""}
          </dd>
        </div>
      </dl>

      {overview.sections.map((section) => (
        <section
          aria-label={section.label}
          className={`admin-runtime-section admin-runtime-${section.state}`}
          key={section.key}
        >
          <h3>{section.label}</h3>
          <p className="muted">{stateLabels[section.state] ?? section.state}</p>
          {section.warning ? <p role="status">{section.warning}</p> : null}
          <div className="admin-runtime-table-scroll">
            <table className="admin-runtime-table">
              <caption>Paramètres runtime — {section.label}</caption>
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
                {section.parameters.map((parameter) => (
                  <tr key={parameter.key}>
                    <th scope="row">
                      {parameter.label}
                      <small>
                        <code>{parameter.key}</code>
                      </small>
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
      ))}
    </section>
  );
}
