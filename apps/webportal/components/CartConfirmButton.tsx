"use client";

import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import type { CartConfirmResponse } from "@kermaria/shared";
import { requestBffJson } from "@/lib/client-api";

export function CartConfirmButton() {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleConfirm() {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setError(null);

    const result = await requestBffJson<CartConfirmResponse>(
      "/api/cart/confirm",
      { method: "POST" },
    );

    if (result.ok) {
      router.push(`/commercial-documents/${result.data.documentId}`);
    } else {
      setError(result.error.message);
      isSubmittingRef.current = false;
      setIsSubmitting(false);
    }
  }

  return (
    <div className="workflow-form">
      {error ? (
        <FormMessage title="Impossible de confirmer" tone="error">
          <p>{error}</p>
        </FormMessage>
      ) : null}
      <SubmitButton
        className="button"
        idleLabel="Confirmer ma commande"
        isSubmitting={isSubmitting}
        submittingLabel="Création de la commande…"
        onClick={handleConfirm}
        type="button"
      />
    </div>
  );
}
