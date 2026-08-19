"use client";

import type {
  BillingV2AdditionalUserAssignPayload,
  BillingV2AdditionalUserSlotStatus,
  BillingV2AdditionalUserSlotSummary,
} from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { StatusBadge } from "@/components/StatusBadge";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type ManagerProps = {
  subscriptionId: string;
  slots: BillingV2AdditionalUserSlotSummary[];
};

type MutationResponse = {
  code: string;
  message: string;
  correlation_id?: string;
};

/**
 * Etats presentes au client. Aucune notion technique : ni KoXo, ni annuaire,
 * ni code d'echec. Une activation qui echoue devient « a finaliser » et
 * renvoie vers le support, parce que le client n'a aucune action utile a
 * faire sur la cause reelle.
 */
const SLOT_STATUS_PRESENTATION: Record<
  BillingV2AdditionalUserSlotStatus,
  { label: string; tone: "success" | "warning" | "danger" | "neutral" | "info" }
> = {
  available: { label: "À attribuer", tone: "neutral" },
  invited: { label: "Invitation envoyée", tone: "info" },
  activating: { label: "Activation en cours", tone: "info" },
  active: { label: "Activé", tone: "success" },
  attention: { label: "Activation à finaliser", tone: "warning" },
  disabled: { label: "Désactivé", tone: "neutral" },
};

export function BillingV2AdditionalUsersManager({
  subscriptionId,
  slots,
}: ManagerProps) {
  const router = useRouter();
  const [openSlotId, setOpenSlotId] = useState<string | null>(null);
  const [pendingSlotId, setPendingSlotId] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<
    { slotId: string; tone: "success" | "error"; message: string } | null
  >(null);

  async function handleResend(slotId: string) {
    if (pendingSlotId) {
      return;
    }

    setPendingSlotId(slotId);
    setFeedback(null);

    const result = await requestBffJson<MutationResponse>(
      `/api/subscriptions/${encodeURIComponent(subscriptionId)}/users/${encodeURIComponent(slotId)}/resend-invitation`,
      { method: "POST" },
    );

    setPendingSlotId(null);

    if (!result.ok) {
      setFeedback({ slotId, tone: "error", message: result.error.message });
      return;
    }

    setFeedback({
      slotId,
      tone: "success",
      message:
        "Une nouvelle invitation a été envoyée. Le lien précédent n'est plus valable.",
    });
    router.refresh();
  }

  async function handleAssign(
    slotId: string,
    payload: BillingV2AdditionalUserAssignPayload,
  ) {
    if (pendingSlotId) {
      return;
    }

    setPendingSlotId(slotId);
    setFeedback(null);

    const result = await requestBffJson<MutationResponse>(
      `/api/subscriptions/${encodeURIComponent(subscriptionId)}/users/${encodeURIComponent(slotId)}/assign`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );

    setPendingSlotId(null);

    if (!result.ok) {
      setFeedback({ slotId, tone: "error", message: result.error.message });
      return;
    }

    setOpenSlotId(null);
    setFeedback({
      slotId,
      tone: "success",
      message:
        "Invitation envoyée. L'utilisateur recevra un e-mail pour définir son mot de passe.",
    });
    router.refresh();
  }

  return (
    <div className="stack-panel">
      {slots.map((slot, index) => {
        const presentation = SLOT_STATUS_PRESENTATION[slot.status];
        const slotFeedback =
          feedback && feedback.slotId === slot.id ? feedback : null;
        const isPending = pendingSlotId === slot.id;

        return (
          <article
            aria-label={`Place utilisateur ${index + 1}`}
            className="content-panel"
            key={slot.id}
          >
            <div className="section-heading">
              <div>
                <h3>{slot.displayName ?? `Place ${index + 1}`}</h3>
                <p>{slot.email ?? "Aucun utilisateur attribué"}</p>
              </div>
              <StatusBadge
                label={presentation.label}
                tone={presentation.tone}
              />
            </div>

            {slot.status === "attention" ? (
              <p className="field-hint">
                L&apos;activation de cet accès n&apos;a pas pu être finalisée.
                Notre équipe peut la reprendre : contactez le support en
                indiquant cette souscription.
              </p>
            ) : null}

            {slot.status === "activating" ? (
              <p className="field-hint">
                Les accès associés sont en cours de préparation. Cette étape
                peut prendre quelques minutes.
              </p>
            ) : null}

            {slotFeedback ? (
              <FormMessage
                title={
                  slotFeedback.tone === "success"
                    ? "Demande enregistrée"
                    : "Demande impossible"
                }
                tone={slotFeedback.tone}
              >
                <p>{slotFeedback.message}</p>
              </FormMessage>
            ) : null}

            {slot.canResendInvitation ? (
              <button
                className="button button-secondary"
                disabled={isPending}
                onClick={() => handleResend(slot.id)}
                type="button"
              >
                {isPending ? "Envoi..." : "Renvoyer l'invitation"}
              </button>
            ) : null}

            {slot.canAssign ? (
              openSlotId === slot.id ? (
                <AssignForm
                  isSubmitting={isPending}
                  onCancel={() => setOpenSlotId(null)}
                  onSubmit={(payload) => handleAssign(slot.id, payload)}
                />
              ) : (
                <button
                  className="button"
                  disabled={Boolean(pendingSlotId)}
                  onClick={() => {
                    setFeedback(null);
                    setOpenSlotId(slot.id);
                  }}
                  type="button"
                >
                  Ajouter un utilisateur
                </button>
              )
            ) : null}
          </article>
        );
      })}
    </div>
  );
}

type AssignFormProps = {
  isSubmitting: boolean;
  onCancel: () => void;
  onSubmit: (payload: BillingV2AdditionalUserAssignPayload) => void;
};

const MAX_FIELD_LENGTH = 160;

function AssignForm({ isSubmitting, onCancel, onSubmit }: AssignFormProps) {
  // Les champs reprennent le contrat reel de l'attribution : l'identite
  // creee ensuite porte civilite, nom, prenom et date de naissance. Reduire
  // le formulaire a « prenom / nom / e-mail » produirait une fiche
  // incomplete que personne ne reviendrait completer.
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [personalTitle, setPersonalTitle] = useState("");
  const [givenName, setGivenName] = useState("");
  const [surname, setSurname] = useState("");
  const [birthDate, setBirthDate] = useState("");
  const [initials, setInitials] = useState("");
  const [phone, setPhone] = useState("");
  const [error, setError] = useState<string | null>(null);

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const trimmedEmail = email.trim();
    const trimmedDisplayName = displayName.trim();
    const normalizedPersonalTitle = personalTitle.trim().toLowerCase();
    const trimmedGivenName = givenName.trim();
    const trimmedSurname = surname.trim();
    const trimmedBirthDate = birthDate.trim();

    if (!trimmedEmail || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmedEmail)) {
      setError("Indiquez une adresse e-mail valide.");
      return;
    }

    if (!trimmedDisplayName) {
      setError("Indiquez le nom affiché de l'utilisateur.");
      return;
    }

    if (
      (normalizedPersonalTitle !== "madame" && normalizedPersonalTitle !== "monsieur")
      || !trimmedGivenName
      || !trimmedSurname
      || !/^\d{4}-\d{2}-\d{2}$/.test(trimmedBirthDate)
    ) {
      setError("Renseignez la civilite, le prenom, le nom et une date de naissance valide.");
      return;
    }

    setError(null);
    onSubmit({
      email: trimmedEmail,
      displayName: trimmedDisplayName,
      personalTitle: normalizedPersonalTitle,
      givenName: trimmedGivenName,
      surname: trimmedSurname,
      birthDate: trimmedBirthDate,
      initials: optional(initials),
      phone: optional(phone),
    });
  }

  return (
    <form className="form-card" noValidate onSubmit={handleSubmit}>
      {error ? (
        <FormMessage title="Informations incomplètes" tone="error">
          <p>{error}</p>
        </FormMessage>
      ) : null}

      <label>
        Adresse e-mail
        <input
          autoComplete="off"
          maxLength={255}
          name="email"
          onChange={(event) => setEmail(event.target.value)}
          required
          type="email"
          value={email}
        />
      </label>

      <label>
        Nom affiché
        <input
          maxLength={MAX_FIELD_LENGTH}
          name="displayName"
          onChange={(event) => setDisplayName(event.target.value)}
          required
          type="text"
          value={displayName}
        />
      </label>

      <label>
        Civilité
        <input
          maxLength={MAX_FIELD_LENGTH}
          name="personalTitle"
          onChange={(event) => setPersonalTitle(event.target.value)}
          required
          type="text"
          value={personalTitle}
        />
      </label>

      <label>
        Prénom
        <input
          maxLength={MAX_FIELD_LENGTH}
          name="givenName"
          onChange={(event) => setGivenName(event.target.value)}
          required
          type="text"
          value={givenName}
        />
      </label>

      <label>
        Nom de famille
        <input
          maxLength={MAX_FIELD_LENGTH}
          name="surname"
          onChange={(event) => setSurname(event.target.value)}
          required
          type="text"
          value={surname}
        />
      </label>

      <label>
        Date de naissance
        <input
          name="birthDate"
          onChange={(event) => setBirthDate(event.target.value)}
          required
          type="date"
          value={birthDate}
        />
      </label>

      <label>
        Initiales
        <input
          maxLength={MAX_FIELD_LENGTH}
          name="initials"
          onChange={(event) => setInitials(event.target.value)}
          type="text"
          value={initials}
        />
      </label>

      <label>
        Téléphone
        <input
          maxLength={MAX_FIELD_LENGTH}
          name="phone"
          onChange={(event) => setPhone(event.target.value)}
          type="tel"
          value={phone}
        />
      </label>

      <p className="field-hint">
        L&apos;utilisateur recevra une invitation par e-mail pour définir son
        mot de passe. Ses accès sont préparés ensuite automatiquement.
      </p>

      <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
        <SubmitButton
          idleLabel="Envoyer l'invitation"
          isSubmitting={isSubmitting}
          submittingLabel="Envoi..."
        />
        <button
          className="button button-secondary"
          disabled={isSubmitting}
          onClick={onCancel}
          type="button"
        >
          Annuler
        </button>
      </div>
    </form>
  );
}

function optional(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}
