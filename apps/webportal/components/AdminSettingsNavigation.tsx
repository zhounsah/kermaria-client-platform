"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
const items = [
  { href: "/admin/settings", label: "Vue d'ensemble" },
  { href: "/admin/settings/messages", label: "Messages" },
  { href: "/admin/settings/diagnostic", label: "Diagnostic" },
  { href: "/admin/settings/billing", label: "Facturation" },
  { href: "/admin/settings/demonstrations", label: "D\u00e9monstrations" },
  { href: "/admin/settings/integrations", label: "Int\u00e9grations" },
  { href: "/admin/settings/directory", label: "Annuaire & KoXo" },
  { href: "/admin/settings/runtime", label: "Runtime" },
  { href: "/admin/settings/audit", label: "Audit & permissions" },
] as const;
export function AdminSettingsNavigation() {
  const pathname = usePathname();
  return (
    <nav aria-label="Sections du Centre de configuration" className="admin-settings-navigation">
      <div className="admin-settings-navigation-scroll">
        {items.map((item) => {
          const active = item.href === "/admin/settings"
            ? pathname === item.href
            : pathname === item.href || pathname.startsWith(`${item.href}/`);
          return (
            <Link
              aria-current={active ? "page" : undefined}
              className={active ? "admin-settings-navigation-link active" : "admin-settings-navigation-link"}
              href={item.href}
              key={item.href}
            >
              {item.label}
            </Link>
          );
        })}
      </div>
    </nav>
  );
}
