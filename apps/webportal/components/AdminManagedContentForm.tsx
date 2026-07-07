"use client";

import type {
  ManagedContentDetail,
  ManagedContentMutationResponse,
  ManagedContentPayload,
} from "@kermaria/shared";
import { FormEvent, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { ManagedMarkdown } from "@/components/ManagedMarkdown";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type AdminManagedContentFormProps = {
  content: ManagedContentDetail;
};

export function AdminManagedContentForm({
  content,
}: AdminManagedContentFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [bodyMarkdown, setBodyMarkdown] = useState(content.bodyMarkdown);
  const [versionLabel, setVersionLabel] = useState(content.versionLabel ?? "");
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current) {
      return;
    }

    const payload: ManagedContentPayload = {
      bodyMarkdown: bodyMarkdown.trim(),
      versionLabel: versionLabel.trim() || null,
    };

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<ManagedContentMutationResponse>(
      `/api/admin/content/${encodeURIComponent(content.key)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );

    if (result.ok) {
      setMessage({
        tone: "success",
        text: result.data.changed
          ? "Le contenu a été enregistré."
          : "Aucune modification supplémentaire n'a été détectée.",
      });
      router.refresh();
    } else {
      setMessage({
        tone: "error",
        text: result.error.message,
      });
    }

    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  return (
    <form className="form-card managed-content-form" onSubmit={handleSubmit}>
      <div className="managed-content-editor-grid">
        <div className="managed-content-editor-column">
          <label>
            Version publique (optionnel)
            <input
              maxLength={160}
              onChange={(event) => setVersionLabel(event.target.value)}
              placeholder="Ex. Version du : 07 juillet 2026"
              value={versionLabel}
            />
          </label>

          <label>
            Contenu Markdown
            <textarea
              maxLength={120000}
              onChange={(event) => setBodyMarkdown(event.target.value)}
              rows={28}
              value={bodyMarkdown}
            />
          </label>
        </div>

        <div className="managed-content-preview-card">
          <div className="managed-content-preview-header">
            <span className="card-kicker">Aperçu rendu</span>
            <h3>{content.title}</h3>
          </div>

          {versionLabel.trim() ? (
            <p className="managed-content-preview-meta">
              {versionLabel.trim()}
            </p>
          ) : null}

          <ManagedMarkdown markdown={bodyMarkdown || "_Aucun contenu._"} />
        </div>
      </div>

      {message ? (
        <FormMessage
          title={message.tone === "success" ? "Enregistrement" : "Erreur"}
          tone={message.tone}
        >
          <p>{message.text}</p>
        </FormMessage>
      ) : null}

      <div className="stack-row">
        <SubmitButton
          idleLabel="Enregistrer le contenu"
          isSubmitting={isSubmitting}
          submittingLabel="Enregistrement..."
        />
      </div>
    </form>
  );
}
