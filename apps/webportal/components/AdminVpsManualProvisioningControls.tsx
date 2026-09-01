"use client";

import type { BillingV2VpsManualProvisioningPayload } from "@kermaria/shared";
import { type FormEvent, type ReactNode, useState } from "react";
import { useRouter } from "next/navigation";

import { requestBffJson } from "@/lib/client-api";

type Props = {
  technicalRequestId: string;
  provisioningStatus: string;
};

/**
 * Surface exclusivement manuelle : elle consigne une intervention deja faite
 * par l'exploitation. Elle ne contacte jamais une infrastructure VPS.
 */
export function AdminVpsManualProvisioningControls({
  technicalRequestId,
  provisioningStatus,
}: Props) {
  const router = useRouter();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState<BillingV2VpsManualProvisioningPayload>({
    infrastructureTarget: "",
    instanceReference: "",
    publicIpAddress: "",
    operationalNotes: "",
  });

  async function startProvisioning(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError(null);
    try {
      const result = await requestBffJson<Record<string, unknown>>(
        `/api/admin/billing-v2/vps/technical-reviews/${encodeURIComponent(technicalRequestId)}/manual-provisioning`,
        {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify(form),
        },
      );
      if (result.ok) {
        router.refresh();
      } else {
        setError(result.error.message);
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  async function markActive() {
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError(null);
    try {
      const result = await requestBffJson<Record<string, unknown>>(
        `/api/admin/billing-v2/vps/technical-reviews/${encodeURIComponent(technicalRequestId)}/manual-provisioning/activate`,
        { method: "POST" },
      );
      if (result.ok) {
        router.refresh();
      } else {
        setError(result.error.message);
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  if (provisioningStatus === "active") {
    return (
      <p className="vps-configurator-notice">
        VPS actif. Cette confirmation est une trace opérationnelle manuelle ;
        aucune action vers un provider n’a été exécutée par le portail.
      </p>
    );
  }

  if (provisioningStatus === "provisioning") {
    return (
      <ActionResult error={error}>
        <button
          className="button"
          disabled={isSubmitting}
          onClick={markActive}
          type="button"
        >
          {isSubmitting ? "Confirmation..." : "Marquer comme actif"}
        </button>
      </ActionResult>
    );
  }

  if (provisioningStatus !== "pending") {
    return (
      <p className="vps-configurator-notice">
        La mise en service nécessite une intervention d’exploitation. Aucun retry
        automatique n’est disponible dans cette première version.
      </p>
    );
  }

  return (
    <form className="vps-configuration-form" onSubmit={startProvisioning}>
      <div>
        <h3>Commencer la mise en service</h3>
        <p className="field-hint">
          Consignez seulement les références non sensibles de l’intervention
          manuelle. Aucun mot de passe, token ou clé privée ne doit être saisi.
        </p>
      </div>
      <div className="form-grid">
        <label className="form-field">
          <span>Infrastructure ou hôte cible</span>
          <input
            autoComplete="off"
            maxLength={255}
            onChange={(event) => setForm((current) => ({
              ...current,
              infrastructureTarget: event.target.value,
            }))}
            required
            value={form.infrastructureTarget}
          />
        </label>
        <label className="form-field">
          <span>Référence d’instance</span>
          <input
            autoComplete="off"
            maxLength={255}
            onChange={(event) => setForm((current) => ({
              ...current,
              instanceReference: event.target.value,
            }))}
            required
            value={form.instanceReference}
          />
        </label>
        <label className="form-field">
          <span>Adresse IP publique <em className="vps-field-optional">(facultative)</em></span>
          <input
            autoComplete="off"
            inputMode="text"
            maxLength={45}
            onChange={(event) => setForm((current) => ({
              ...current,
              publicIpAddress: event.target.value,
            }))}
            value={form.publicIpAddress}
          />
        </label>
        <label className="form-field vps-form-field-wide">
          <span>Notes opérationnelles <em className="vps-field-optional">(facultatives)</em></span>
          <textarea
            maxLength={2000}
            onChange={(event) => setForm((current) => ({
              ...current,
              operationalNotes: event.target.value,
            }))}
            rows={4}
            value={form.operationalNotes}
          />
        </label>
      </div>
      <ActionResult error={error}>
        <button className="button" disabled={isSubmitting} type="submit">
          {isSubmitting ? "Enregistrement..." : "Commencer la mise en service"}
        </button>
      </ActionResult>
    </form>
  );
}

function ActionResult({
  children,
  error,
}: {
  children: ReactNode;
  error: string | null;
}) {
  return (
    <div className="vps-configurator-actions">
      {children}
      {error ? (
        <p className="field-hint" role="alert" style={{ color: "var(--danger)" }}>
          {error}
        </p>
      ) : null}
    </div>
  );
}
