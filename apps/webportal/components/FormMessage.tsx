import type { ReactNode } from "react";

type FormMessageProps = {
  title: string;
  children: ReactNode;
  tone: "success" | "error" | "info";
};

export function FormMessage({
  title,
  children,
  tone,
}: FormMessageProps) {
  return (
    <div
      aria-live={tone === "error" ? "assertive" : "polite"}
      className={`form-message form-message-${tone}`}
      role={tone === "error" ? "alert" : "status"}
    >
      <strong>{title}</strong>
      <div>{children}</div>
    </div>
  );
}
