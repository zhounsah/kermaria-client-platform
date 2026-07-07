"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";

import { requestBffJson } from "@/lib/client-api";

type AdminReconcileProvisioningButtonProps = {
  subscriptionId: string;
  disabled?: boolean;
};

export function AdminReconcileProvisioningButton({
  subscriptionId,
  disabled,
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
      const result = await requestBffJson(
        `/api/admin/subscriptions/${encodeURIComponent(subscriptionId)}/provisioning/reconcile`,
        { method: "POST" },
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
        {isBusy ? "Relance..." : "Relancer le provisioning"}
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
