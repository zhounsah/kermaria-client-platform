"use client";

import Link from "next/link";
import { Menu, X } from "lucide-react";
import { usePathname } from "next/navigation";
import {
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from "react";

import type { AuthMeResponse, InternalSession } from "@kermaria/shared";

import { AdminNavigation } from "@/components/AdminNavigation";
import { PortalNavigation } from "@/components/PortalNavigation";
import { PublicShell } from "@/components/PublicShell";
import { SitePageFrame } from "@/components/SitePageFrame";
import { BrandLogo } from "@/components/BrandLogo";
import { requestBffJson } from "@/lib/client-api";
import type { PortalArea } from "@/lib/public-route-config";
import {
  getPortalArea,
  isClientCheckoutContinuationPath,
  isPublicRoute,
} from "@/lib/public-route-config";
import appPackage from "../../../package.json";

const APP_VERSION_LABEL = `Version v${appPackage.displayVersion ?? appPackage.version}`;
const CLIENT_VPS_DETAIL_PATH =
  /^\/services\/vps\/[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const UUID_PATH_PART = /\/[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}(?=\/|$)/gi;
const FORMULA_DETAIL_PATH = /^\/formules\/(?!reprendre$)[a-z0-9-]+$/;
const OFFER_DETAIL_PATH = /^\/offres\/[a-z0-9-]+$/;
const COMPOSABLE_WIDGET_PAGE_KEYS = new Set([
  "/offres",
  "/offres/[slug]",
  "/panier",
  "/souscription",
  "/formules",
  "/formules/[code]",
  "/tarifs",
  "/diagnostic",
  "/contact",
  "/demander-mes-donnees",
  "/signup",
  "/souscrire",
  "/dashboard",
  "/profile",
  "/admin",
  "/admin/catalog",
  "/profile/donnees", "/profile/donnees/[id]",
  "/admin/data-requests", "/admin/data-requests/[id]",
]);

type AppShellProps = {
  children: ReactNode;
  localPortalOrigin: string | null;
  signupEnabled: boolean;
};

export function AppShell({
  children,
  localPortalOrigin,
  signupEnabled,
}: AppShellProps) {
  const pathname = usePathname();
  const pageKey = FORMULA_DETAIL_PATH.test(pathname)
    ? "/formules/[code]" : OFFER_DETAIL_PATH.test(pathname)
      ? "/offres/[slug]" : pathname.replace(UUID_PATH_PART, "/[id]");
  const hasOwnPageLayout = COMPOSABLE_WIDGET_PAGE_KEYS.has(pageKey);
  const [session, setSession] = useState<InternalSession | null>(null);
  const [sidebarState, setSidebarState] = useState({ path: "", open: false });
  const sidebarOpen = sidebarState.path === pathname && sidebarState.open;
  const sidebarToggleRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!sidebarOpen) return;
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;
      setSidebarState({ path: pathname, open: false });
      sidebarToggleRef.current?.focus();
    };
    document.addEventListener("keydown", closeOnEscape);
    return () => document.removeEventListener("keydown", closeOnEscape);
  }, [pathname, sidebarOpen]);
  const portalArea: PortalArea | null = typeof window === "undefined"
    ? null
    : getPortalArea(window.location.origin, localPortalOrigin);
  // `/services` est volontairement servi comme vitrine sur le domaine public
  // et comme espace « Mes services » sur le portail client. Le choix du shell
  // doit donc tenir compte de l'hôte, pas uniquement du chemin.
  const isClientVpsDetailRoute = CLIENT_VPS_DETAIL_PATH.test(pathname);
  const isClientServicesPath = pathname === "/services" || isClientVpsDetailRoute;
  const isLocalClientServicesRoute = isClientServicesPath && portalArea === "local";
  const isClientServicesRoute = isClientServicesPath
    && (
      portalArea === "client"
      || (
        isLocalClientServicesRoute
        && session?.user.role === "client_user"
      )
    );
  const usePublicShell = isPublicRoute(pathname) && !isClientServicesRoute;
  const isWikiRoute = pathname === "/wiki" || pathname.startsWith("/wiki/");
  const isCheckoutContinuation = isClientCheckoutContinuationPath(pathname);
  const keepAuthenticatedCheckoutShell =
    isCheckoutContinuation
    && (portalArea === "client" || portalArea === "local")
    && session?.user.role === "client_user";
  const effectiveSession =
    usePublicShell && !isWikiRoute && !keepAuthenticatedCheckoutShell
      ? null
      : session;
  const keepAuthenticatedWikiShell =
    isWikiRoute
    && portalArea === "client"
    && effectiveSession?.user.role === "client_user";
  const hasSidebar =
    effectiveSession?.user.role === "client_user"
    || effectiveSession?.user.role === "internal_admin";
  const shellLabel =
    effectiveSession?.user.role === "internal_admin"
      ? "Administration interne"
      : effectiveSession?.user.role === "client_user"
        ? "Espace client sécurisé"
        : "Accès sécurisé";

  useEffect(() => {
    if (
      usePublicShell
      && !isWikiRoute
      && !isCheckoutContinuation
      && !isLocalClientServicesRoute
    ) {
      return;
    }

    let ignore = false;

    async function loadSession() {
      const result = await requestBffJson<AuthMeResponse>(
        "/api/auth/me",
        { method: "GET" },
        5000,
      );

      if (ignore) {
        return;
      }

      setSession(
        result.ok && result.data.authenticated
          ? {
              user: result.data.user,
              expiresAt: result.data.expiresAt,
            }
          : null,
      );
    }

    void loadSession();

    return () => {
      ignore = true;
    };
  }, [
    isCheckoutContinuation,
    isLocalClientServicesRoute,
    isWikiRoute,
    usePublicShell,
  ]);

  if (
    usePublicShell
    && !keepAuthenticatedWikiShell
    && !keepAuthenticatedCheckoutShell
  ) {
    return (
      <PublicShell signupEnabled={signupEnabled}>
        {pageKey === "/" || pageKey === "/services" || hasOwnPageLayout ? children :
          <SitePageFrame area="public" pageKey={pageKey}>{children}</SitePageFrame>}
      </PublicShell>
    );
  }

  return (
    <>
      <a className="skip-link" href="#main-content">
        Aller au contenu
      </a>
      <header className="site-header">
        <div className="site-header-inner">
          <Link className="brand" href="/">
            <BrandLogo className="brand-logo brand-logo-app" priority variant="dark" />
          </Link>
          <div className="site-header-tools">
            <div className="demo-chip">{shellLabel}</div>
          </div>
        </div>
      </header>
      {hasSidebar ? (
        <div className="app-shell">
          <button aria-controls="app-sidebar-navigation" aria-expanded={sidebarOpen}
            className="app-sidebar-toggle" onClick={() => setSidebarState({ path: pathname, open: !sidebarOpen })}
            ref={sidebarToggleRef}
            type="button">
            {sidebarOpen ? <X aria-hidden="true" size={20} /> : <Menu aria-hidden="true" size={20} />}
            {sidebarOpen ? "Fermer le menu" : "Ouvrir le menu"}
          </button>
          {effectiveSession?.user.role === "client_user" ? (
            <PortalNavigation displayName={effectiveSession.user.displayName} mobileOpen={sidebarOpen} />
          ) : null}
          {effectiveSession?.user.role === "internal_admin" ? (
            <AdminNavigation displayName={effectiveSession.user.displayName} mobileOpen={sidebarOpen} />
          ) : null}
          <main className="main-content app-content" id="main-content">
            {hasOwnPageLayout ? children :
              <SitePageFrame area={effectiveSession?.user.role === "internal_admin" ? "admin" : "client"} pageKey={pageKey}>{children}</SitePageFrame>}
          </main>
        </div>
      ) : (
        <main className="main-content" id="main-content">
          {hasOwnPageLayout ? children :
            <SitePageFrame area={pageKey === "/login" || pageKey === "/set-password" ? "public" : null} pageKey={pageKey}>{children}</SitePageFrame>}
        </main>
      )}
      <footer className="site-footer">
        <div>
          <BrandLogo className="brand-logo brand-logo-footer" variant="dark" />
          <strong>Zachary HOUNSA-HOUNKPA EI</strong>
          <p>Portail client sécurisé pour le suivi de vos services.</p>
          {effectiveSession?.user.role === "internal_admin" ? <p>{APP_VERSION_LABEL}</p> : null}
        </div>
        <p>Accès sécurisé à vos documents et à vos services.</p>
      </footer>
    </>
  );
}
