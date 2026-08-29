import type { DirectoryOverview } from "@kermaria/shared";

import { AdminDirectoryTabs } from "@/components/AdminDirectoryTabs";
import { StatusBadge } from "@/components/StatusBadge";

const stateLabels: Record<string, string> = {
  healthy: "Opérationnel",
  warning: "À surveiller",
  critical: "Bloquant",
  info: "Information",
};

export function AdminDirectoryCenter({
  overview,
}: {
  overview: DirectoryOverview;
}) {
  return (
    <section
      aria-label="Annuaire et KoXo"
      className={`content-panel section-card admin-settings-surface admin-settings-focused-page admin-directory admin-runtime-${overview.state}`}
    >
      <header className="admin-settings-focused-header">
        <div>
          <h2>Annuaire & KoXo</h2>
          <p>
            Consultez séparément l’autorité, le périmètre d’écriture, les racines
            autorisées et les opérations réellement effectuées.
          </p>
        </div>
        <div className="badge-stack">
          <StatusBadge
            label={stateLabels[overview.state] ?? overview.state}
            tone={
              overview.state === "healthy"
                ? "success"
                : overview.state === "warning"
                  ? "warning"
                  : overview.state === "critical"
                    ? "danger"
                    : "info"
            }
          />
          <StatusBadge
            label={overview.configurationValid ? "Configuration valide" : "Configuration invalide"}
            tone={overview.configurationValid ? "success" : "danger"}
          />
        </div>
      </header>

      <div className="admin-settings-summary-strip">
        <div>
          <span>Mode</span>
          <strong>{overview.mode}</strong>
        </div>
        <div>
          <span>Racines autorisées</span>
          <strong>{overview.allowedRoots.length}</strong>
        </div>
        <div>
          <span>Écritures enregistrées</span>
          <strong>{overview.writes.length}</strong>
        </div>
      </div>

      {overview.warning ? <p role="status">{overview.warning}</p> : null}

      <p className="admin-settings-entity-note">
        Cette vue reste en lecture seule. Les secrets sont signalés par leur présence
        uniquement : valeur jamais transmise.
      </p>
      <p className="admin-settings-entity-note">{overview.writesNotice}</p>

      <AdminDirectoryTabs overview={overview} />
    </section>
  );
}
