"use client";

import type { ReactNode } from "react";
import { useEffect, useRef, useState } from "react";
import { ChevronDown, Menu, X } from "lucide-react";

import { BrandLogo } from "@/components/BrandLogo";
import { SERVICE_CATEGORIES } from "@/lib/public-services";
import { PUBLIC_SITE_URL } from "@/lib/public-route-config";
import appPackage from "../../../package.json";

const CLIENT_PORTAL_LOGIN_URL = "https://dashboard.zachary-it.fr/login";
const APP_VERSION_LABEL = `Version v${appPackage.displayVersion ?? appPackage.version}`;
const publicHref = (pathname: string) => `${PUBLIC_SITE_URL}${pathname}`;
const primaryLinks = [
  { href: publicHref("/services/support-it#infogerance"), label: "Infogérance" },
  { href: publicHref("/services/cloud-hebergement"), label: "Cloud & Hébergement" },
  { href: publicHref("/tarifs"), label: "Tarifs" },
  { href: publicHref("/formules"), label: "Formules" },
  { href: publicHref("/a-propos"), label: "À propos" },
] as const;

type PublicShellProps = { children: ReactNode; signupEnabled: boolean };

function ServicesMegaMenu({ onNavigate }: { onNavigate?: () => void }) {
  const [open, setOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function closeOnOutsidePointer(event: PointerEvent) {
      if (!menuRef.current?.contains(event.target as Node)) setOpen(false);
    }
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === "Escape") setOpen(false);
    }
    document.addEventListener("pointerdown", closeOnOutsidePointer);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("pointerdown", closeOnOutsidePointer);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, []);

  function close() {
    setOpen(false);
    onNavigate?.();
  }

  return (
    <div className="public-services-menu" ref={menuRef}>
      <button aria-controls="public-services-mega-menu" aria-expanded={open} className="public-services-trigger" onClick={() => setOpen((current) => !current)} type="button">
        Services <ChevronDown aria-hidden="true" size={16} strokeWidth={1.9} />
      </button>
      <div aria-label="Services Zachary IT" className={open ? "public-services-mega-menu public-services-mega-menu-open" : "public-services-mega-menu"} id="public-services-mega-menu">
        <div className="public-services-mega-grid">
          {SERVICE_CATEGORIES.map((category) => (
            <a href={publicHref(`/services/${category.slug}`)} key={category.slug} onClick={close}>
              <strong>{category.shortTitle}</strong>
              <span>{category.menuSummary}</span>
            </a>
          ))}
        </div>
        <div className="public-services-mega-grid public-services-mega-featured">
          <a href={publicHref("/services/vps")} onClick={close}><strong>VPS & infogérance</strong><span>Serveurs gérés ou repris</span></a>
          <a href={publicHref("/services/vpn-entreprise")} onClick={close}><strong>VPN entreprise</strong><span>Accès distant sécurisé</span></a>
          <a href={publicHref("/services/messagerie-professionnelle")} onClick={close}><strong>Messagerie professionnelle</strong><span>Boîtes, migration et DNS</span></a>
        </div>
        <a className="public-services-mega-all" href={publicHref("/services")} onClick={close}>Voir tous les services</a>
      </div>
    </div>
  );
}

function MobileServicesMenu({ onNavigate }: { onNavigate: () => void }) {
  return (
    <details className="public-mobile-services-menu">
      <summary>Services <ChevronDown aria-hidden="true" size={18} strokeWidth={1.9} /></summary>
      <div>
        {SERVICE_CATEGORIES.map((category) => (
          <a href={publicHref(`/services/${category.slug}`)} key={category.slug} onClick={onNavigate}>
            <strong>{category.shortTitle}</strong><span>{category.menuSummary}</span>
          </a>
        ))}
        <a href={publicHref("/services/vps")} onClick={onNavigate}><strong>VPS & infogérance</strong><span>Serveurs gérés ou repris</span></a>
        <a href={publicHref("/services/vpn-entreprise")} onClick={onNavigate}><strong>VPN entreprise</strong><span>Accès distant sécurisé</span></a>
        <a href={publicHref("/services/messagerie-professionnelle")} onClick={onNavigate}><strong>Messagerie professionnelle</strong><span>Boîtes, migration et DNS</span></a>
        <a className="public-mobile-services-all" href={publicHref("/services")} onClick={onNavigate}>Voir tous les services</a>
      </div>
    </details>
  );
}

export function PublicShell({ children, signupEnabled }: PublicShellProps) {
  const [menuOpen, setMenuOpen] = useState(false);
  const closeMobileMenu = () => setMenuOpen(false);

  return (
    <>
      <a className="skip-link" href="#main-content">Aller au contenu</a>
      <header className="public-header">
        <div className="public-header-inner">
          <a className="brand brand-public" href={publicHref("/")}><BrandLogo className="brand-logo brand-logo-public" priority /></a>
          <button aria-controls="public-header-nav" aria-expanded={menuOpen} aria-label={menuOpen ? "Fermer le menu" : "Ouvrir le menu"} className="public-menu-toggle" onClick={() => setMenuOpen((current) => !current)} type="button">
            {menuOpen ? <X aria-hidden="true" size={20} strokeWidth={1.75} /> : <Menu aria-hidden="true" size={20} strokeWidth={1.75} />}
          </button>
          <nav aria-label="Navigation principale" className={menuOpen ? "public-header-nav public-header-nav-open" : "public-header-nav"} id="public-header-nav">
            <div className="public-header-links">
              <div className="public-services-menu-desktop"><ServicesMegaMenu /></div>
              <div className="public-services-menu-mobile"><MobileServicesMenu onNavigate={closeMobileMenu} /></div>
              {primaryLinks.map((link) => <a href={link.href} key={link.href} onClick={closeMobileMenu}>{link.label}</a>)}
            </div>
            <div className="public-header-actions">
              <a className="public-header-login" href={CLIENT_PORTAL_LOGIN_URL} onClick={closeMobileMenu}>Espace client</a>
              <a className="public-header-primary" href={publicHref("/contact")} onClick={closeMobileMenu}>Demander un audit</a>
            </div>
          </nav>
        </div>
      </header>
      <main className="public-main" id="main-content">{children}</main>
      <footer className="public-footer">
        <div className="public-footer-inner">
          <div className="public-footer-brand">
            <BrandLogo className="brand-logo brand-logo-footer" variant="dark" />
            <p>Zachary HOUNSA-HOUNKPA EI</p><p>Services informatiques, formules et espace client.</p><p>{APP_VERSION_LABEL}</p>
          </div>
          <nav className="public-footer-nav" aria-label="Liens légaux et navigation">
            <a href={publicHref("/services")}>Services</a><a href={publicHref("/services/support-it")}>Infogérance</a><a href={publicHref("/tarifs")}>Tarifs</a><a href={publicHref("/formules")}>Formules</a><a href={publicHref("/a-propos")}>À propos</a><a href={publicHref("/infrastructure")}>Infrastructure</a><a href={publicHref("/ressources")}>Ressources</a><a href={publicHref("/wiki")}>Wiki</a><a href={publicHref("/diagnostic")}>Diagnostic</a><a href={publicHref("/contact")}>Contact</a><a href={publicHref("/mentions-legales")}>Mentions légales</a><a href={publicHref("/politique-confidentialite")}>Politique de confidentialité</a><a href={publicHref("/cgv")}>CGV</a><a href={CLIENT_PORTAL_LOGIN_URL}>Espace client</a>
            {signupEnabled ? <a href={publicHref("/signup")}>Inscription</a> : null}
          </nav>
        </div>
      </footer>
    </>
  );
}
