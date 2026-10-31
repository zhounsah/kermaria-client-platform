"use client";

import Link, { useLinkStatus } from "next/link";
import type { ReactNode } from "react";
import { Fragment, useEffect, useRef, useState, useSyncExternalStore } from "react";
import { ChevronDown, Menu, Pause, Play, X } from "lucide-react";
import { usePathname } from "next/navigation";

import { BrandLogo } from "@/components/BrandLogo";
import { BillingV2CartHeaderLink } from "@/components/BillingV2CartHeaderLink";
import { SitePageFrame } from "@/components/SitePageFrame";
import { SERVICE_CATEGORIES } from "@/lib/public-services";

const publicHref = (pathname: string) => pathname;
const MOTION_PREFERENCE_KEY = "zachary-public-motion";
const MOTION_PREFERENCE_EVENT = "zachary-public-motion-change";
let fallbackMotionPaused = false;

function readMotionPreference() {
  try { return window.sessionStorage.getItem(MOTION_PREFERENCE_KEY) === "paused"; }
  catch { return fallbackMotionPaused; }
}

function subscribeMotionPreference(onChange: () => void) {
  window.addEventListener("storage", onChange);
  window.addEventListener(MOTION_PREFERENCE_EVENT, onChange);
  return () => {
    window.removeEventListener("storage", onChange);
    window.removeEventListener(MOTION_PREFERENCE_EVENT, onChange);
  };
}
const PUBLIC_VIEW_TRANSITIONS = `
@media (prefers-reduced-motion: no-preference) {
  @view-transition { navigation: auto; }
  .public-header { view-transition-name: public-header; }
  ::view-transition-old(public-header), ::view-transition-new(public-header) { animation: none; }
  ::view-transition-old(root) { animation: .28s ease-out both public-route-out; }
  ::view-transition-new(root) { animation: .58s cubic-bezier(.22, 1, .36, 1) both public-route-wipe; }
}
@keyframes public-route-out { to { transform: translateX(-4vw) scale(.985); } }
@keyframes public-route-wipe { from { clip-path: inset(0 100% 0 0); transform: translateX(6vw); } to { clip-path: inset(0 0 0 0); transform: translateX(0); } }
`;
const primaryLinks = [
  { href: publicHref("/offres"), label: "Offres" },
  { href: publicHref("/tarifs"), label: "Tarifs" },
  { href: publicHref("/a-propos"), label: "À propos" },
] as const;

type PublicFooterLink = {
  href: string;
  label: string;
};

const footerServiceLinks: readonly PublicFooterLink[] = [
  { href: publicHref("/services"), label: "Tous les services" },
  { href: publicHref("/services/support-it"), label: "Assistance & maintenance" },
  { href: publicHref("/assistance-maintenance"), label: "Abonnements assistance" },
  { href: publicHref("/services/reseau-securite"), label: "Réseau & sécurité" },
  { href: publicHref("/services/cloud-hebergement"), label: "Hébergement & services en ligne" },
  { href: publicHref("/services/domaines-messagerie"), label: "Domaines & messagerie" },
];

const footerDiscoverLinks: readonly PublicFooterLink[] = [
  { href: publicHref("/offres"), label: "Offres" },
  { href: publicHref("/tarifs"), label: "Tarifs" },
  { href: publicHref("/diagnostic"), label: "Trouver ma solution" },
  { href: publicHref("/a-propos"), label: "À propos" },
  { href: publicHref("/infrastructure"), label: "Notre fonctionnement" },
];

const footerLegalLinks: readonly PublicFooterLink[] = [
  { href: publicHref("/mentions-legales"), label: "Mentions légales" },
  { href: publicHref("/politique-confidentialite"), label: "Politique de confidentialité" },
  { href: publicHref("/demander-mes-donnees"), label: "Demander mes données" },
  { href: publicHref("/cgv"), label: "CGV" },
];

type PublicShellProps = {
  children: ReactNode;
  signupEnabled: boolean;
};

function ServicesMegaMenu({ onNavigate, active }: { onNavigate?: () => void; active: boolean }) {
  const pathname = usePathname();
  const [openState, setOpenState] = useState({ pathname, open: false });
  const open = openState.pathname === pathname && openState.open;
  const menuRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    function closeOnOutsidePointer(event: PointerEvent) {
      if (!menuRef.current?.contains(event.target as Node)) setOpenState({ pathname, open: false });
    }
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key !== "Escape" || !open) return;
      setOpenState({ pathname, open: false });
      triggerRef.current?.focus();
    }
    document.addEventListener("pointerdown", closeOnOutsidePointer);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("pointerdown", closeOnOutsidePointer);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [open, pathname]);

  function close() {
    setOpenState({ pathname, open: false });
    onNavigate?.();
  }

  return (
    <div className="public-services-menu" ref={menuRef}>
      <button aria-controls="public-services-mega-menu" aria-expanded={open} className="public-services-trigger" data-active={active || undefined} onClick={() => setOpenState({ pathname, open: !open })} ref={triggerRef} type="button">
        Services <ChevronDown aria-hidden="true" size={16} strokeWidth={1.9} />
      </button>
      <div aria-label="Services Zachary IT" className={open ? "public-services-mega-menu public-services-mega-menu-open" : "public-services-mega-menu"} hidden={!open} id="public-services-mega-menu" role="group">
        <div className="public-services-mega-grid">
          {SERVICE_CATEGORIES.map((category) => (
            <Link href={publicHref(`/services/${category.slug}`)} key={category.slug} onClick={close}>
              <strong>{category.shortTitle}</strong>
              <span>{category.menuSummary}</span>
            </Link>
          ))}
        </div>
        <div className="public-services-mega-grid public-services-mega-featured">
          <Link href={publicHref("/services/vps")} onClick={close}><strong>Serveur à distance</strong><span>Une solution suivie pour vos outils</span></Link>
          <Link href={publicHref("/services/vpn-entreprise")} onClick={close}><strong>Accès à distance</strong><span>Retrouver vos outils en sécurité</span></Link>
          <Link href={publicHref("/services/hebergement-web")} onClick={close}><strong>Site web</strong><span>Mettre votre site en ligne et le suivre</span></Link>
        </div>
        <Link className="public-services-mega-all" href={publicHref("/assistance-maintenance")} onClick={close}>Abonnements assistance et maintenance</Link>
        <Link className="public-services-mega-all" href={publicHref("/services")} onClick={close}>Voir tous les services</Link>
      </div>
    </div>
  );
}

function FooterLinkList({ links }: { links: readonly PublicFooterLink[] }) {
  return (
    <ul className="public-footer-link-list">
      {links.map((link) => (
        <li key={link.href}>
          <Link href={link.href}>{link.label}</Link>
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
          <Link href={publicHref(`/services/${category.slug}`)} key={category.slug} onClick={onNavigate}>
            <strong>{category.shortTitle}</strong><span>{category.menuSummary}</span>
          </Link>
        ))}
        <Link href={publicHref("/services/vps")} onClick={onNavigate}><strong>Serveur à distance</strong><span>Une solution suivie pour vos outils</span></Link>
        <Link href={publicHref("/assistance-maintenance")} onClick={onNavigate}><strong>Abonnements assistance</strong><span>Particuliers et professionnels</span></Link>
        <Link href={publicHref("/services/vpn-entreprise")} onClick={onNavigate}><strong>Accès à distance</strong><span>Retrouver vos outils en sécurité</span></Link>
        <Link href={publicHref("/services/hebergement-web")} onClick={onNavigate}><strong>Site web</strong><span>Mettre votre site en ligne et le suivre</span></Link>
        <Link className="public-mobile-services-all" href={publicHref("/services")} onClick={onNavigate}>Voir tous les services</Link>
      </div>
    </details>
  );
}

function NavPendingMark() {
  const { pending } = useLinkStatus();
  return <span aria-hidden="true" className={pending ? "public-nav-pending public-nav-pending-active" : "public-nav-pending"} />;
}

export function PublicShell({
  children,
  signupEnabled,
}: PublicShellProps) {
  const pathname = usePathname();
  const [menuState, setMenuState] = useState({ pathname, open: false });
  const menuOpen = menuState.pathname === pathname && menuState.open;
  const motionPaused = useSyncExternalStore(subscribeMotionPreference, readMotionPreference, () => false);
  const menuToggleRef = useRef<HTMLButtonElement>(null);
  const mainRef = useRef<HTMLElement>(null);
  const previousPathRef = useRef(pathname);
  const routeClickRef = useRef(false);
  const closeMobileMenu = () => setMenuState({ pathname, open: false });
  const currentYear = new Date().getFullYear();

  useEffect(() => {
    function markRouteClick(event: MouseEvent) {
      if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || !(event.target instanceof Element)) return;
      const anchor = event.target.closest<HTMLAnchorElement>("a[href]");
      if (!anchor || (anchor.target && anchor.target !== "_self")) return;
      const target = new URL(anchor.href, window.location.href);
      if (target.origin === window.location.origin && target.pathname !== window.location.pathname) routeClickRef.current = true;
    }
    document.addEventListener("click", markRouteClick, true);
    return () => document.removeEventListener("click", markRouteClick, true);
  }, []);

  useEffect(() => {
    if (previousPathRef.current === pathname) return;
    previousPathRef.current = pathname;
    const resetScroll = routeClickRef.current;
    routeClickRef.current = false;
    const frame = window.requestAnimationFrame(() => {
      mainRef.current?.focus({ preventScroll: true });
      if (resetScroll) window.scrollTo({ top: 0, behavior: "instant" });
    });
    return () => window.cancelAnimationFrame(frame);
  }, [pathname]);

  function toggleMotion() {
    const next = !motionPaused;
    fallbackMotionPaused = next;
    try { window.sessionStorage.setItem(MOTION_PREFERENCE_KEY, next ? "paused" : "running"); } catch { /* La préférence reste active pour cette page. */ }
    window.dispatchEvent(new Event(MOTION_PREFERENCE_EVENT));
  }

  useEffect(() => {
    function closeMobileMenuOnEscape(event: KeyboardEvent) {
      if (event.key !== "Escape" || !menuOpen) return;
      setMenuState({ pathname, open: false });
      menuToggleRef.current?.focus();
    }

    document.addEventListener("keydown", closeMobileMenuOnEscape);
    return () => document.removeEventListener("keydown", closeMobileMenuOnEscape);
  }, [menuOpen, pathname]);

  return (
    <>
      {motionPaused ? null : <style>{PUBLIC_VIEW_TRANSITIONS}</style>}
      <a className="skip-link" href="#main-content">Aller au contenu</a>
      <header className={motionPaused ? "public-header public-motion-paused" : "public-header"}>
        <div className="public-header-inner">
          <Link className="brand brand-public" href={publicHref("/")}><BrandLogo className="brand-logo brand-logo-public" priority /></Link>
          <button aria-controls="public-header-nav" aria-expanded={menuOpen} aria-label={menuOpen ? "Fermer le menu" : "Ouvrir le menu"} className="public-menu-toggle" onClick={() => setMenuState({ pathname, open: !menuOpen })} ref={menuToggleRef} type="button">
            {menuOpen ? <X aria-hidden="true" size={20} strokeWidth={1.75} /> : <Menu aria-hidden="true" size={20} strokeWidth={1.75} />}
          </button>
          <nav aria-label="Navigation principale" className={menuOpen ? "public-header-nav public-header-nav-open" : "public-header-nav"} id="public-header-nav">
            <div className="public-header-links">
              <div className="public-services-menu-desktop"><ServicesMegaMenu active={pathname.startsWith("/services") || pathname.startsWith("/assistance-maintenance")} /></div>
              <div className="public-services-menu-mobile"><MobileServicesMenu key={pathname} onNavigate={closeMobileMenu} /></div>
              {primaryLinks.map((link) => <Link aria-current={pathname === link.href ? "page" : undefined} href={link.href} key={link.href} onClick={closeMobileMenu}>{link.label}<NavPendingMark /></Link>)}
            </div>
            <div className="public-header-actions">
              <BillingV2CartHeaderLink onNavigate={closeMobileMenu} />
              <Link className="public-header-login" href="/login" onClick={closeMobileMenu}>Espace client</Link>
              <button aria-label={motionPaused ? "Relancer les animations" : "Mettre les animations en pause"} aria-pressed={motionPaused} className="public-motion-toggle" onClick={toggleMotion} title={motionPaused ? "Relancer les animations" : "Mettre les animations en pause"} type="button">
                {motionPaused ? <Play aria-hidden="true" size={18} /> : <Pause aria-hidden="true" size={18} />}
              </button>
              <Link aria-current={pathname === "/contact" ? "page" : undefined} className="public-header-primary" href={publicHref("/contact")} onClick={closeMobileMenu}>Contact<NavPendingMark /></Link>
            </div>
          </nav>
        </div>
      </header>
      <main aria-label="Contenu principal" className={motionPaused ? "public-main public-motion-paused" : "public-main"} id="main-content" ref={mainRef} tabIndex={-1}><Fragment key={pathname}>{children}</Fragment></main>
      <footer className={motionPaused ? "public-footer public-motion-paused" : "public-footer"}>
        <div className="public-footer-inner">
          <SitePageFrame area="public" pageKey="/footer">
          <div className="public-footer-grid">
            <div className="public-footer-brand">
              <BrandLogo className="brand-logo brand-logo-footer" variant="dark" />
              <p className="public-footer-tagline">Maintenance, réseau et sauvegarde pour les professionnels et les petites structures.</p>
              <p className="public-footer-company">Zachary HOUNSA-HOUNKPA EI</p>
              <Link className="public-footer-contact" href={publicHref("/contact")}>Demander un devis</Link>
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
              <h2 id="public-footer-help-title">Aide et espace client</h2>
              <nav aria-label="Aide et espace client">
                <ul className="public-footer-link-list">
                  <li><Link href={publicHref("/contact")}>Nous contacter</Link></li>
                  <li><Link href="/login">Espace client</Link></li>
                  {signupEnabled ? <li><Link href={publicHref("/signup")}>Créer un accès client</Link></li> : null}
                  <li><Link href={publicHref("/ressources")}>Ressources</Link></li>
                  <li><Link href={publicHref("/wiki")}>Conseils et guides</Link></li>
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
          </SitePageFrame>
          <div className="public-footer-bottom">
            <p>© <time dateTime={String(currentYear)}>{currentYear}</time> Zachary HOUNSA-HOUNKPA EI</p>
          </div>
        </div>
      </footer>
    </>
  );
}
