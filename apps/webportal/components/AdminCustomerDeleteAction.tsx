"use client";

import type { AdminCustomerDeleteResponse } from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { requestBffJson } from "@/lib/client-api";

type Props = {
  customerReference: string;
  displayName: string;
  email: string | null;
};

/** Mutation admin distincte : aucune suppression n'est déclenchée au premier clic. */
export function AdminCustomerDeleteAction({ customerReference, displayName, email }: Props) {
  const router = useRouter();
  const submitting = useRef(false);
  const [confirming, setConfirming] = useState(false);
  const [pending, setPending] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  async function remove() {
    if (submitting.current) return;
    submitting.current = true;
    setPending(true);
    setMessage(null);
    const result = await requestBffJson<AdminCustomerDeleteResponse>(
      `/api/admin/customers/${encodeURIComponent(customerReference)}`,
      { method: "DELETE" },
    );
    submitting.current = false;
    setPending(false);

    if (!result.ok) {
      setMessage(result.error.message);
      return;
    }

    router.replace("/admin/customers");
    router.refresh();
  }

  if (!confirming) {
    return (
      <button className="button button-danger" onClick={() => setConfirming(true)} type="button">
        Supprimer le client
      </button>
    );
  }

  return (
    <section className="form-card admin-customer-delete-confirmation" role="dialog" aria-modal="true" aria-labelledby="delete-customer-title">
      <h2 id="delete-customer-title">Supprimer définitivement ce client ?</h2>
      <p>
        <strong>{displayName}</strong>{email ? <> · {email}</> : null}
      </p>
      <p>
        Cette action n’est possible que si aucune donnée commerciale, financière,
        technique ou d’accès portail n’est rattachée à cette fiche.
      </p>
      {message ? <FormMessage title="Suppression impossible" tone="error">{message}</FormMessage> : null}
      <div className="form-actions">
        <button className="button button-secondary" disabled={pending} onClick={() => setConfirming(false)} type="button">
          Annuler
        </button>
        <button className="button button-danger" disabled={pending} onClick={() => void remove()} type="button">
          {pending ? "Suppression…" : "Supprimer définitivement"}
        </button>
      </div>
    </section>
  );
}
