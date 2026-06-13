import Link from "next/link";
import type { ReactNode } from "react";

import { PortalNavigation } from "@/components/PortalNavigation";
import { getCurrentPortalSession } from "@/lib/auth";

type AppShellProps = {
  children: ReactNode;
};

export async function AppShell({ children }: AppShellProps) {
  const session = await getCurrentPortalSession();

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
          <div className="demo-chip">V0.7 authentifiée</div>
        </div>
      </header>
      {session ? (
        <PortalNavigation displayName={session.user.displayName} />
      ) : null}
      <main className="main-content">{children}</main>
      <footer className="site-footer">
        <div>
          <strong>Zachary HOUNSA-HOUNKPA EI</strong>
          <p>Espace client authentifié, données de démonstration.</p>
        </div>
        <p>Aucun AD réel, paiement ou facturation réelle.</p>
      </footer>
    </>
  );
}
