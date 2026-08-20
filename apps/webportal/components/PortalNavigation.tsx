"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Bell,
  BookOpen,
  Download,
  FileText,
  LayoutDashboard,
  LifeBuoy,
  LockKeyhole,
  PackagePlus,
  UserRound,
  Wrench,
  type LucideIcon,
} from "lucide-react";

import { LogoutButton } from "@/components/LogoutButton";

type NavSection = {
  label: string;
  items: {
    href: string;
    label: string;
    icon: LucideIcon;
    exact?: boolean;
    // Sous-pages qui gardent l'entree active alors que `exact` interdit le
    // prefixe (ex. /profile ne doit pas s'allumer sur /profile/subscriptions,
    // qui a sa propre entree, mais doit rester actif sur /profile/edit).
    activePaths?: string[];
  }[];
};

const navigationSections: NavSection[] = [
  {
    label: "Mon espace",
    items: [
      { href: "/dashboard", label: "Vue d'ensemble", icon: LayoutDashboard, exact: true },
      { href: "/services", label: "Mes services", icon: Wrench },
      { href: "/souscrire", label: "Souscrire", icon: PackagePlus },
      { href: "/profile/subscriptions", label: "Mes souscriptions", icon: FileText },
      { href: "/downloads", label: "Téléchargements", icon: Download },
      { href: "/invoices", label: "Documents & factures", icon: FileText },
    ],
  },
  {
    label: "Demandes",
    items: [
      { href: "/support", label: "Support", icon: LifeBuoy },
      { href: "/request-service", label: "Nouvelle demande", icon: PackagePlus },
    ],
  },
  {
    label: "Suivi",
    items: [
      { href: "/notifications", label: "Notifications", icon: Bell },
      { href: "/wiki", label: "Wiki", icon: BookOpen },
      {
        href: "/profile",
        label: "Profil",
        icon: UserRound,
        exact: true,
        activePaths: ["/profile/edit"],
      },
      { href: "/password", label: "Mot de passe", icon: LockKeyhole },
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
                const isActive = item.activePaths?.includes(pathname)
                  || (item.exact
                    ? pathname === item.href
                    : pathname === item.href
                      || pathname.startsWith(`${item.href}/`));

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
                      <item.icon aria-hidden="true" size={16} strokeWidth={1.75} />
                      <span>{item.label}</span>
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
