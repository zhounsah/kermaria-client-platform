"use client";

import type { SubscriptionProvisioningReconcilePayload } from "@kermaria/shared";
import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";

import { requestBffJson } from "@/lib/client-api";

type AdminReconcileProvisioningButtonProps = {
  subscriptionId: string;
  authoritativeBillingV2?: boolean;
  disabled?: boolean;
  idleLabel?: string;
  submittingLabel?: string;
  targetUserSamAccountNames?: string[] | null;
};

export function AdminReconcileProvisioningButton({
  subscriptionId,
  authoritativeBillingV2,
  disabled,
  idleLabel = "Relancer le provisioning",
  submittingLabel = "Relance...",
  targetUserSamAccountNames,
}: AdminReconcileProvisioningButtonProps) {
  const router = useRouter();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isRefreshing, startRefresh] = useTransition();
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const isBusy = isSubmitting || isRefreshing;

  async function handleClick() {
    if (isBusy || disabled) {
      return;
    }

    setIsSubmitting(true);
    setError(null);
    setNotice(null);
    try {
      const payload: SubscriptionProvisioningReconcilePayload | undefined =
        !authoritativeBillingV2
          && targetUserSamAccountNames
          && targetUserSamAccountNames.length > 0
          ? { targetUserSamAccountNames }
          : undefined;
      const reconcileEndpoint: `/api/${string}` = authoritativeBillingV2
        ? `/api/admin/billing-v2/subscriptions/${encodeURIComponent(subscriptionId)}/provisioning/reconcile`
        : `/api/admin/subscriptions/${encodeURIComponent(subscriptionId)}/provisioning/reconcile`;
      const result = await requestBffJson<{ succeeded?: boolean; resultCode?: string }>(
        reconcileEndpoint,
        payload
          ? {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify(payload),
            }
          : { method: "POST" },
      );

      if (result.ok) {
        if (authoritativeBillingV2 && result.data.resultCode === "KOXO_QUALITIES_PENDING") {
          setNotice("Demande enregistrée. Importez les qualités dans KoXo en ne conservant que celles du CSV. Les accès seront ensuite vérifiés automatiquement.");
          startRefresh(() => router.refresh());
          return;
        }
        if (authoritativeBillingV2 && result.data.succeeded !== true) {
          setError(`La réconciliation n'a pas abouti : ${result.data.resultCode ?? "résultat indisponible"}. Vérifiez la préparation du client.`);
          return;
        }
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
      {notice ? <p className="field-hint" role="status" style={{ marginTop: 6 }}>{notice}</p> : null}
    </div>
  );
}
