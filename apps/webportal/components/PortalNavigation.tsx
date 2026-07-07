"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

import { LogoutButton } from "@/components/LogoutButton";

type NavSection = {
  label: string;
  items: { href: string; label: string; exact?: boolean }[];
};

const navigationSections: NavSection[] = [
  {
    label: "Mon espace",
    items: [
      { href: "/dashboard", label: "Vue d'ensemble", exact: true },
      { href: "/services", label: "Mes services" },
      { href: "/souscrire", label: "Souscrire" },
      { href: "/profile/subscriptions", label: "Mes souscriptions" },
      { href: "/invoices", label: "Documents & factures" },
    ],
  },
  {
    label: "Demandes",
    items: [
      { href: "/support", label: "Support" },
      { href: "/request-service", label: "Nouvelle demande" },
    ],
  },
  {
    label: "Suivi",
    items: [
      { href: "/notifications", label: "Notifications" },
      { href: "/profile", label: "Profil", exact: true },
      { href: "/password", label: "Mot de passe" },
    ],
  },
];

type PortalNavigationProps = {
  displayName: string;
};

export function PortalNavigation({ displayName }: PortalNavigationProps) {
  const pathname = usePathname();

  return (
    <nav aria-label="Navigation principale" className="app-sidebar">
      <div className="app-sidebar-header">
        <span className="app-sidebar-role">Espace client</span>
        <span className="app-sidebar-user" title={displayName}>
          {displayName}
        </span>
      </div>
      <div className="app-sidebar-scroll">
        {navigationSections.map((section) => (
          <div className="app-sidebar-section" key={section.label}>
            <span className="app-sidebar-section-label">{section.label}</span>
            <ul className="app-sidebar-list">
              {section.items.map((item) => {
                const isActive = item.exact
                  ? pathname === item.href
                  : pathname === item.href || pathname.startsWith(`${item.href}/`);

                return (
                  <li key={item.href}>
                    <Link
                      aria-current={isActive ? "page" : undefined}
                      className={
                        isActive
                          ? "app-sidebar-link app-sidebar-link-active"
                          : "app-sidebar-link"
                      }
                      href={item.href}
                    >
                      {item.label}
                    </Link>
                  </li>
                );
              })}
            </ul>
          </div>
        ))}
      </div>
      <div className="app-sidebar-footer">
        <LogoutButton />
      </div>
    </nav>
  );
}
