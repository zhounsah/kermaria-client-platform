"use client";

import type { RequestMutationResponse, RequestType } from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { FormEvent, useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type ClientReplyFormProps = {
  requestId: string;
  requestType: RequestType;
};

export function ClientReplyForm({
  requestId,
  requestType,
}: ClientReplyFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [text, setText] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);
  const fieldId = `${requestType}-client-reply`;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalized = text.trim();
    if (isSubmittingRef.current) {
      return;
    }

    if (normalized.length < 3 || normalized.length > 2000) {
      setMessage({
        tone: "error",
        text: "Votre réponse doit contenir entre 3 et 2 000 caractères.",
      });
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);
    const route = requestType === "support"
      ? "support-requests"
      : "service-requests";
    const result = await requestBffJson<RequestMutationResponse>(
      `/api/${route}/${encodeURIComponent(requestId)}/messages`,
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
        text: "Votre message a été ajouté à la demande.",
      });
      router.refresh();
    } else {
      setMessage({
        tone: "error",
        text: "Impossible d’envoyer votre réponse pour le moment. Réessayez dans quelques instants.",
      });
    }

    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  return (
    <form className="workflow-form client-reply-form" onSubmit={handleSubmit}>
      <div className="field">
        <label htmlFor={fieldId}>Répondre à cette demande</label>
        <textarea
          id={fieldId}
          maxLength={2000}
          onChange={(event) => setText(event.target.value)}
          rows={5}
          value={text}
        />
        <span className="field-hint">
          Votre réponse sera visible par l’équipe Kermaria. Aucun e-mail ne
          sera envoyé depuis cette version du portail.
        </span>
      </div>
      {message ? (
        <FormMessage
          title={message.tone === "success" ? "Réponse ajoutée" : "Échec"}
          tone={message.tone}
        >
          <p>{message.text}</p>
        </FormMessage>
      ) : null}
      <SubmitButton
        idleLabel="Ajouter ma réponse"
        isSubmitting={isSubmitting}
        submittingLabel="Envoi…"
      />
    </form>
  );
}
