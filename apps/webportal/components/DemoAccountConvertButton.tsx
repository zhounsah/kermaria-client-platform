"use client";

import type { DemoConversionResult } from "@kermaria/shared";
import { useState } from "react";
import { useRouter } from "next/navigation";

import { requestBffJson } from "@/lib/client-api";

type DemoAccountConvertButtonProps = {
  customerReference: string;
  displayName: string;
};

export function DemoAccountConvertButton({
  customerReference,
  displayName,
}: DemoAccountConvertButtonProps) {
  const router = useRouter();
  const [isConverting, setIsConverting] = useState(false);

  async function handleConvert() {
    if (isConverting) {
      return;
    }

    const codes = window.prompt(
      `Convertir « ${displayName} » en client réel.\n\n`
        + "Codes de services Billing V2 à accorder, séparés par des virgules "
        + "(ex. RDS-ACCESS,VPN-ACCESS) — laisser vide pour seulement retirer "
        + "l'accès de démonstration :",
      "",
    );
    // prompt renvoie null si l'admin annule ; une chaîne vide reste un choix
    // valide (conversion sans service accordé).
    if (codes === null) {
      return;
    }

    const serviceCodes = codes
      .split(",")
      .map((value) => value.trim())
      .filter((value) => value.length > 0);

    setIsConverting(true);
    const result = await requestBffJson<DemoConversionResult>(
      `/api/admin/demo/accounts/${encodeURIComponent(customerReference)}/convert`,
      {
        method: "POST",
        body: JSON.stringify({ serviceCodes }),
      },
    );
    setIsConverting(false);

    if (!result.ok) {
      window.alert(result.error.message);
      return;
    }

    if (result.data.alreadyConverted) {
      window.alert("Ce compte avait déjà été converti.");
    } else if (!result.data.converted) {
      window.alert(
        "Conversion incomplète côté Active Directory : rien n'a été basculé en base. "
          + "Corrigez la configuration puis relancez, l'opération est rejouable.",
      );
    }

    router.refresh();
  }

  return (
    <button
      className="table-action"
      disabled={isConverting}
      onClick={handleConvert}
      type="button"
    >
      {isConverting ? "Conversion…" : "Convertir"}
    </button>
  );
}
