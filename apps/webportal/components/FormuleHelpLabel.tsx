"use client";
import { Info } from "lucide-react";
import { type ReactNode, useEffect, useId, useRef, useState } from "react";
import {
  FORMULE_HELP_CONTENT,
  type FormuleHelpKey,
} from "@/lib/formule-help";
type Props = {
  helpKey: FormuleHelpKey;
  children: ReactNode;
};
export function FormuleHelpLabel({ helpKey, children }: Props) {
  const [open, setOpen] = useState(false);
  const content = FORMULE_HELP_CONTENT[helpKey];
  const popoverId = useId();
  const rootRef = useRef<HTMLSpanElement>(null);
  const triggerRef = useRef<HTMLSpanElement>(null);
  useEffect(() => {
    if (!open) {
      return;
    }
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setOpen(false);
        triggerRef.current?.focus();
      }
    }
    function closeOnOutsidePress(event: PointerEvent) {
      if (
        rootRef.current
        && event.target instanceof Node
        && !rootRef.current.contains(event.target)
      ) {
        setOpen(false);
      }
    }
    document.addEventListener("keydown", closeOnEscape);
    document.addEventListener("pointerdown", closeOnOutsidePress);
    return () => {
      document.removeEventListener("keydown", closeOnEscape);
      document.removeEventListener("pointerdown", closeOnOutsidePress);
    };
  }, [open]);
  function toggleHelp() {
    setOpen((current) => !current);
  }
  return (
    <span className="formule-help-label" ref={rootRef}>
      <span className="formule-help-text">{children}</span>
      <span
        ref={triggerRef}
        role="button"
        tabIndex={0}
        className="formule-help-trigger"
        aria-label={`Afficher l’aide : ${content.title}`}
        aria-expanded={open}
        aria-controls={popoverId}
        aria-describedby={open ? popoverId : undefined}
        onPointerDown={(event) => event.stopPropagation()}
        onClick={(event) => {
          // Le composant peut vivre dans un <label> radio/checkbox : le clic
          // sur l'aide ne doit jamais sélectionner ou désélectionner l'option.
          event.preventDefault();
          event.stopPropagation();
          toggleHelp();
        }}
        onKeyDown={(event) => {
          if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            event.stopPropagation();
            toggleHelp();
          }
        }}
      >
        <Info aria-hidden="true" size={15} strokeWidth={2.2} />
      </span>
      {open ? (
        <span id={popoverId} className="formule-help-popover" role="tooltip">
          <strong>{content.title}</strong>
          <span>{content.description}</span>
        </span>
      ) : null}
    </span>
  );
}
