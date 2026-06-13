import Link from "next/link";

export function AccessDenied() {
  return (
    <section className="empty-state access-denied">
      <span className="empty-state-mark" aria-hidden="true">
        403
      </span>
      <h1>Accès refusé</h1>
      <p>
        Votre compte ne dispose pas du rôle interne requis pour consulter
        cette zone.
      </p>
      <div className="empty-state-action">
        <Link className="button button-secondary" href="/dashboard">
        Retour à l&apos;espace client
        </Link>
      </div>
    </section>
  );
}
