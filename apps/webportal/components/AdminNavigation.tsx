"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Activity,
  BookOpen,
  Boxes,
  CircleDollarSign,
  ClipboardList,
  Download,
  FileText,
  LayoutDashboard,
  LifeBuoy,
  Mail,
  MonitorSmartphone,
  Package,
  ScrollText,
  ShieldCheck,
  UserPlus,
  Users,
  WalletCards,
  type LucideIcon,
} from "lucide-react";

import { LogoutButton } from "@/components/LogoutButton";

type NavSection = {
  label: string;
  items: { href: string; label: string; icon: LucideIcon; exact?: boolean }[];
};

const navigationSections: NavSection[] = [
  {
    label: "Pilotage",
    items: [
      { href: "/admin", label: "Vue d'ensemble", icon: LayoutDashboard, exact: true },
      { href: "/admin/koxo", label: "KoXo", icon: Boxes },
      { href: "/admin/activity", label: "Flux d'activité", icon: Activity },
      { href: "/admin/audit-logs", label: "Journal d'audit", icon: ScrollText },
    ],
  },
  {
    label: "Activité commerciale",
    items: [
      { href: "/admin/catalog", label: "Catalogue", icon: Package },
      { href: "/admin/public-pack-catalog", label: "Vitrine packs", icon: Boxes },
      { href: "/admin/diagnostic", label: "Diagnostic", icon: ClipboardList },
      { href: "/admin/solutions", label: "Portail solutions", icon: MonitorSmartphone },
      { href: "/admin/content", label: "Contenus", icon: FileText },
      { href: "/admin/editorial", label: "Editorial", icon: BookOpen },
      { href: "/admin/downloads", label: "Téléchargements", icon: Download },
      { href: "/admin/commercial-documents", label: "Documents", icon: FileText },
      { href: "/admin/payments", label: "Paiements", icon: WalletCards },
      { href: "/admin/subscriptions", label: "Abonnements", icon: ClipboardList },
      { href: "/admin/billing-v2", label: "Billing V2", icon: CircleDollarSign },
    ],
  },
  {
    label: "Relation client",
    items: [
      { href: "/admin/customers", label: "Clients", icon: Users },
      { href: "/admin/demo", label: "Comptes démo", icon: MonitorSmartphone },
      { href: "/admin/signups", label: "Demandes d'inscription", icon: UserPlus },
      { href: "/admin/support-requests", label: "Demandes support", icon: LifeBuoy },
      { href: "/admin/service-requests", label: "Demandes service", icon: ClipboardList },
      { href: "/admin/email-log", label: "Journal e-mails", icon: Mail },
    ],
  },
  {
    label: "Sécurité",
    items: [{ href: "/admin/sessions", label: "Sessions", icon: ShieldCheck }],
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
