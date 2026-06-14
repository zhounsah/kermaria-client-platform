import type { RequestType } from "@kermaria/shared";

import {
  serviceRequestStatus,
  supportStatus,
} from "@/lib/formatters";

type AdminRequestFiltersProps = {
  order?: string;
  priority?: string;
  requestType: RequestType;
  status?: string;
};

export function AdminRequestFilters({
  order = "newest",
  priority,
  requestType,
  status,
}: AdminRequestFiltersProps) {
  const definitions = requestType === "support"
    ? supportStatus
    : serviceRequestStatus;

  return (
    <form className="admin-filters" method="get">
      <div className="field">
        <label htmlFor="status-filter">Statut</label>
        <select defaultValue={status ?? ""} id="status-filter" name="status">
          <option value="">Tous les statuts</option>
          {Object.entries(definitions).map(([value, definition]) => (
            <option key={value} value={value}>
              {definition.label}
            </option>
          ))}
        </select>
      </div>
      {requestType === "support" ? (
        <div className="field">
          <label htmlFor="priority-filter">Priorité</label>
          <select
            defaultValue={priority ?? ""}
            id="priority-filter"
            name="priority"
          >
            <option value="">Toutes les priorités</option>
            <option value="high">Haute</option>
            <option value="normal">Normale</option>
            <option value="low">Faible</option>
          </select>
        </div>
      ) : null}
      <div className="field">
        <label htmlFor="order-filter">Tri</label>
        <select defaultValue={order} id="order-filter" name="order">
          <option value="newest">Activité récente</option>
          <option value="oldest">Création ancienne</option>
          <option value="status">Statut</option>
        </select>
      </div>
      <button className="button button-secondary" type="submit">
        Appliquer les filtres
      </button>
    </form>
  );
}
