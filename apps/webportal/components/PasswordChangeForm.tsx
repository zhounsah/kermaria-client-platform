"use client";

import type {
  PortalPasswordChangePayload,
  PortalPasswordChangeResponse,
} from "@kermaria/shared";
import { FormEvent, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

export function PasswordChangeForm() {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!currentPassword || !newPassword || !confirmation) {
      setMessage({
        tone: "error",
        text: "Tous les champs sont obligatoires.",
      });
      return;
    }

    if (newPassword !== confirmation) {
      setMessage({
        tone: "error",
        text: "La confirmation ne correspond pas au nouveau mot de passe.",
      });
      return;
    }

    if (newPassword === currentPassword) {
      setMessage({
        tone: "error",
        text: "Le nouveau mot de passe doit etre different du mot de passe actuel.",
      });
      return;
    }

    setIsSubmitting(true);
    setMessage(null);

    const payload: PortalPasswordChangePayload = {
      currentPassword,
      newPassword,
    };
    const result = await requestBffJson<PortalPasswordChangeResponse>(
      "/api/profile/password",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );

    if (result.ok) {
      setMessage({
        tone: "success",
        text: "Mot de passe Active Directory mis a jour.",
      });
      setCurrentPassword("");
      setNewPassword("");
      setConfirmation("");
    } else {
      setMessage({ tone: "error", text: result.error.message });
    }

    setIsSubmitting(false);
  }

  return (
    <form
      autoComplete="off"
      className="form-card"
      onSubmit={handleSubmit}
    >
      <label>
        Mot de passe actuel
        <input
          autoComplete="current-password"
          onChange={(event) => setCurrentPassword(event.target.value)}
          required
          type="password"
          value={currentPassword}
        />
      </label>
      <label>
        Nouveau mot de passe
        <input
          autoComplete="new-password"
          onChange={(event) => setNewPassword(event.target.value)}
          required
          type="password"
          value={newPassword}
        />
      </label>
      <label>
        Confirmation du nouveau mot de passe
        <input
          autoComplete="new-password"
          onChange={(event) => setConfirmation(event.target.value)}
          required
          type="password"
          value={confirmation}
        />
      </label>
      <p className="field-hint">
        La politique de complexite et d&apos;historique du mot de passe est
        controlee par votre domaine Active Directory. Aucune regle locale
        n&apos;est appliquee cote portail.
      </p>
      {message ? (
        <FormMessage
          title={message.tone === "success" ? "Mot de passe change" : "Echec"}
          tone={message.tone}
        >
          <p>{message.text}</p>
        </FormMessage>
      ) : null}
      <SubmitButton
        idleLabel="Changer le mot de passe"
        isSubmitting={isSubmitting}
        submittingLabel="Envoi..."
      />
    </form>
  );
}
