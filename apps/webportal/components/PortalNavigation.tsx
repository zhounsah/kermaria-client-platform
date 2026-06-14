"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

import { LogoutButton } from "@/components/LogoutButton";

const navigationItems = [
  { href: "/dashboard", label: "Vue d'ensemble" },
  { href: "/services", label: "Services" },
  { href: "/invoices", label: "Factures" },
  { href: "/support", label: "Support" },
  { href: "/request-service", label: "Nouvelle demande" },
  { href: "/notifications", label: "Notifications" },
  { href: "/profile", label: "Profil" },
];

type PortalNavigationProps = {
  displayName: string;
};

export function PortalNavigation({ displayName }: PortalNavigationProps) {
  const pathname = usePathname();

  return (
    <nav aria-label="Navigation principale" className="portal-nav">
      <div className="portal-nav-inner">
        <div className="portal-nav-links">
          {navigationItems.map((item) => {
            const isActive =
              pathname === item.href
              || pathname.startsWith(`${item.href}/`);

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
        <span className="nav-user" title={displayName}>
          {displayName}
        </span>
        <LogoutButton />
      </div>
    </nav>
  );
}
