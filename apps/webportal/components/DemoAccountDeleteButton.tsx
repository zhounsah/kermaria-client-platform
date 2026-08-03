"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

import { requestBffJson } from "@/lib/client-api";

type DemoAccountDeleteButtonProps = {
  customerReference: string;
  displayName: string;
  kind: string;
};

export function DemoAccountDeleteButton({
  customerReference,
  displayName,
  kind,
}: DemoAccountDeleteButtonProps) {
  const router = useRouter();
  const [isDeleting, setIsDeleting] = useState(false);

  async function handleDelete() {
    if (isDeleting) {
      return;
    }

    const confirmed = window.confirm(
      `Supprimer définitivement « ${displayName} » (${customerReference}) ?\n\n`
        + (kind === "trial"
          ? "L'accès Active Directory sera révoqué (retrait des groupes de "
            + "démonstration et désactivation du compte) avant la suppression.\n\n"
          : "")
        + "Le compte, son utilisateur et ses services seront supprimés. "
        + "Cette action est irréversible.",
    );
    if (!confirmed) {
      return;
    }

    setIsDeleting(true);
    const result = await requestBffJson<unknown>(
      `/api/admin/demo/accounts/${encodeURIComponent(customerReference)}`,
      { method: "DELETE" },
    );
    setIsDeleting(false);

    if (!result.ok) {
      window.alert(result.error.message);
      return;
    }

    router.refresh();
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
