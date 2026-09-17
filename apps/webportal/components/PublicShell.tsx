"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useEffect, useRef, useState } from "react";
import { ChevronDown, Menu, X } from "lucide-react";

import { BrandLogo } from "@/components/BrandLogo";
import { SERVICE_CATEGORIES } from "@/lib/public-services";

const publicHref = (pathname: string) => pathname;
const primaryLinks = [
  { href: publicHref("/offres"), label: "Offres" },
  { href: publicHref("/tarifs"), label: "Tarifs" },
  { href: publicHref("/diagnostic"), label: "Diagnostic" },
  { href: publicHref("/a-propos"), label: "À propos" },
] as const;

type PublicFooterLink = {
  href: string;
  label: string;
};

const footerServiceLinks: readonly PublicFooterLink[] = [
  { href: publicHref("/services"), label: "Tous les services" },
  { href: publicHref("/services/support-it"), label: "Assistance & maintenance" },
  { href: publicHref("/services/reseau-securite"), label: "Réseau & sécurité" },
  { href: publicHref("/services/cloud-hebergement"), label: "Hébergement & services en ligne" },
  { href: publicHref("/services/domaines-messagerie"), label: "Domaines & messagerie" },
];

const footerDiscoverLinks: readonly PublicFooterLink[] = [
  { href: publicHref("/offres"), label: "Offres" },
  { href: publicHref("/tarifs"), label: "Tarifs" },
  { href: publicHref("/diagnostic"), label: "Diagnostic" },
  { href: publicHref("/a-propos"), label: "À propos" },
  { href: publicHref("/infrastructure"), label: "Infrastructure" },
];

const footerLegalLinks: readonly PublicFooterLink[] = [
  { href: publicHref("/mentions-legales"), label: "Mentions légales" },
  { href: publicHref("/politique-confidentialite"), label: "Politique de confidentialité" },
  { href: publicHref("/cgv"), label: "CGV" },
];

type PublicShellProps = {
  children: ReactNode;
  signupEnabled: boolean;
};

function ServicesMegaMenu({ onNavigate }: { onNavigate?: () => void }) {
  const [open, setOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    function closeOnOutsidePointer(event: PointerEvent) {
      if (!menuRef.current?.contains(event.target as Node)) setOpen(false);
    }
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key !== "Escape" || !open) return;
      setOpen(false);
      triggerRef.current?.focus();
    }
    document.addEventListener("pointerdown", closeOnOutsidePointer);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("pointerdown", closeOnOutsidePointer);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [open]);

  function close() {
    setOpen(false);
    onNavigate?.();
  }

  return (
    <div className="public-services-menu" ref={menuRef}>
      <button aria-controls="public-services-mega-menu" aria-expanded={open} className="public-services-trigger" onClick={() => setOpen((current) => !current)} ref={triggerRef} type="button">
        Services <ChevronDown aria-hidden="true" size={16} strokeWidth={1.9} />
      </button>
      <div aria-label="Services Zachary IT" className={open ? "public-services-mega-menu public-services-mega-menu-open" : "public-services-mega-menu"} hidden={!open} id="public-services-mega-menu" role="group">
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
          <a href={publicHref("/services/vpn-entreprise")} onClick={close}><strong>VPN / accès sécurisé</strong><span>Travailler à distance en sécurité</span></a>
          <a href={publicHref("/services/hebergement-web")} onClick={close}><strong>Hébergement web</strong><span>Site ou application suivis</span></a>
        </div>
        <a className="public-services-mega-all" href={publicHref("/services")} onClick={close}>Voir tous les services</a>
      </div>
    </div>
  );
}

function FooterLinkList({ links }: { links: readonly PublicFooterLink[] }) {
  return (
    <ul className="public-footer-link-list">
      {links.map((link) => (
        <li key={link.href}>
          <a href={link.href}>{link.label}</a>
        </li>
      ))}
    </ul>
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
        <a href={publicHref("/services/vpn-entreprise")} onClick={onNavigate}><strong>VPN / accès sécurisé</strong><span>Travailler à distance en sécurité</span></a>
        <a href={publicHref("/services/hebergement-web")} onClick={onNavigate}><strong>Hébergement web</strong><span>Site ou application suivis</span></a>
        <a className="public-mobile-services-all" href={publicHref("/services")} onClick={onNavigate}>Voir tous les services</a>
      </div>
    </details>
  );
}

export function PublicShell({
  children,
  signupEnabled,
}: PublicShellProps) {
  const [menuOpen, setMenuOpen] = useState(false);
  const menuToggleRef = useRef<HTMLButtonElement>(null);
  const closeMobileMenu = () => setMenuOpen(false);
  const currentYear = new Date().getFullYear();

  useEffect(() => {
    function closeMobileMenuOnEscape(event: KeyboardEvent) {
      if (event.key !== "Escape" || !menuOpen) return;
      setMenuOpen(false);
      menuToggleRef.current?.focus();
    }

    document.addEventListener("keydown", closeMobileMenuOnEscape);
    return () => document.removeEventListener("keydown", closeMobileMenuOnEscape);
  }, [menuOpen]);

  return (
    <>
      <a className="skip-link" href="#main-content">Aller au contenu</a>
      <header className="public-header">
        <div className="public-header-inner">
          <a className="brand brand-public" href={publicHref("/")}><BrandLogo className="brand-logo brand-logo-public" priority /></a>
          <button aria-controls="public-header-nav" aria-expanded={menuOpen} aria-label={menuOpen ? "Fermer le menu" : "Ouvrir le menu"} className="public-menu-toggle" onClick={() => setMenuOpen((current) => !current)} ref={menuToggleRef} type="button">
            {menuOpen ? <X aria-hidden="true" size={20} strokeWidth={1.75} /> : <Menu aria-hidden="true" size={20} strokeWidth={1.75} />}
          </button>
          <nav aria-label="Navigation principale" className={menuOpen ? "public-header-nav public-header-nav-open" : "public-header-nav"} id="public-header-nav">
            <div className="public-header-links">
              <div className="public-services-menu-desktop"><ServicesMegaMenu /></div>
              <div className="public-services-menu-mobile"><MobileServicesMenu onNavigate={closeMobileMenu} /></div>
              {primaryLinks.map((link) => <a href={link.href} key={link.href} onClick={closeMobileMenu}>{link.label}</a>)}
            </div>
            <div className="public-header-actions">
              <Link className="public-header-login" href="/login" onClick={closeMobileMenu}>Espace client</Link>
              <a className="public-header-primary" href={publicHref("/contact")} onClick={closeMobileMenu}>Nous contacter</a>
            </div>
          </nav>
        </div>
      </header>
      <main className="public-main" id="main-content">{children}</main>
      <footer className="public-footer">
        <div className="public-footer-inner">
          <div className="public-footer-grid">
            <div className="public-footer-brand">
              <BrandLogo className="brand-logo brand-logo-footer" variant="dark" />
              <p className="public-footer-tagline">Des services informatiques clairs, suivis et adaptés à vos besoins.</p>
              <p className="public-footer-company">Zachary HOUNSA-HOUNKPA EI</p>
              <a className="public-footer-contact" href={publicHref("/contact")}>Nous contacter</a>
            </div>

            <section className="public-footer-column" aria-labelledby="public-footer-services-title">
              <h2 id="public-footer-services-title">Services</h2>
              <nav aria-label="Services Zachary IT">
                <FooterLinkList links={footerServiceLinks} />
              </nav>
            </section>

            <section className="public-footer-column" aria-labelledby="public-footer-discover-title">
              <h2 id="public-footer-discover-title">Découvrir</h2>
              <nav aria-label="Découvrir Zachary IT">
                <FooterLinkList links={footerDiscoverLinks} />
              </nav>
            </section>

            <section className="public-footer-column public-footer-help" aria-labelledby="public-footer-help-title">
              <h2 id="public-footer-help-title">Aide & espace client</h2>
              <nav aria-label="Aide et espace client">
                <ul className="public-footer-link-list">
                  <li><a href={publicHref("/contact")}>Nous contacter</a></li>
                  <li><Link href="/login">Espace client</Link></li>
                  {signupEnabled ? <li><a href={publicHref("/signup")}>Créer un accès client</a></li> : null}
                  <li><a href={publicHref("/ressources")}>Ressources</a></li>
                  <li><a href={publicHref("/wiki")}>Wiki</a></li>
                </ul>
              </nav>
              <div className="public-footer-legal">
                <h3>Informations légales</h3>
                <nav aria-label="Informations légales">
                  <FooterLinkList links={footerLegalLinks} />
                </nav>
              </div>
            </section>
          </div>
          <div className="public-footer-bottom">
            <p>© <time dateTime={String(currentYear)}>{currentYear}</time> Zachary HOUNSA-HOUNKPA EI</p>
          </div>
        </div>
      </footer>
    </>
  );
}
