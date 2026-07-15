"use client";

import type { SubscriptionProvisioningReconcilePayload } from "@kermaria/shared";
import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";

import { requestBffJson } from "@/lib/client-api";

type AdminReconcileProvisioningButtonProps = {
  subscriptionId: string;
  disabled?: boolean;
  idleLabel?: string;
  submittingLabel?: string;
  targetUserSamAccountNames?: string[] | null;
};

export function AdminReconcileProvisioningButton({
  subscriptionId,
  disabled,
  idleLabel = "Relancer le provisioning",
  submittingLabel = "Relance...",
  targetUserSamAccountNames,
}: AdminReconcileProvisioningButtonProps) {
  const router = useRouter();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isRefreshing, startRefresh] = useTransition();
  const [error, setError] = useState<string | null>(null);
  const isBusy = isSubmitting || isRefreshing;

  async function handleClick() {
    if (isBusy || disabled) {
      return;
    }

    setIsSubmitting(true);
    setError(null);
    try {
      const payload: SubscriptionProvisioningReconcilePayload | undefined =
        targetUserSamAccountNames && targetUserSamAccountNames.length > 0
          ? { targetUserSamAccountNames }
          : undefined;
      const result = await requestBffJson(
        `/api/admin/subscriptions/${encodeURIComponent(subscriptionId)}/provisioning/reconcile`,
        payload
          ? {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify(payload),
            }
          : { method: "POST" },
      );

      if (result.ok) {
        startRefresh(() => {
          router.refresh();
        });
        return;
      }

      setError(result.error.message);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div>
      <button
        className="button button-secondary"
        disabled={isBusy || disabled}
        onClick={handleClick}
        type="button"
      >
        {isBusy ? submittingLabel : idleLabel}
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
