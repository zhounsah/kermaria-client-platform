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
          Impossible de charger les informations pour le moment. Réessayez dans
          quelques instants.
          {correlationId ? ` Référence : ${correlationId}.` : ""}
        </span>
      </div>
    );
  }

  if (source === "api-internal-persistent") {
    return (
      <div className="demo-notice">
        <strong>Données disponibles</strong>
        <span>
          Les informations affichées sont fournies par les services sécurisés
          du portail.
        </span>
      </div>
    );
  }

  return (
    <div className="demo-notice">
      <strong>Mode démonstration</strong>
      <span>
        {source === "api-internal-mock"
          ? "Certaines informations sont simulées et ne représentent pas une prestation réelle."
          : "Les informations affichées proviennent du mode local de développement."}
      </span>
    </div>
  );
}
