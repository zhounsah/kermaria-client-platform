"use client";

import type { ReactNode } from "react";
import { useState } from "react";

import { PORTFOLIO_URL, PUBLIC_SITE_URL } from "@/lib/public-route-config";
import appPackage from "../../../package.json";

const CLIENT_PORTAL_LOGIN_URL = "https://dashboard.zacharyhounsa.ovh/login";
const APP_VERSION_LABEL = `Version v${appPackage.displayVersion ?? appPackage.version}`;
const publicHref = (pathname: string) => `${PUBLIC_SITE_URL}${pathname}`;
const publicLinks = [
  { href: publicHref("/offres"), label: "Offres" },
  { href: publicHref("/diagnostic"), label: "Diagnostic" },
  { href: publicHref("/ressources"), label: "Ressources" },
  { href: PORTFOLIO_URL, label: "Portfolio" },
  { href: publicHref("/a-propos"), label: "À propos" },
  { href: publicHref("/contact"), label: "Contact" },
  { href: publicHref("/wiki"), label: "Wiki" },
] as const;

type PublicShellProps = {
  children: ReactNode;
  signupEnabled: boolean;
};

export function PublicShell({ children, signupEnabled }: PublicShellProps) {
  const [menuOpen, setMenuOpen] = useState(false);

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
              <strong>Zachary IT</strong>
              <small>Sauvegarde et continuité à Guichen</small>
            </span>
          </a>
          <button
            aria-controls="public-header-nav"
            aria-expanded={menuOpen}
            aria-label={menuOpen ? "Fermer le menu" : "Ouvrir le menu"}
            className="public-menu-toggle"
            onClick={() => setMenuOpen((current) => !current)}
            type="button"
          >
            <span aria-hidden="true" />
            <span aria-hidden="true" />
            <span aria-hidden="true" />
          </button>
          <nav
            aria-label="Navigation principale"
            className={menuOpen
              ? "public-header-nav public-header-nav-open"
              : "public-header-nav"}
            id="public-header-nav"
          >
            <div className="public-header-links">
              {publicLinks.map((link) => (
                <a href={link.href} key={link.href}>
                  {link.label}
                </a>
              ))}
            </div>
            <div className="public-header-actions">
              <a
                className="public-header-primary"
                href={publicHref("/contact")}
              >
                Expliquer mon besoin
              </a>
              <a
                className="public-header-login"
                href={CLIENT_PORTAL_LOGIN_URL}
              >
                Connexion
              </a>
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
            {/*
              Marque en tete, identite juridique juste dessous : le pied de
              page est le seul endroit present sur toutes les pages publiques
              ou les deux noms se lisent ensemble.
            */}
            <strong>Zachary IT</strong>
            <p>Zachary HOUNSA-HOUNKPA EI</p>
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
            <a href={publicHref("/ressources")}>Ressources</a>
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
