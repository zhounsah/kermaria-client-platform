type LoadingStateProps = {
  title?: string;
  description?: string;
  compact?: boolean;
};

export function LoadingState({
  title = "Chargement en cours",
  description = "Les informations disponibles sont en cours de récupération.",
  compact = false,
}: LoadingStateProps) {
  return (
    <div
      aria-live="polite"
      className={compact ? "loading-state loading-state-compact" : "loading-state"}
      role="status"
    >
      <span aria-hidden="true" className="loading-indicator" />
      <div>
        <strong>{title}</strong>
        <p>{description}</p>
      </div>
    </div>
  );
}
