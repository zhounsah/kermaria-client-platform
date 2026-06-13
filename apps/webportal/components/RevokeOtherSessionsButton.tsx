"use client";

import { useState } from "react";

type State =
  | { status: "idle" | "submitting" }
  | { status: "success"; revokedCount: number }
  | { status: "error"; message: string };

export function RevokeOtherSessionsButton() {
  const [state, setState] = useState<State>({ status: "idle" });

  async function revoke() {
    setState({ status: "submitting" });

    try {
      const response = await fetch("/api/auth/revoke-other-sessions", {
        method: "POST",
      });
      if (!response.ok) {
        setState({
          status: "error",
          message: "La révocation n'a pas pu être effectuée.",
        });
        return;
      }

      const result = (await response.json()) as { revokedCount: number };
      setState({
        status: "success",
        revokedCount: result.revokedCount,
      });
    } catch {
      setState({
        status: "error",
        message: "Le service est temporairement indisponible.",
      });
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
        <p className="form-helper" role="status">
          {state.revokedCount} autre(s) session(s) révoquée(s).
        </p>
      ) : null}
      {state.status === "error" ? (
        <p className="form-helper feedback-error" role="alert">
          {state.message}
        </p>
      ) : null}
    </div>
  );
}
