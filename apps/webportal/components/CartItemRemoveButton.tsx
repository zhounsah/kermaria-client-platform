"use client";

import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import { SubmitButton } from "@/components/SubmitButton";
import type { CartMutationResponse } from "@kermaria/shared";
import { requestBffJson } from "@/lib/client-api";

type CartItemRemoveButtonProps = {
  offerId: string;
};

export function CartItemRemoveButton({ offerId }: CartItemRemoveButtonProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleRemove() {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    setIsSubmitting(true);

    const result = await requestBffJson<CartMutationResponse>(
      "/api/cart/items/remove",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ offerId }),
      },
    );

    if (result.ok) {
      router.refresh();
    } else {
      isSubmittingRef.current = false;
      setIsSubmitting(false);
    }
  }

  return (
    <SubmitButton
      className="button button-ghost"
      idleLabel="Retirer"
      isSubmitting={isSubmitting}
      submittingLabel="…"
      onClick={handleRemove}
      type="button"
    />
  );
}
