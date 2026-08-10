"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";

import type { InternalSession } from "@kermaria/shared";

import { AdminNavigation } from "@/components/AdminNavigation";
import { HeaderCartDrawer } from "@/components/HeaderCartDrawer";
import { PortalNavigation } from "@/components/PortalNavigation";
import { PublicShell } from "@/components/PublicShell";
import type { PortalArea } from "@/lib/public-route-config";
import { isPublicRoute } from "@/lib/public-route-config";
import appPackage from "../../../package.json";

const APP_VERSION_LABEL = `Version v${appPackage.version}`;

type AppShellProps = {
  children: ReactNode;
  portalArea: PortalArea | null;
  session: InternalSession | null;
  signupEnabled: boolean;
};

export function AppShell({
  children,
  portalArea,
  session,
  signupEnabled,
}: AppShellProps) {
  const pathname = usePathname();
  const usePublicShell = isPublicRoute(pathname);
  const isWikiRoute = pathname === "/wiki" || pathname.startsWith("/wiki/");
  const keepAuthenticatedWikiShell =
    isWikiRoute
    && portalArea === "client"
    && session?.user.role === "client_user";
  const hasSidebar =
    session?.user.role === "client_user"
    || session?.user.role === "internal_admin";
  const shellLabel =
    session?.user.role === "internal_admin"
      ? "Administration interne"
      : session?.user.role === "client_user"
        ? "Espace client sécurisé"
        : "Accès sécurisé";

  if (usePublicShell && !keepAuthenticatedWikiShell) {
    return (
      <PublicShell signupEnabled={signupEnabled}>
        {children}
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
            <span className="brand-mark" aria-hidden="true">
              ZH
            </span>
            <span className="brand-copy">
              <strong>Zachary HOUNSA-HOUNKPA EI</strong>
              <small>Espace client</small>
            </span>
          </Link>
          <div className="site-header-tools">
            {session?.user.role === "client_user" ? <HeaderCartDrawer /> : null}
            <div className="demo-chip">{shellLabel}</div>
          </div>
        </div>
      </header>
      {hasSidebar ? (
        <div className="app-shell">
          {session?.user.role === "client_user" ? (
            <PortalNavigation displayName={session.user.displayName} />
          ) : null}
          {session?.user.role === "internal_admin" ? (
            <AdminNavigation displayName={session.user.displayName} />
          ) : null}
          <main className="main-content app-content" id="main-content">
            {children}
          </main>
        </div>
      ) : (
        <main className="main-content" id="main-content">
          {children}
        </main>
      )}
      <footer className="site-footer">
        <div>
          <strong>Zachary HOUNSA-HOUNKPA EI</strong>
          <p>Portail client authentifié et administration interne contrôlée.</p>
          <p>{APP_VERSION_LABEL}</p>
        </div>
        <p>Accès client sécurisé, gestion des documents et suivi des services.</p>
      </footer>
    </>
  );
}
