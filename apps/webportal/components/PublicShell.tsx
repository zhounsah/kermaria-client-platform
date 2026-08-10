"use client";

import type { ReactNode } from "react";

import { PORTFOLIO_URL, PUBLIC_SITE_URL } from "@/lib/public-route-config";
import appPackage from "../../../package.json";

const CLIENT_PORTAL_LOGIN_URL = "https://dashboard.zacharyhounsa.ovh/login";
const APP_VERSION_LABEL = `Version v${appPackage.version}`;
const publicHref = (pathname: string) => `${PUBLIC_SITE_URL}${pathname}`;

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
          <a className="brand brand-public" href={publicHref("/")}>
            <span className="brand-mark" aria-hidden="true">
              ZH
            </span>
            <span className="brand-copy">
              <strong>Zachary HOUNSA-HOUNKPA EI</strong>
              <small>Services informatiques et espace client</small>
            </span>
          </a>
          <nav className="public-header-nav" aria-label="Navigation principale">
            <div className="public-header-links">
              <a href={publicHref("/offres")}>Offres</a>
              <a href={publicHref("/diagnostic")}>Diagnostic</a>
              <a href={publicHref("/solutions")}>Services</a>
              <a href={PORTFOLIO_URL}>Portfolio</a>
              <a href={publicHref("/a-propos")}>À propos</a>
              <a href={publicHref("/contact")}>Contact</a>
              <a href={publicHref("/wiki")}>Wiki</a>
            </div>
            <div className="public-header-actions">
              <a
                className="public-header-login"
                href={CLIENT_PORTAL_LOGIN_URL}
              >
                Connexion
              </a>
              {signupEnabled ? (
                <a className="public-header-signup" href={publicHref("/signup")}>
                  Inscription
                </a>
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
            <p>Site public, offres et espace client.</p>
            <p>{APP_VERSION_LABEL}</p>
          </div>
          <nav className="public-footer-nav" aria-label="Liens légaux">
            <a href={publicHref("/mentions-legales")}>Mentions légales</a>
            <a href={publicHref("/politique-confidentialite")}>
              Politique de confidentialité
            </a>
            <a href={publicHref("/cgv")}>CGV</a>
            <a href={publicHref("/offres")}>Offres</a>
            <a href={publicHref("/solutions")}>Services</a>
            <a href={publicHref("/wiki")}>Wiki</a>
            <a href={publicHref("/diagnostic")}>Diagnostic</a>
            <a href={CLIENT_PORTAL_LOGIN_URL}>Connexion</a>
            {signupEnabled ? <a href={publicHref("/signup")}>Inscription</a> : null}
          </nav>
        </div>
      </footer>
    </>
  );
}
