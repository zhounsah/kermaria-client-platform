"use client";

import Link from "next/link";
import { useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type SetPasswordFormProps = {
  token: string;
  /**
   * Parcours d'origine du lien. Absent pour l'inscription.
   *
   * Purement présentationnel : il choisit le libellé et la borne haute
   * affichée. Il n'autorise rien — c'est le jeton, et son `purpose` vérifié
   * côté API, qui décident.
   */
  flow?: string;
};

const MIN_PASSWORD_LENGTH = 12;
// Inscription : borne historique du parcours signup.
const MAX_PASSWORD_LENGTH = 200;
// Utilisateur supplémentaire Billing V2 : borne réelle du service Phase 4.
// Afficher 200 ici laisserait saisir un mot de passe que l'API refuserait.
const MAX_ADDITIONAL_USER_PASSWORD_LENGTH = 128;
const ADDITIONAL_USER_FLOW = "billing-v2-additional-user";

type SetPasswordState =
  | { status: "idle" | "submitting" }
  | { status: "success"; message: string }
  | { status: "error"; message: string };

type SetPasswordResponse = {
  code: string;
  message: string;
  correlation_id?: string;
};

export function SetPasswordForm({ token, flow }: SetPasswordFormProps) {
  const isSubmittingRef = useRef(false);
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [state, setState] = useState<SetPasswordState>({ status: "idle" });

  const maxPasswordLength = flow === ADDITIONAL_USER_FLOW
    ? MAX_ADDITIONAL_USER_PASSWORD_LENGTH
    : MAX_PASSWORD_LENGTH;

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

    if (password.length > maxPasswordLength) {
      setState({
        status: "error",
        message: `Le mot de passe ne doit pas dépasser ${maxPasswordLength} caractères.`,
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
          // Deux corps litteraux plutot qu'un objet compose : le parcours
          // d'inscription continue d'envoyer exactement `{ token, password }`,
          // sans champ surnumeraire ajoute par construction.
          body: flow
            ? JSON.stringify({ token, password, flow })
            : JSON.stringify({ token, password }),
        },
      );

      if (!response.ok) {
        setState({ status: "error", message: response.error.message });
        return;
      }

      setState({
        status: "success",
        message: flow === ADDITIONAL_USER_FLOW
          ? "Mot de passe défini. Vous pouvez vous connecter à votre espace : vos accès sont en cours d'activation et seront disponibles sous peu."
          : "Mot de passe défini. Connectez-vous maintenant à votre espace : votre tableau de bord vous guidera vers la reprise de votre pack ou la suite de votre activation.",
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
          <Link href={flow === ADDITIONAL_USER_FLOW ? "/login" : "/login?next=%2Fformules%2Freprendre"}>
            {flow === ADDITIONAL_USER_FLOW
              ? "Se connecter et ouvrir le tableau de bord"
              : "Se connecter et finaliser ma formule"}
          </Link>
        </p>
      </FormMessage>
    );
  }

  return (
    <form
      acceptCharset="UTF-8"
      action="/api/set-password"
      className="form-card set-password-form"
      encType="application/x-www-form-urlencoded"
      method="post"
      noValidate
      onSubmit={handleSubmit}
    >
      <input name="token" type="hidden" value={token} />
      {flow ? <input name="flow" type="hidden" value={flow} /> : null}

      {state.status === "error" ? (
        <FormMessage title="Définition impossible" tone="error">
          <p>{state.message}</p>
        </FormMessage>
      ) : null}

      <label>
        Nouveau mot de passe
        <input
          autoComplete="new-password"
          maxLength={maxPasswordLength}
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
          maxLength={maxPasswordLength}
          minLength={MIN_PASSWORD_LENGTH}
          name="confirmPassword"
          onChange={(event) => setConfirmPassword(event.target.value)}
          required
          type="password"
          value={confirmPassword}
        />
      </label>

      <p className="set-password-note">
        Choisissez un mot de passe de {MIN_PASSWORD_LENGTH} à{" "}
        {maxPasswordLength} caractères. Ce lien est à usage unique et constitue
        la dernière étape avant l&apos;accès à votre espace client.
      </p>

      <SubmitButton
        idleLabel="Définir le mot de passe"
        isSubmitting={state.status === "submitting"}
        submittingLabel="Enregistrement..."
      />
    </form>
  );
}
