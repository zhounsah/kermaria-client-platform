"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

import { requestBffJson } from "@/lib/client-api";

type DemoProfileDeleteButtonProps = {
  profileKey: string;
};

export function DemoProfileDeleteButton({
  profileKey,
}: DemoProfileDeleteButtonProps) {
  const router = useRouter();
  const [isDeleting, setIsDeleting] = useState(false);

  async function handleDelete() {
    if (isDeleting) {
      return;
    }

    const confirmed = window.confirm(
      `Supprimer le profil « ${profileKey} » ? Les comptes déjà créés ne sont pas affectés.`,
    );
    if (!confirmed) {
      return;
    }

    setIsDeleting(true);
    const result = await requestBffJson<{ deleted: boolean }>(
      `/api/admin/demo/profiles/${encodeURIComponent(profileKey)}`,
      { method: "DELETE" },
    );
    setIsDeleting(false);

    if (result.ok) {
      router.refresh();
    } else {
      window.alert(result.error.message);
    }
  }

  return (
    <button
      className="table-action"
      disabled={isDeleting}
      onClick={handleDelete}
      type="button"
    >
      {isDeleting ? "Suppression…" : "Supprimer"}
    </button>
  );
}
