"use client";

import type { RequestMutationResponse, RequestType } from "@kermaria/shared";
import { FormEvent, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type InternalNoteFormProps = {
  requestId: string;
  requestType: RequestType;
};

export function InternalNoteForm({
  requestId,
  requestType,
}: InternalNoteFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [text, setText] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalized = text.trim();
    if (isSubmittingRef.current || normalized.length < 3) {
      setMessage({
        tone: "error",
        text: "La note interne doit contenir au moins 3 caractères.",
      });
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);
    const result = await requestBffJson<RequestMutationResponse>(
      `/api/admin/${requestType === "support" ? "support-requests" : "service-requests"}/${encodeURIComponent(requestId)}/notes`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ text: normalized }),
      },
    );

    if (result.ok) {
      setText("");
      setMessage({
        tone: "success",
        text: "La note interne a été ajoutée.",
      });
      router.refresh();
    } else {
      setMessage({ tone: "error", text: result.error.message });
    }

    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  return (
    <form className="workflow-form" onSubmit={handleSubmit}>
      <div className="field">
        <label htmlFor="internal-note">Note interne</label>
        <textarea
          id="internal-note"
          maxLength={2000}
          onChange={(event) => setText(event.target.value)}
          rows={5}
          value={text}
        />
        <span className="field-hint">
          Visible uniquement par les administrateurs. Ne pas y inscrire de mot
          de passe, token ou secret.
        </span>
      </div>
      {message ? (
        <FormMessage
          title={message.tone === "success" ? "Note ajoutée" : "Échec"}
          tone={message.tone}
        >
          <p>{message.text}</p>
        </FormMessage>
      ) : null}
      <SubmitButton
        idleLabel="Ajouter la note interne"
        isSubmitting={isSubmitting}
        submittingLabel="Ajout…"
      />
    </form>
  );
}
