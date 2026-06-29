import Link from "next/link";
import type { ReactNode } from "react";

import {
  PORTFOLIO_URL,
  isSignupEnabled,
} from "@/lib/public-routes";

type PublicShellProps = {
  children: ReactNode;
};

export function PublicShell({ children }: PublicShellProps) {
  const signupEnabled = isSignupEnabled();

  return (
    <>
      <header className="public-header">
        <div className="public-header-inner">
          <Link className="brand brand-public" href="/">
            <span className="brand-mark" aria-hidden="true">
              ZH
            </span>
            <span className="brand-copy">
              <strong>Zachary HOUNSA-HOUNKPA EI</strong>
              <small>Espace client professionnel</small>
            </span>
          </Link>
          <nav className="public-header-nav" aria-label="Navigation principale">
            <Link href="/offres">Offres</Link>
            <a href={PORTFOLIO_URL}>Portfolio</a>
            <Link href="/a-propos">À propos</Link>
            <Link href="/contact">Contact</Link>
            <Link className="public-header-login" href="/login">
              Connexion
            </Link>
            {signupEnabled ? (
              <Link className="public-header-signup" href="/signup">
                Inscription
              </Link>
            ) : null}
          </nav>
        </div>
      </header>
      <main className="public-main">{children}</main>
      <footer className="public-footer">
        <div className="public-footer-inner">
          <div className="public-footer-brand">
            <strong>Zachary HOUNSA-HOUNKPA EI</strong>
            <p>Espace client professionnel.</p>
          </div>
          <nav className="public-footer-nav" aria-label="Liens légaux">
            <Link href="/mentions-legales">Mentions légales</Link>
            <Link href="/politique-confidentialite">
              Politique de confidentialité
            </Link>
            <Link href="/cgv">CGV</Link>
            <Link href="/login">Connexion</Link>
            {signupEnabled ? <Link href="/signup">Inscription</Link> : null}
          </nav>
        </div>
      </footer>
    </>
  );
}
