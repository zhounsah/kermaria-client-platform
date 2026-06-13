import type { CorrelationId, DataSource } from "@kermaria/shared";

type MockNoticeProps = {
  source: DataSource;
  correlationId?: CorrelationId;
};

export function MockNotice({
  source,
  correlationId,
}: MockNoticeProps) {
  if (source === "unavailable") {
    return (
      <div className="demo-notice demo-notice-error" role="status">
        <strong>Données indisponibles</strong>
        <span>
          La source interne n&apos;a pas pu être jointe. Aucun détail technique
          n&apos;est affiché.
          {correlationId ? ` Référence : ${correlationId}.` : ""}
        </span>
      </div>
    );
  }

  if (source === "api-internal-persistent") {
    return (
      <div className="demo-notice">
        <strong>Données enregistrées</strong>
        <span>
          Données fournies côté serveur par API-INTERNAL et persistées dans
          MariaDB. Le navigateur ne se connecte jamais directement à la base.
        </span>
      </div>
    );
  }

  return (
    <div className="demo-notice">
      <strong>Mode démonstration</strong>
      <span>
        {source === "api-internal-mock"
          ? "Données fournies côté serveur par API-INTERNAL en mode mock, sans persistance."
          : "Fallback local de développement actif, sans appel à un système réel."}
      </span>
    </div>
  );
}
