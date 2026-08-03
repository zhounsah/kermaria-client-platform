"use client";

import type {
  DemoAccountCreatedResponse,
  DemoContentTemplateSummary,
  DemoProfileSummary,
} from "@kermaria/shared";
import { FormEvent, useMemo, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type DemoAccountCreateFormProps = {
  profiles: DemoProfileSummary[];
  templates: DemoContentTemplateSummary[];
};

type CreatedState = {
  reference: string;
  email: string;
  expiresAt: string | null;
};

export function DemoAccountCreateForm({
  profiles,
  templates,
}: DemoAccountCreateFormProps) {
  const router = useRouter();
  const activeProfiles = useMemo(
    () => profiles.filter((profile) => profile.status === "active"),
    [profiles],
  );

  const [profileKey, setProfileKey] = useState(
    activeProfiles[0]?.key ?? "",
  );
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [initialPassword, setInitialPassword] = useState("");
  const [personalTitle, setPersonalTitle] = useState("");
  const [givenName, setGivenName] = useState("");
  const [surname, setSurname] = useState("");
  const [birthDate, setBirthDate] = useState("");
  const [lifetimeOverride, setLifetimeOverride] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [created, setCreated] = useState<CreatedState | null>(null);

  const selectedProfile = useMemo(
    () => activeProfiles.find((profile) => profile.key === profileKey) ?? null,
    [activeProfiles, profileKey],
  );

  const templateServiceNames = useMemo(() => {
    const key = selectedProfile?.contentTemplateKey;
    if (!key) {
      return [];
    }
    const template = templates.find((entry) => entry.key === key);
    return template?.serviceNames ?? [];
  }, [selectedProfile, templates]);

  // Ajustement d'etat pendant le rendu (pattern React recommande) : quand le
  // profil change, la selection a la carte repart de tous les services du
  // template. Evite un useEffect + setState (regle react-hooks).
  const [checkedServices, setCheckedServices] = useState<string[]>(
    templateServiceNames,
  );
  const [syncedProfileKey, setSyncedProfileKey] = useState(profileKey);
  if (syncedProfileKey !== profileKey) {
    setSyncedProfileKey(profileKey);
    setCheckedServices(templateServiceNames);
  }

  function toggleService(name: string) {
    setCheckedServices((current) =>
      current.includes(name)
        ? current.filter((entry) => entry !== name)
        : [...current, name],
    );
  }

  if (activeProfiles.length === 0) {
    return (
      <FormMessage title="Aucun profil actif" tone="info">
        Aucun profil de démonstration actif n&apos;est disponible. Créez ou
        activez un profil avant de générer un compte.
      </FormMessage>
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmitting) {
      return;
    }

    setIsSubmitting(true);
    setErrorMessage(null);
    setCreated(null);

    const trimmedOverride = lifetimeOverride.trim();
    const result = await requestBffJson<DemoAccountCreatedResponse>(
      "/api/admin/demo/accounts",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          profileKey,
          displayName,
          email,
          initialPassword,
          personalTitle: personalTitle === "" ? null : personalTitle,
          givenName: givenName.trim() === "" ? null : givenName.trim(),
          surname: surname.trim() === "" ? null : surname.trim(),
          birthDate: birthDate === "" ? null : birthDate,
          lifetimeDaysOverride:
            trimmedOverride === "" ? null : Number(trimmedOverride),
          selectedServiceNames:
            templateServiceNames.length > 0 ? checkedServices : null,
        }),
      },
    );

    setIsSubmitting(false);

    if (!result.ok) {
      setErrorMessage(result.error.message);
      return;
    }

    setCreated({
      reference: result.data.customerReference,
      email: result.data.email,
      expiresAt: result.data.expiresAt,
    });
    setDisplayName("");
    setEmail("");
    setInitialPassword("");
    setLifetimeOverride("");
    router.refresh();
  }

  return (
    <form className="form-grid" onSubmit={handleSubmit}>
      <div className="form-field">
        <label htmlFor="demo-profile">Profil de démonstration</label>
        <select
          id="demo-profile"
          onChange={(event) => setProfileKey(event.target.value)}
          value={profileKey}
        >
          {activeProfiles.map((profile) => (
            <option key={profile.key} value={profile.key}>
              {profile.label} — {profile.kind === "trial" ? "essai réel" : "vitrine"}
            </option>
          ))}
        </select>
        {selectedProfile ? (
          <p className="form-hint">
            {selectedProfile.kind === "trial"
              ? "Essai réel cadré : accès provisionné selon la matrice du profil."
              : "Vitrine inerte : aucun envoi, aucune facturation, aucun accès réel."}
            {" "}
            Durée par défaut : {selectedProfile.lifetimeDays} jour(s).
            {selectedProfile.contentTemplateKey
              ? ` Contenu : ${selectedProfile.contentTemplateKey}.`
              : ""}
          </p>
        ) : null}
      </div>

      {templateServiceNames.length > 0 ? (
        <fieldset className="form-field">
          <legend>Services inclus (composition à la carte)</legend>
          {templateServiceNames.map((name) => (
            <label className="checkbox-field" key={name}>
              <input
                checked={checkedServices.includes(name)}
                onChange={() => toggleService(name)}
                type="checkbox"
              />
              {name}
            </label>
          ))}
          <p className="form-hint">
            Décochez un service pour le retirer de cette démonstration.
          </p>
        </fieldset>
      ) : null}

      <div className="form-field">
        <label htmlFor="demo-display-name">Nom du client démo</label>
        <input
          id="demo-display-name"
          maxLength={200}
          onChange={(event) => setDisplayName(event.target.value)}
          required
          type="text"
          value={displayName}
        />
      </div>

      <div className="form-field">
        <label htmlFor="demo-email">E-mail de connexion</label>
        <input
          id="demo-email"
          maxLength={254}
          onChange={(event) => setEmail(event.target.value)}
          required
          type="email"
          value={email}
        />
      </div>

      <div className="form-field">
        <label htmlFor="demo-password">Mot de passe initial</label>
        <input
          autoComplete="new-password"
          id="demo-password"
          minLength={8}
          maxLength={200}
          onChange={(event) => setInitialPassword(event.target.value)}
          required
          type="password"
          value={initialPassword}
        />
        <p className="form-hint">
          Au moins 8 caractères. À communiquer au prospect pour sa connexion.
        </p>
      </div>

      {selectedProfile?.kind === "trial" ? (
        <>
          <div className="form-field">
            <label htmlFor="demo-title">Civilité</label>
            <select
              id="demo-title"
              onChange={(event) => setPersonalTitle(event.target.value)}
              required
              value={personalTitle}
            >
              <option value="">—</option>
              <option value="madame">Madame</option>
              <option value="monsieur">Monsieur</option>
            </select>
            <p className="form-hint">
              État civil requis pour l&apos;essai réel : il alimente la synchronisation
              KoXo qui crée l&apos;identité Active Directory. Un champ manquant
              bloquerait l&apos;export pour tous les comptes.
            </p>
          </div>

          <div className="form-field">
            <label htmlFor="demo-given-name">Prénom</label>
            <input
              id="demo-given-name"
              maxLength={100}
              onChange={(event) => setGivenName(event.target.value)}
              required
              type="text"
              value={givenName}
            />
          </div>

          <div className="form-field">
            <label htmlFor="demo-surname">Nom</label>
            <input
              id="demo-surname"
              maxLength={100}
              onChange={(event) => setSurname(event.target.value)}
              required
              type="text"
              value={surname}
            />
          </div>

          <div className="form-field">
            <label htmlFor="demo-birth-date">Date de naissance</label>
            <input
              id="demo-birth-date"
              onChange={(event) => setBirthDate(event.target.value)}
              required
              type="date"
              value={birthDate}
            />
          </div>
        </>
      ) : null}

      <div className="form-field">
        <label htmlFor="demo-lifetime">Durée de vie (jours, optionnel)</label>
        <input
          id="demo-lifetime"
          max={365}
          min={0}
          onChange={(event) => setLifetimeOverride(event.target.value)}
          placeholder={
            selectedProfile
              ? String(selectedProfile.lifetimeDays)
              : "14"
          }
          type="number"
          value={lifetimeOverride}
        />
        <p className="form-hint">
          Laisser vide pour la durée du profil. 0 = sans expiration.
        </p>
      </div>

      {errorMessage ? (
        <FormMessage title="Création impossible" tone="error">
          {errorMessage}
        </FormMessage>
      ) : null}

      {created ? (
        <FormMessage title="Compte de démonstration créé" tone="success">
          Référence <strong>{created.reference}</strong> — connexion{" "}
          <strong>{created.email}</strong>
          {created.expiresAt
            ? ` — expire le ${new Date(created.expiresAt).toLocaleString("fr-FR")}`
            : " — sans expiration"}
          .
        </FormMessage>
      ) : null}

      <div className="form-actions">
        <SubmitButton
          idleLabel="Créer le compte démo"
          isSubmitting={isSubmitting}
          submittingLabel="Création…"
        />
      </div>
    </form>
  );
}
