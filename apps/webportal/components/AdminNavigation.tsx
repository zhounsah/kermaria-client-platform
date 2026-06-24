"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

import { LogoutButton } from "@/components/LogoutButton";

const navigationItems = [
  { href: "/admin", label: "Vue d'ensemble" },
  { href: "/admin/catalog", label: "Catalogue" },
  { href: "/admin/commercial-documents", label: "Documents" },
  { href: "/admin/payments", label: "Paiements" },
  { href: "/admin/email-log", label: "E-mails" },
  { href: "/admin/customers", label: "Clients" },
  { href: "/admin/support-requests", label: "Support" },
  { href: "/admin/service-requests", label: "Demandes service" },
  { href: "/admin/sessions", label: "Sessions" },
  { href: "/admin/audit-logs", label: "Audits" },
];

type AdminNavigationProps = {
  displayName: string;
};

export function AdminNavigation({ displayName }: AdminNavigationProps) {
  const pathname = usePathname();

  return (
    <nav aria-label="Navigation administration" className="portal-nav">
      <div className="portal-nav-inner">
        <div className="portal-nav-links">
          {navigationItems.map((item) => {
            const isActive =
              item.href === "/admin"
                ? pathname === item.href
                : pathname.startsWith(item.href);

            return (
              <Link
                aria-current={isActive ? "page" : undefined}
                className={isActive ? "nav-link nav-link-active" : "nav-link"}
                href={item.href}
                key={item.href}
              >
                {item.label}
              </Link>
            );
          })}
        </div>
        <span className="nav-role">Administration interne</span>
        <span className="nav-user" title={displayName}>
          {displayName}
        </span>
        <LogoutButton />
      </div>
    </nav>
  );
}
