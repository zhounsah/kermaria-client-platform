"use client";

import Link from "next/link";
import { useEffect, type ReactNode } from "react";

import { FormMessage } from "@/components/FormMessage";
import styles from "./AdminCatalog.module.css";

export type CatalogTab = { key: string; label: string; href: string };

export function CatalogNavigation({ active }: { active: string }) {
  const entries = [
    { key: "services", label: "Services", href: "/admin/catalog" },
    { key: "formules", label: "Formules", href: "/admin/catalog?section=formules" },
    { key: "engagements", label: "Engagements", href: "/admin/catalog?section=engagements" },
    { key: "integrations", label: "Intégrations", href: "/admin/catalog/integrations" },
  ];
  return (
    <nav aria-label="Catégories du catalogue" className={styles.navigation}>
      {entries.map((entry) => (
        <Link aria-current={active === entry.key ? "page" : undefined} href={entry.href} key={entry.key}>
          {entry.label}
        </Link>
      ))}
    </nav>
  );
}

export function CatalogTabs({ active, tabs }: { active: string; tabs: CatalogTab[] }) {
  return (
    <nav aria-label="Sections de la fiche" className={styles.tabs}>
      {tabs.map((tab) => (
        <Link aria-current={active === tab.key ? "page" : undefined} href={tab.href} key={tab.key}>
          {tab.label}
        </Link>
      ))}
    </nav>
  );
}

export function ImmutableCode({ value }: { value: string }) {
  return (
    <div className={styles.codeValue}>
      <code>{value}</code>
      <span>Code immuable</span>
      <button className={styles.copyButton} onClick={() => void navigator.clipboard.writeText(value)} type="button">
        Copier
      </button>
    </div>
  );
}

export function CatalogField({
  children,
  full = false,
  hint,
  label,
  htmlFor,
}: {
  children: ReactNode;
  full?: boolean;
  hint?: string;
  label: string;
  htmlFor: string;
}) {
  return (
    <div className={`${styles.field}${full ? ` ${styles.fieldFull}` : ""}`}>
      <label htmlFor={htmlFor}>{label}</label>
      {children}
      {hint ? <p className={styles.hint}>{hint}</p> : null}
    </div>
  );
}

export function CatalogToggle({ checked, description, label, name, onChange }: {
  checked: boolean;
  description: string;
  label: string;
  name: string;
  onChange: (checked: boolean) => void;
}) {
  return (
    <label className={styles.toggle}>
      <input checked={checked} name={name} onChange={(event) => onChange(event.currentTarget.checked)} type="checkbox" />
      <span className={styles.toggleText}><strong>{label}</strong><span>{description}</span></span>
    </label>
  );
}

export function CatalogFeedback({ feedback }: { feedback: { tone: "success" | "error"; message: string } | null }) {
  return feedback ? (
    <FormMessage title={feedback.tone === "success" ? "Modification enregistrée" : "Modification refusée"} tone={feedback.tone}>
      {feedback.message}
    </FormMessage>
  ) : null;
}

const UNSAVED_CHANGES_MESSAGE = "Des modifications ne sont pas enregistr├®es. Quitter cette page les abandonnera.";
/**
 * Prot├¿ge ├á la fois les vrais unloads et les navigations internes Next.js.
 * `beforeunload` seul ne voit pas les transitions client d├®clench├®es par <Link>.
 */
export function useUnsavedChangesGuard(dirty: boolean) {
  useEffect(() => {
    if (!dirty) return;
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    const onDocumentClick = (event: MouseEvent) => {
      if (
        event.defaultPrevented
        || event.button !== 0
        || event.metaKey
        || event.ctrlKey
        || event.shiftKey
        || event.altKey
      ) return;
      const target = event.target instanceof Element ? event.target : null;
      const anchor = target?.closest("a[href]");
      if (!(anchor instanceof HTMLAnchorElement) || anchor.target === "_blank" || anchor.hasAttribute("download")) return;
      const next = new URL(anchor.href, window.location.href);
      const current = new URL(window.location.href);
      if (next.origin !== current.origin) return;
      if (next.pathname === current.pathname && next.search === current.search) return;
      if (window.confirm(UNSAVED_CHANGES_MESSAGE)) return;
      event.preventDefault();
      event.stopPropagation();
    };
    window.addEventListener("beforeunload", onBeforeUnload);
    document.addEventListener("click", onDocumentClick, true);
    return () => {
      window.removeEventListener("beforeunload", onBeforeUnload);
      document.removeEventListener("click", onDocumentClick, true);
    };
  }, [dirty]);
}
export function StickyActions({ busy, dirty, onCancel }: { busy: boolean; dirty: boolean; onCancel: () => void }) {
  return (
    <div className={styles.stickyActions}>
      <span className={dirty ? styles.dirty : styles.saved}>
        {dirty ? "Modifications non enregistrées" : "Aucune modification en attente"}
      </span>
      <div className={styles.actionGroup}>
        <button className="button button-secondary" disabled={busy || !dirty} onClick={onCancel} type="button">Annuler les modifications</button>
        <button className="button" disabled={busy || !dirty} type="submit">{busy ? "Enregistrement…" : "Enregistrer"}</button>
      </div>
    </div>
  );
}

export { styles as adminCatalogStyles };
