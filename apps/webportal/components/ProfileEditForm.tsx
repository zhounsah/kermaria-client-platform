"use client";

import type {
  ClientProfile,
  PortalProfileUpdatePayload,
  PortalProfileUpdateResponse,
} from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { FormEvent, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type ProfileEditFormProps = {
  profile: ClientProfile;
};

function toFields(profile: ClientProfile): PortalProfileUpdatePayload {
  return {
    contactName: profile.contactName ?? "",
    phone: profile.phone ?? "",
    address: profile.address ?? "",
    city: profile.city ?? "",
    country: profile.country ?? "",
  };
}

export function ProfileEditForm({ profile }: ProfileEditFormProps) {
  const router = useRouter();
  const [fields, setFields] = useState(() => toFields(profile));
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);

  function update(field: keyof PortalProfileUpdatePayload, value: string) {
    setFields((current) => ({ ...current, [field]: value }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const payload: PortalProfileUpdatePayload = {
      contactName: fields.contactName.trim(),
      phone: fields.phone.trim(),
      address: fields.address.trim(),
      city: fields.city.trim(),
      country: fields.country.trim(),
    };

    if (payload.contactName.length < 2) {
      setMessage({
        tone: "error",
        text: "Le nom du contact principal est obligatoire.",
      });
      return;
    }

    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<PortalProfileUpdateResponse>(
      "/api/profile",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );

    if (result.ok) {
      setFields(toFields(result.data.profile));
      setMessage({ tone: "success", text: result.data.message });
      // Le nom du contact alimente aussi l'en-tête et le tableau de bord :
      // on recharge les composants serveur pour éviter un affichage périmé.
      router.refresh();
    } else {
      setMessage({ tone: "error", text: result.error.message });
    }

    setIsSubmitting(false);
  }

  return (
    <form className="form-card" onSubmit={handleSubmit}>
      <label>
        Contact principal
        <input
          autoComplete="name"
          maxLength={200}
          onChange={(event) => update("contactName", event.target.value)}
          required
          type="text"
          value={fields.contactName}
        />
      </label>
      <div className="form-grid">
        <label>
          Téléphone
          <input
            autoComplete="tel"
            maxLength={40}
            onChange={(event) => update("phone", event.target.value)}
            type="tel"
            value={fields.phone}
          />
        </label>
        <label>
          Ville
          <input
            autoComplete="address-level2"
            maxLength={160}
            onChange={(event) => update("city", event.target.value)}
            type="text"
            value={fields.city}
          />
        </label>
      </div>
      <div className="form-grid">
        <label>
          Adresse
          <input
            autoComplete="street-address"
            maxLength={255}
            onChange={(event) => update("address", event.target.value)}
            type="text"
            value={fields.address}
          />
        </label>
        <label>
          Pays
          <input
            autoComplete="country-name"
            maxLength={100}
            onChange={(event) => update("country", event.target.value)}
            type="text"
            value={fields.country}
          />
        </label>
      </div>
      {message ? (
        <FormMessage
          title={
            message.tone === "success"
              ? "Coordonnées enregistrées"
              : "Enregistrement impossible"
          }
          tone={message.tone}
        >
          <p>{message.text}</p>
        </FormMessage>
      ) : null}
      <SubmitButton
        idleLabel="Enregistrer les modifications"
        isSubmitting={isSubmitting}
        submittingLabel="Enregistrement..."
      />
    </form>
  );
}
