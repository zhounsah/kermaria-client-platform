import type { ReactNode } from "react";

type SectionCardProps = {
  children: ReactNode;
  className?: string;
  ariaLabel?: string;
};

export function SectionCard({
  children,
  className,
  ariaLabel,
}: SectionCardProps) {
  const classes = ["content-panel", "section-card", className]
    .filter(Boolean)
    .join(" ");

  return (
    <section aria-label={ariaLabel} className={classes}>
      {children}
    </section>
  );
}
