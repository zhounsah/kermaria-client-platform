"use client";

import type { SubscriptionSummary } from "@kermaria/shared";
import { useState } from "react";

import { requestBffJson } from "@/lib/client-api";

type ClientCancelSubscriptionButtonProps = {
  subscriptionId: string;
  disabled?: boolean;
};

export function ClientCancelSubscriptionButton({
  subscriptionId,
  disabled,
}: ClientCancelSubscriptionButtonProps) {
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

    const result = await requestBffJson<SubscriptionSummary>(
      `/api/subscriptions/${encodeURIComponent(subscriptionId)}/cancel`,
      { method: "POST" },
    );

    if (result.ok) {
      const flash =
        result.data.status === "pending_cancellation"
          ? "scheduled"
          : "terminated";
      window.location.assign(`/profile/subscriptions?subscription=${flash}`);
      return;
    }

    setError(result.error.message);
    setIsSubmitting(false);
  }

  return (
    <div>
      <button
        className="button button-secondary"
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
