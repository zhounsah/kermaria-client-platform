import type { ReactNode } from "react";

type ErrorStateProps = {
  title?: string;
  description?: string;
  reference?: string;
  action?: ReactNode;
  compact?: boolean;
};

export function ErrorState({
  title = "Informations temporairement indisponibles",
  description = "Réessayez dans quelques instants. Si le problème persiste, contactez le support.",
  reference,
  action,
  compact = false,
}: ErrorStateProps) {
  return (
    <section
      className={compact ? "error-state error-state-compact" : "error-state"}
      role="alert"
    >
      <span aria-hidden="true" className="error-state-mark">
        !
      </span>
      <div className="error-state-content">
        <h2>{title}</h2>
        <p>{description}</p>
        {reference ? (
          <p className="error-reference">Référence : {reference}</p>
        ) : null}
        {action ? <div className="error-state-action">{action}</div> : null}
      </div>
    </section>
  );
}
