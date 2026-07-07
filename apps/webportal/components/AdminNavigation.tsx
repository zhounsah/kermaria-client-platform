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
    label: "Pilotage",
    items: [
      { href: "/admin", label: "Vue d'ensemble", exact: true },
      { href: "/admin/activity", label: "Flux d'activité" },
      { href: "/admin/audit-logs", label: "Journal d'audit" },
    ],
  },
  {
    label: "Activité commerciale",
    items: [
      { href: "/admin/catalog", label: "Catalogue" },
      { href: "/admin/content", label: "Contenus" },
      { href: "/admin/commercial-documents", label: "Documents" },
      { href: "/admin/payments", label: "Paiements" },
      { href: "/admin/subscriptions", label: "Abonnements" },
    ],
  },
  {
    label: "Relation client",
    items: [
      { href: "/admin/customers", label: "Clients" },
      { href: "/admin/signups", label: "Demandes d'inscription" },
      { href: "/admin/support-requests", label: "Demandes support" },
      { href: "/admin/service-requests", label: "Demandes service" },
      { href: "/admin/email-log", label: "Journal e-mails" },
    ],
  },
  {
    label: "Sécurité",
    items: [{ href: "/admin/sessions", label: "Sessions" }],
  },
];

type AdminNavigationProps = {
  displayName: string;
};

export function AdminNavigation({ displayName }: AdminNavigationProps) {
  const pathname = usePathname();

  return (
    <nav aria-label="Navigation administration" className="app-sidebar">
      <div className="app-sidebar-header">
        <span className="app-sidebar-role">Administration</span>
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
