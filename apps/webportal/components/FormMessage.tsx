import type { ReactNode, Ref } from "react";

type FormMessageProps = {
  title: string;
  children: ReactNode;
  tone: "success" | "error" | "info";
  /**
   * Permet a un formulaire d'amener le focus sur le resultat apres envoi.
   * `tabIndex={-1}` n'est pose que dans ce cas : sans `ref`, le bloc reste
   * hors du parcours de tabulation, comme aujourd'hui.
   */
  ref?: Ref<HTMLDivElement>;
};

export function FormMessage({
  title,
  children,
  tone,
  ref,
}: FormMessageProps) {
  return (
    <div
      aria-live={tone === "error" ? "assertive" : "polite"}
      className={`form-message form-message-${tone}`}
      ref={ref}
      role={tone === "error" ? "alert" : "status"}
      tabIndex={ref ? -1 : undefined}
    >
      <strong>{title}</strong>
      <div>{children}</div>
    </div>
  );
}
