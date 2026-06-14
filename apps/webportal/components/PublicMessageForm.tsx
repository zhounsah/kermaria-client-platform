"use client";

import type { RequestMutationResponse, RequestType } from "@kermaria/shared";
import { FormEvent, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type PublicMessageFormProps = {
  requestId: string;
  requestType: RequestType;
};

export function PublicMessageForm({
  requestId,
  requestType,
}: PublicMessageFormProps) {
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
        text: "Le message doit contenir au moins 3 caractères.",
      });
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);
    const result = await requestBffJson<RequestMutationResponse>(
      `/api/admin/${requestType === "support" ? "support-requests" : "service-requests"}/${encodeURIComponent(requestId)}/messages`,
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
        text: "Le message est maintenant visible dans l’espace client.",
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
        <label htmlFor="public-message">Message au client</label>
        <textarea
          id="public-message"
          maxLength={2000}
          onChange={(event) => setText(event.target.value)}
          rows={5}
          value={text}
        />
        <span className="field-hint">
          Ce texte sera visible par le client sous le libellé « Équipe
          Kermaria ». Aucun e-mail ne sera envoyé.
        </span>
      </div>
      {message ? (
        <FormMessage
          title={message.tone === "success" ? "Message publié" : "Échec"}
          tone={message.tone}
        >
          <p>{message.text}</p>
        </FormMessage>
      ) : null}
      <SubmitButton
        idleLabel="Publier le message"
        isSubmitting={isSubmitting}
        submittingLabel="Publication…"
      />
    </form>
  );
}
