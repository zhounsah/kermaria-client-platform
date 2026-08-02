"use client";

import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { requestBffJson } from "@/lib/client-api";

type ActionState =
  | { status: "idle" | "working" }
  | { status: "success"; message: string }
  | { status: "error"; message: string };

type ValidationResponse = {
  lastRun?: { summaryMessage?: string | null } | null;
};

export function AdminKoxoValidationButton() {
  const router = useRouter();
  const isWorkingRef = useRef(false);
  const [state, setState] = useState<ActionState>({ status: "idle" });

  async function handleClick() {
    if (isWorkingRef.current) {
      return;
    }

    if (!window.confirm("Lancer une validation KoXo non destructive maintenant ?")) {
      return;
    }

    isWorkingRef.current = true;
    setState({ status: "working" });
    try {
      const response = await requestBffJson<ValidationResponse>(
        "/api/admin/koxo/validate",
        {
          method: "POST",
        },
      );

      if (!response.ok) {
        setState({ status: "error", message: response.error.message });
        return;
      }

      setState({
        status: "success",
        message:
          response.data.lastRun?.summaryMessage
          ?? "Validation KoXo exécutée.",
      });
      router.refresh();
    } finally {
      isWorkingRef.current = false;
    }
  }

  return (
    <div className="signup-actions">
      {state.status === "success" ? (
        <FormMessage title="Validation KoXo" tone="success">
          <p>{state.message}</p>
        </FormMessage>
      ) : null}
      {state.status === "error" ? (
        <FormMessage title="Validation impossible" tone="error">
          <p>{state.message}</p>
        </FormMessage>
      ) : null}
      <button
        className="button"
        disabled={state.status === "working"}
        onClick={handleClick}
        type="button"
      >
        {state.status === "working"
          ? "Validation en cours..."
          : "Tester la validation"}
      </button>
    </div>
  );
}
