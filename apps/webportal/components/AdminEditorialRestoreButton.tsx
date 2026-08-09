"use client";

import type {
  EditorialContentType,
  EditorialMutationResponse,
} from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import { requestBffJson } from "@/lib/client-api";

type AdminEditorialRestoreButtonProps = {
  revisionId: string;
  contentType: EditorialContentType;
};

export function AdminEditorialRestoreButton({
  revisionId,
  contentType,
}: AdminEditorialRestoreButtonProps) {
  const router = useRouter();
  const submittingRef = useRef(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function restore() {
    if (submittingRef.current) {
      return;
    }

    if (!window.confirm("Restaurer cette version précédente ?")) {
      return;
    }

    submittingRef.current = true;
    setIsSubmitting(true);
    const result = await requestBffJson<EditorialMutationResponse>(
      `/api/admin/editorial/revisions/${encodeURIComponent(revisionId)}/restore`,
      { method: "POST" },
    );
    setIsSubmitting(false);
    submittingRef.current = false;

    if (result.ok) {
      router.push(
        `/admin/editorial/${segmentFor(contentType)}/${encodeURIComponent(result.data.id)}`,
      );
      router.refresh();
      return;
    }

    window.alert(result.error.message);
  }

  return (
    <button
      className="button button-secondary"
      disabled={isSubmitting}
      onClick={() => void restore()}
      type="button"
    >
      {isSubmitting ? "Restauration..." : "Restaurer cette version"}
    </button>
  );
}

function segmentFor(contentType: EditorialContentType) {
  return contentType === "wiki_article"
    ? "wiki"
    : contentType === "seo_page"
      ? "seo"
      : "faq";
}
