"use client";

import type { BillingV2ProvisioningReadinessReviewResult } from "@kermaria/shared";
import { useRef, useState } from "react";
import { requestBffJson } from "@/lib/client-api";

export function AdminProvisioningReadinessReview({ customerId }: { customerId: string }) {
  const inFlight = useRef(false);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<BillingV2ProvisioningReadinessReviewResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function review() {
    if (inFlight.current) return;
    inFlight.current = true;
    setBusy(true);
    setError(null);
    setResult(null);
    try {
      const response = await requestBffJson<BillingV2ProvisioningReadinessReviewResult>(
        `/api/admin/billing-v2/provisioning-readiness/${encodeURIComponent(customerId)}/review`,
        { method: "POST" },
      );
      if (response.ok) setResult(response.data);
      else setError(response.error.message);
    } finally {
      inFlight.current = false;
      setBusy(false);
    }
  }

  return <div>
    <p className="field-hint">Cette revue contrôle et enregistre la préparation du client. La réconciliation des ressources se lance séparément.</p>
    <button className="button button-secondary" disabled={busy} onClick={review} type="button">
      {busy ? "Vérification..." : "Vérifier la préparation du client"}
    </button>
    {error ? <p role="alert">{error}</p> : null}
    {result ? <div role="status">
      <p>{result.ready && result.persisted ? "Préparation validée et enregistrée." : "Préparation non validée."}</p>
      <p>{result.desiredAdGroupCount} groupe(s) AD et {result.storageTargetCount} cible(s) de stockage examinés.</p>
      {!result.ready || !result.persisted ? <p>{result.reasonCodes.join(" · ") || result.reasonCode}</p> : null}
    </div> : null}
  </div>;
}
