"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

import { requestBffJson } from "@/lib/client-api";

type AdminCancelSubscriptionButtonProps = {
  subscriptionId: string;
  disabled?: boolean;
};

export function AdminCancelSubscriptionButton({
  subscriptionId,
  disabled,
}: AdminCancelSubscriptionButtonProps) {
  const router = useRouter();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleClick() {
    if (isSubmitting || disabled) {
      return;
    }
    if (
      typeof window !== "undefined"
      && !window.confirm(
        "Demander la résiliation de cette souscription ? Si un terme est en cours, elle prendra effet à son échéance.",
      )
    ) {
      return;
    }

    setIsSubmitting(true);
    setError(null);

    const result = await requestBffJson(
      `/api/admin/subscriptions/${encodeURIComponent(subscriptionId)}/cancel`,
      { method: "POST" },
    );

    if (result.ok) {
      router.refresh();
    } else {
      setError(result.error.message);
    }

    setIsSubmitting(false);
  }

  return (
    <div>
      <button
        className="button"
        disabled={isSubmitting || disabled}
        onClick={handleClick}
        type="button"
      >
        {isSubmitting ? "Résiliation..." : "Demander la résiliation"}
      </button>
      {error ? (
        <p
          className="field-hint"
          role="alert"
          style={{ marginTop: 6, color: "var(--danger)" }}
        >
          {error}
        </p>
      ) : null}
    </div>
  );
}
