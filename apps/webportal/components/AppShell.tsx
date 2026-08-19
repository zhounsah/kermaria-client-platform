"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  useEffect,
  useState,
  type ReactNode,
} from "react";

import type { AuthMeResponse, InternalSession } from "@kermaria/shared";

import { AdminNavigation } from "@/components/AdminNavigation";
import { HeaderCartDrawer } from "@/components/HeaderCartDrawer";
import { PortalNavigation } from "@/components/PortalNavigation";
import { PublicShell } from "@/components/PublicShell";
import { requestBffJson } from "@/lib/client-api";
import type { PortalArea } from "@/lib/public-route-config";
import {
  getPortalArea,
  isClientCheckoutContinuationPath,
  isPublicRoute,
} from "@/lib/public-route-config";
import appPackage from "../../../package.json";

const APP_VERSION_LABEL = `Version v${appPackage.displayVersion ?? appPackage.version}`;

type AppShellProps = {
  children: ReactNode;
  signupEnabled: boolean;
};

export function AppShell({
  children,
  signupEnabled,
}: AppShellProps) {
  const pathname = usePathname();
  const [session, setSession] = useState<InternalSession | null>(null);
  const usePublicShell = isPublicRoute(pathname);
  const isWikiRoute = pathname === "/wiki" || pathname.startsWith("/wiki/");
  const isCheckoutContinuation = isClientCheckoutContinuationPath(pathname);
  const portalArea: PortalArea | null = typeof window === "undefined"
    ? null
    : getPortalArea(window.location.origin);
  const keepAuthenticatedCheckoutShell =
    isCheckoutContinuation
    && portalArea === "client"
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
    if (usePublicShell && !isWikiRoute && !isCheckoutContinuation) {
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
  }, [isCheckoutContinuation, isWikiRoute, usePublicShell]);

  if (
    usePublicShell
    && !keepAuthenticatedWikiShell
    && !keepAuthenticatedCheckoutShell
  ) {
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
            {effectiveSession?.user.role === "client_user" ? (
              <HeaderCartDrawer />
            ) : null}
            <div className="demo-chip">{shellLabel}</div>
          </div>
        </div>
      </header>
      {hasSidebar ? (
        <div className="app-shell">
          {effectiveSession?.user.role === "client_user" ? (
            <PortalNavigation displayName={effectiveSession.user.displayName} />
          ) : null}
          {effectiveSession?.user.role === "internal_admin" ? (
            <AdminNavigation displayName={effectiveSession.user.displayName} />
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
