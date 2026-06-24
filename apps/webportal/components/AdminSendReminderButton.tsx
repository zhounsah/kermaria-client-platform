"use client";

import { useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type SendReminderResult = {
  code: string;
  message: string;
};

type AdminSendReminderButtonProps = {
  documentId: string;
};

export function AdminSendReminderButton({
  documentId,
}: AdminSendReminderButtonProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);

  async function handleSend() {
    if (isSubmittingRef.current) return;
    if (
      !window.confirm(
        "Envoyer un e-mail de relance au client ? Le message est tracé dans le journal d'envoi.",
      )
    ) {
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<SendReminderResult>(
      `/api/admin/commercial-documents/${encodeURIComponent(documentId)}/send-reminder`,
      { method: "POST" },
    );

    if (result.ok) {
      setMessage({ tone: "success", text: result.data.message });
      router.refresh();
    } else {
      setMessage({ tone: "error", text: result.error.message });
    }

    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  return (
    <div className="stack-list">
      {message ? (
        <FormMessage
          title={message.tone === "success" ? "Relance envoyée" : "Échec"}
          tone={message.tone}
        >
          <p>{message.text}</p>
        </FormMessage>
      ) : null}
      <SubmitButton
        className="button button-secondary"
        idleLabel="Envoyer une relance e-mail"
        isSubmitting={isSubmitting}
        submittingLabel="Envoi en cours..."
        onClick={handleSend}
        type="button"
      />
    </div>
  );
}
