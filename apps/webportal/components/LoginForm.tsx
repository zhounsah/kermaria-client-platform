"use client";

import type { ApiError, LoginPayload } from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { useState } from "react";

type LoginState =
  | { status: "idle" | "submitting" }
  | { status: "error"; message: string };

export function LoginForm() {
  const router = useRouter();
  const [payload, setPayload] = useState<LoginPayload>({
    email: "",
    password: "",
  });
  const [state, setState] = useState<LoginState>({ status: "idle" });

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setState({ status: "submitting" });

    try {
      const response = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });

      if (!response.ok) {
        const error = (await response.json()) as Partial<ApiError>;
        setState({
          status: "error",
          message: error.message ?? "Identifiants invalides.",
        });
        setPayload((current) => ({ ...current, password: "" }));
        return;
      }

      setPayload({ email: "", password: "" });
      router.replace("/dashboard");
      router.refresh();
    } catch {
      setState({
        status: "error",
        message: "Le service de connexion est temporairement indisponible.",
      });
      setPayload((current) => ({ ...current, password: "" }));
    }
  }

  return (
    <form
      action="/api/auth/login"
      className="form-card login-form"
      method="post"
      onSubmit={handleSubmit}
    >
      {state.status === "error" ? (
        <div className="feedback-message feedback-error" role="alert">
          <strong>Connexion impossible.</strong>
          <span>{state.message}</span>
        </div>
      ) : null}

      <label>
        Adresse e-mail
        <input
          autoComplete="username"
          maxLength={254}
          name="email"
          onChange={(event) =>
            setPayload((current) => ({
              ...current,
              email: event.target.value,
            }))}
          required
          type="email"
          value={payload.email}
        />
      </label>

      <label>
        Mot de passe
        <input
          autoComplete="current-password"
          maxLength={1024}
          name="password"
          onChange={(event) =>
            setPayload((current) => ({
              ...current,
              password: event.target.value,
            }))}
          required
          type="password"
          value={payload.password}
        />
      </label>

      <button
        className="button"
        disabled={state.status === "submitting"}
        type="submit"
      >
        {state.status === "submitting"
          ? "Connexion en cours..."
          : "Se connecter"}
      </button>
    </form>
  );
}
