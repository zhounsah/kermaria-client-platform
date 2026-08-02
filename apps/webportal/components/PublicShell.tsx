"use client";

import Link from "next/link";
import type { ReactNode } from "react";

import { PORTFOLIO_URL } from "@/lib/public-route-config";
import appPackage from "../../../package.json";

const CLIENT_PORTAL_LOGIN_URL = "https://dashboard.zacharyhounsa.ovh/login";
const APP_VERSION_LABEL = `Version v${appPackage.version}`;

type PublicShellProps = {
  children: ReactNode;
  signupEnabled: boolean;
};

export function PublicShell({ children, signupEnabled }: PublicShellProps) {
  return (
    <>
      <a className="skip-link" href="#main-content">
        Aller au contenu
      </a>
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
            <div className="public-header-links">
              <Link href="/offres">Offres</Link>
              <a href={PORTFOLIO_URL}>Portfolio</a>
              <Link href="/a-propos">À propos</Link>
              <Link href="/contact">Contact</Link>
            </div>
            <div className="public-header-actions">
              <a
                className="public-header-login"
                href={CLIENT_PORTAL_LOGIN_URL}
              >
                Connexion
              </a>
              {signupEnabled ? (
                <Link className="public-header-signup" href="/signup">
                  Inscription
                </Link>
              ) : null}
            </div>
          </nav>
        </div>
      </header>
      <main className="public-main" id="main-content">
        {children}
      </main>
      <footer className="public-footer">
        <div className="public-footer-inner">
          <div className="public-footer-brand">
            <strong>Zachary HOUNSA-HOUNKPA EI</strong>
            <p>Site public, offres et espace client professionnel.</p>
            <p>{APP_VERSION_LABEL}</p>
          </div>
          <nav className="public-footer-nav" aria-label="Liens légaux">
            <Link href="/mentions-legales">Mentions légales</Link>
            <Link href="/politique-confidentialite">
              Politique de confidentialité
            </Link>
            <Link href="/cgv">CGV</Link>
            <a href={CLIENT_PORTAL_LOGIN_URL}>Connexion</a>
            {signupEnabled ? <Link href="/signup">Inscription</Link> : null}
          </nav>
        </div>
      </footer>
    </>
  );
}
