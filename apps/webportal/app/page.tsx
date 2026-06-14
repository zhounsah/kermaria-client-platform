import Link from "next/link";

export default function HomePage() {
  return (
    <section className="landing">
      <div className="landing-copy">
        <p className="eyebrow">Zachary HOUNSA-HOUNKPA EI</p>
        <h1>Vos services et demandes réunis dans un espace client clair.</h1>
        <p className="lead">
          Consultez les services, informations de facturation et demandes
          d&apos;assistance rattachés à votre compte client.
        </p>
        <div className="actions">
          <Link className="button" href="/login">
            Se connecter
          </Link>
        </div>
        <p className="landing-note">
          Espace authentifié avec administration interne en lecture seule.
          Aucune action Active Directory réelle, facturation réelle ni paiement.
        </p>
      </div>

      <aside className="landing-preview" aria-label="Aperçu de l'espace client">
        <div className="preview-header">
          <div>
            <span className="preview-kicker">Aperçu illustratif</span>
            <strong>Exemple d&apos;espace client</strong>
          </div>
          <span className="status-badge status-success">
            Session serveur requise
          </span>
        </div>
        <div className="preview-metrics">
          <div>
            <span>Services actifs</span>
            <strong>3</strong>
          </div>
          <div>
            <span>Demandes ouvertes</span>
            <strong>2</strong>
          </div>
          <div>
            <span>Document informatif</span>
            <strong>1</strong>
          </div>
        </div>
        <div className="preview-list">
          <div>
            <span className="preview-icon">HDP</span>
            <p>
              <strong>Hébergement dossier personnel</strong>
              <small>Selon devis</small>
            </p>
          </div>
          <div>
            <span className="preview-icon">SAV</span>
            <p>
              <strong>Sauvegarde dossier personnel</strong>
              <small>Inclus selon périmètre</small>
            </p>
          </div>
        </div>
      </aside>
    </section>
  );
}
