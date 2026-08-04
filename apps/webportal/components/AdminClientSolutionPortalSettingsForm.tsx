"use client";

import type {
  ClientSolutionPortalMutationResponse,
  ClientSolutionPortalSettings,
  ClientSolutionPortalSettingsPayload,
} from "@kermaria/shared";
import { FormEvent, startTransition, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type AdminClientSolutionPortalSettingsFormProps = {
  settings: ClientSolutionPortalSettings;
};

export function AdminClientSolutionPortalSettingsForm({
  settings,
}: AdminClientSolutionPortalSettingsFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [formState, setFormState] = useState({
    eyebrow: settings.eyebrow ?? "",
    title: settings.title,
    description: settings.description ?? "",
    footerNote: settings.footerNote ?? "",
  });
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    title: string;
    text: string;
  } | null>(null);

  function updateField(key: keyof typeof formState, value: string) {
    setFormState((current) => ({ ...current, [key]: value }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current) {
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const payload: ClientSolutionPortalSettingsPayload = {
      eyebrow: formState.eyebrow.trim() || null,
      title: formState.title.trim(),
      description: formState.description.trim() || null,
      footerNote: formState.footerNote.trim() || null,
    };

    const result =
      await requestBffJson<ClientSolutionPortalMutationResponse>(
        "/api/admin/client-solutions/settings",
        {
          method: "PATCH",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(payload),
        },
      );

    if (result.ok) {
      setMessage({
        tone: "success",
        title: "En-tête enregistré",
        text: "Le texte affiché en haut de la page publique a été mis à jour.",
      });
      startTransition(() => router.refresh());
    } else {
      setMessage({
        tone: "error",
        title: "Enregistrement impossible",
        text: result.error.message,
      });
    }

    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  return (
    <form className="form-card" onSubmit={handleSubmit}>
      <div className="section-heading">
        <div>
          <span className="card-kicker">Page publique</span>
          <h2>En-tête de la page Solutions</h2>
          <p>
            Ces textes s&apos;affichent au-dessus des tuiles sur
            {" "}
            <code>/solutions</code>.
          </p>
        </div>
      </div>

      <div className="form-grid">
        <label>
          Surtitre
          <input
            maxLength={120}
            onChange={(event) => updateField("eyebrow", event.target.value)}
            placeholder="Ex. Portail de services"
            value={formState.eyebrow}
          />
        </label>

        <label>
          Titre
          <input
            maxLength={160}
            onChange={(event) => updateField("title", event.target.value)}
            placeholder="Ex. Accéder à mes solutions"
            required
            value={formState.title}
          />
        </label>
      </div>

      <label>
        Texte d&apos;introduction
        <textarea
          maxLength={600}
          onChange={(event) => updateField("description", event.target.value)}
          rows={3}
          value={formState.description}
        />
      </label>

      <label>
        Note de bas de page
        <textarea
          maxLength={600}
          onChange={(event) => updateField("footerNote", event.target.value)}
          rows={2}
          value={formState.footerNote}
        />
      </label>

      {message ? (
        <FormMessage title={message.title} tone={message.tone}>
          <p>{message.text}</p>
        </FormMessage>
      ) : null}

      <div className="stack-row">
        <SubmitButton
          idleLabel="Enregistrer l'en-tête"
          isSubmitting={isSubmitting}
          submittingLabel="Enregistrement..."
        />
      </div>
    </form>
  );
}
