import Link from "next/link";
import type { ReactNode } from "react";

import { AdminNavigation } from "@/components/AdminNavigation";
import { PortalNavigation } from "@/components/PortalNavigation";
import { getCurrentPortalSession } from "@/lib/auth";

type AppShellProps = {
  children: ReactNode;
};

export async function AppShell({ children }: AppShellProps) {
  const session = await getCurrentPortalSession();
  const hasSidebar =
    session?.user.role === "client_user"
    || session?.user.role === "internal_admin";

  return (
    <>
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
          <div className="demo-chip">Espace sécurisé</div>
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
          <main className="main-content app-content">{children}</main>
        </div>
      ) : (
        <main className="main-content">{children}</main>
      )}
      <footer className="site-footer">
        <div>
          <strong>Zachary HOUNSA-HOUNKPA EI</strong>
          <p>Portail client authentifié et administration interne contrôlée.</p>
        </div>
        <p>Aucun AD réel, paiement ou facturation réelle.</p>
      </footer>
    </>
  );
}
