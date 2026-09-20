"use client";

import type {
  AdminCustomerCreatePayload,
  AdminCustomerCreateResponse,
} from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { FormEvent, useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

const initialForm: AdminCustomerCreatePayload = {
  customerType: "professional",
  displayName: "",
  billingEmail: "",
  phone: "",
  addressLine1: "",
  addressLine2: "",
  postalCode: "",
  city: "",
  country: "France",
};

/** Cree exclusivement la fiche client : aucun mot de passe ni invitation. */
export function AdminCustomerCreateForm() {
  const router = useRouter();
  const submitting = useRef(false);
  const [form, setForm] = useState(initialForm);
  const [message, setMessage] = useState<{ tone: "error" | "success"; text: string } | null>(null);
  const [pending, setPending] = useState(false);

  function update<K extends keyof AdminCustomerCreatePayload>(key: K, value: AdminCustomerCreatePayload[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting.current) return;

    submitting.current = true;
    setPending(true);
    setMessage(null);
    const payload: AdminCustomerCreatePayload = {
      ...form,
      displayName: form.displayName.trim(),
      billingEmail: form.billingEmail.trim(),
      phone: form.phone?.trim() || null,
      addressLine1: form.addressLine1.trim(),
      addressLine2: form.addressLine2?.trim() || null,
      postalCode: form.postalCode.trim(),
      city: form.city.trim(),
      country: form.country.trim(),
    };

    const result = await requestBffJson<AdminCustomerCreateResponse>(
      "/api/admin/customers",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );
    if (result.ok) {
      setMessage({
        tone: "success",
        text: `La fiche ${result.data.customerReference} a été créée. Aucun accès portail n’a été envoyé.`,
      });
      setPending(false);
      submitting.current = false;
      router.refresh();
      return;
    }

    setMessage({ tone: "error", text: result.error.message });
    submitting.current = false;
    setPending(false);
  }

  return <form className="form-card" onSubmit={submit}>
    <p className="form-help">
      Cette action crée uniquement la fiche client. Aucun accès portail, mot de passe,
      e-mail, paiement ou provisioning n’est créé.
    </p>
    <div className="form-grid">
      <label>Type de client
        <select value={form.customerType} onChange={(event) => update("customerType", event.target.value as AdminCustomerCreatePayload["customerType"])}>
          <option value="individual">Particulier</option>
          <option value="professional">Professionnel</option>
          <option value="association">Association</option>
        </select>
      </label>
      <label>Nom ou raison sociale
        <input required maxLength={200} value={form.displayName} onChange={(event) => update("displayName", event.target.value)} />
      </label>
    </div>
    <div className="form-grid">
      <label>Adresse e-mail de facturation
        <input required type="email" maxLength={320} value={form.billingEmail} onChange={(event) => update("billingEmail", event.target.value)} />
      </label>
      <label>Téléphone
        <input maxLength={40} value={form.phone ?? ""} onChange={(event) => update("phone", event.target.value)} />
      </label>
    </div>
    <label>Adresse
      <input required maxLength={255} value={form.addressLine1} onChange={(event) => update("addressLine1", event.target.value)} />
    </label>
    <label>Complément d’adresse
      <input maxLength={255} value={form.addressLine2 ?? ""} onChange={(event) => update("addressLine2", event.target.value)} />
    </label>
    <div className="form-grid">
      <label>Code postal
        <input required maxLength={32} value={form.postalCode} onChange={(event) => update("postalCode", event.target.value)} />
      </label>
      <label>Ville
        <input required maxLength={160} value={form.city} onChange={(event) => update("city", event.target.value)} />
      </label>
      <label>Pays
        <input required maxLength={100} value={form.country} onChange={(event) => update("country", event.target.value)} />
      </label>
    </div>
    {message ? <FormMessage title={message.tone === "error" ? "Création impossible" : "Création client"} tone={message.tone}>{message.text}</FormMessage> : null}
    <SubmitButton
      idleLabel="Créer la fiche client"
      isSubmitting={pending}
      submittingLabel="Création…"
    />
  </form>;
}
