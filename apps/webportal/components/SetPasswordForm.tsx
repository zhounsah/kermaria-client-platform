"use client";

import Link from "next/link";
import { useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type SetPasswordFormProps = {
  token: string;
};

const MIN_PASSWORD_LENGTH = 12;

type SetPasswordState =
  | { status: "idle" | "submitting" }
  | { status: "success"; message: string }
  | { status: "error"; message: string };

type SetPasswordResponse = {
  code: string;
  message: string;
  correlation_id?: string;
};

export function SetPasswordForm({ token }: SetPasswordFormProps) {
  const isSubmittingRef = useRef(false);
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [state, setState] = useState<SetPasswordState>({ status: "idle" });

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current) {
      return;
    }

    if (password.length < MIN_PASSWORD_LENGTH) {
      setState({
        status: "error",
        message: `Le mot de passe doit comporter au moins ${MIN_PASSWORD_LENGTH} caractères.`,
      });
      return;
    }

    if (password !== confirmPassword) {
      setState({
        status: "error",
        message: "Les deux mots de passe ne correspondent pas.",
      });
      return;
    }

    isSubmittingRef.current = true;
    setState({ status: "submitting" });

    try {
      const response = await requestBffJson<SetPasswordResponse>(
        "/api/set-password",
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ token, password }),
        },
      );

      if (!response.ok) {
        setState({ status: "error", message: response.error.message });
        return;
      }

      setState({
        status: "success",
        message:
          "Mot de passe défini. Vous pouvez désormais vous connecter à votre espace.",
      });
      setPassword("");
      setConfirmPassword("");
    } finally {
      isSubmittingRef.current = false;
    }
  }

  if (state.status === "success") {
    return (
      <FormMessage title="Mot de passe défini" tone="success">
        <p>{state.message}</p>
        <p>
          <Link href="/login">Aller à la page de connexion</Link>
        </p>
      </FormMessage>
    );
  }

  return (
    <form
      action="/api/set-password"
      className="form-card set-password-form"
      method="post"
      noValidate
      onSubmit={handleSubmit}
    >
      {state.status === "error" ? (
        <FormMessage title="Définition impossible" tone="error">
          <p>{state.message}</p>
        </FormMessage>
      ) : null}

      <label>
        Nouveau mot de passe
        <input
          autoComplete="new-password"
          minLength={MIN_PASSWORD_LENGTH}
          name="password"
          onChange={(event) => setPassword(event.target.value)}
          required
          type="password"
          value={password}
        />
      </label>

      <label>
        Confirmez le mot de passe
        <input
          autoComplete="new-password"
          minLength={MIN_PASSWORD_LENGTH}
          name="confirmPassword"
          onChange={(event) => setConfirmPassword(event.target.value)}
          required
          type="password"
          value={confirmPassword}
        />
      </label>

      <p className="set-password-note">
        Choisissez un mot de passe d&apos;au moins {MIN_PASSWORD_LENGTH}{" "}
        caractères. Ce lien est à usage unique.
      </p>

      <SubmitButton
        idleLabel="Définir le mot de passe"
        isSubmitting={state.status === "submitting"}
        submittingLabel="Enregistrement..."
      />
    </form>
  );
}
