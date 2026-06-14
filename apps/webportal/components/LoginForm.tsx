"use client";

import type {
  AuthMeResponse,
  LoginPayload,
} from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";
import {
  type FieldErrors,
  hasFieldErrors,
  validateLoginPayload,
} from "@/lib/form-validation";

type LoginState =
  | { status: "idle" | "submitting" }
  | { status: "error"; message: string };

export function LoginForm() {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [payload, setPayload] = useState<LoginPayload>({
    email: "",
    password: "",
  });
  const [state, setState] = useState<LoginState>({ status: "idle" });
  const [fieldErrors, setFieldErrors] = useState<
    FieldErrors<keyof LoginPayload>
  >({});

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current) {
      return;
    }

    const validation = validateLoginPayload(payload);
    setFieldErrors(validation.errors);
    setPayload(validation.payload);

    if (hasFieldErrors(validation.errors)) {
      setState({
        status: "error",
        message: "Vérifiez les champs signalés.",
      });
      return;
    }

    isSubmittingRef.current = true;
    setState({ status: "submitting" });

    try {
      const response = await requestBffJson<AuthMeResponse>(
        "/api/auth/login",
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(validation.payload),
        },
      );

      if (!response.ok) {
        setState({
          status: "error",
          message: response.error.message,
        });
        setPayload((current) => ({ ...current, password: "" }));
        return;
      }

      const result = response.data;
      setPayload({ email: "", password: "" });
      router.replace(
        result.authenticated && result.user.role === "internal_admin"
          ? "/admin"
          : "/dashboard",
      );
      router.refresh();
    } finally {
      isSubmittingRef.current = false;
    }
  }

  return (
    <form
      action="/api/auth/login"
      className="form-card login-form"
      method="post"
      noValidate
      onSubmit={handleSubmit}
    >
      {state.status === "error" ? (
        <FormMessage title="Connexion impossible" tone="error">
          <p>{state.message}</p>
        </FormMessage>
      ) : null}

      <label>
        Adresse e-mail
        <input
          aria-describedby={
            fieldErrors.email ? "login-email-error" : undefined
          }
          aria-invalid={Boolean(fieldErrors.email)}
          autoComplete="username"
          maxLength={254}
          name="email"
          onChange={(event) => {
            setFieldErrors((current) => ({
              ...current,
              email: undefined,
            }));
            setPayload((current) => ({
              ...current,
              email: event.target.value,
            }));
          }}
          required
          type="email"
          value={payload.email}
        />
        {fieldErrors.email ? (
          <span className="field-error" id="login-email-error">
            {fieldErrors.email}
          </span>
        ) : null}
      </label>

      <label>
        Mot de passe
        <input
          aria-describedby={
            fieldErrors.password ? "login-password-error" : undefined
          }
          aria-invalid={Boolean(fieldErrors.password)}
          autoComplete="current-password"
          maxLength={1024}
          name="password"
          onChange={(event) => {
            setFieldErrors((current) => ({
              ...current,
              password: undefined,
            }));
            setPayload((current) => ({
              ...current,
              password: event.target.value,
            }));
          }}
          required
          type="password"
          value={payload.password}
        />
        {fieldErrors.password ? (
          <span className="field-error" id="login-password-error">
            {fieldErrors.password}
          </span>
        ) : null}
      </label>

      <SubmitButton
        idleLabel="Se connecter"
        isSubmitting={state.status === "submitting"}
        submittingLabel="Connexion en cours..."
      />
    </form>
  );
}
