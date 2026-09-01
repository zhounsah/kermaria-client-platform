"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

import { requestBffJson } from "@/lib/client-api";

type AdminApproveVpsTechnicalReviewButtonProps = {
  technicalRequestId: string;
};

/** Approbation humaine uniquement. Cette action ne lance aucun provisioning. */
export function AdminApproveVpsTechnicalReviewButton({
  technicalRequestId,
}: AdminApproveVpsTechnicalReviewButtonProps) {
  const router = useRouter();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleClick() {
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError(null);
    try {
      const result = await requestBffJson<Record<string, unknown>>(
        `/api/admin/billing-v2/vps/technical-reviews/${encodeURIComponent(technicalRequestId)}/approve`,
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

  return (
    <div>
      <button
        className="button"
        disabled={isSubmitting}
        onClick={handleClick}
        type="button"
      >
        {isSubmitting ? "Approbation..." : "Approuver"}
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
