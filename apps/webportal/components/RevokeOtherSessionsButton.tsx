"use client";

import { useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { requestBffJson } from "@/lib/client-api";

type State =
  | { status: "idle" | "submitting" }
  | { status: "success"; revokedCount: number }
  | { status: "error"; message: string };

export function RevokeOtherSessionsButton() {
  const isSubmittingRef = useRef(false);
  const [state, setState] = useState<State>({ status: "idle" });

  async function revoke() {
    if (isSubmittingRef.current) {
      return;
    }

    isSubmittingRef.current = true;
    setState({ status: "submitting" });

    try {
      const response = await requestBffJson<{ revokedCount: number }>(
        "/api/auth/revoke-other-sessions",
        { method: "POST" },
      );

      if (!response.ok) {
        setState({
          status: "error",
          message: response.error.message,
        });
        return;
      }

      setState({
        status: "success",
        revokedCount: response.data.revokedCount,
      });
    } finally {
      isSubmittingRef.current = false;
    }
  }

  return (
    <div className="session-action">
      <button
        className="button button-secondary"
        disabled={state.status === "submitting"}
        onClick={revoke}
        type="button"
      >
        {state.status === "submitting"
          ? "Révocation..."
          : "Déconnecter mes autres sessions"}
      </button>
      {state.status === "success" ? (
        <FormMessage title="Sessions mises à jour" tone="success">
          <p>{state.revokedCount} autre(s) session(s) révoquée(s).</p>
        </FormMessage>
      ) : null}
      {state.status === "error" ? (
        <FormMessage title="Révocation impossible" tone="error">
          <p>{state.message}</p>
        </FormMessage>
      ) : null}
    </div>
  );
}
