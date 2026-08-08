"use client";

import Script from "next/script";
import { useEffect, useRef, useState } from "react";

import type { CatalogConfigurationInput, PublicPackCode } from "@kermaria/shared";

import { FormMessage } from "@/components/FormMessage";
import {
  PublicPackSelectionSummary,
  type PublicPackSelectionSummaryInput,
} from "@/components/PublicPackSelectionSummary";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";
import styles from "./SignupForm.module.css";

type SignupFormProps = {
  hcaptchaSiteKey: string | null;
  initialPackSelection?: (PublicPackSelectionSummaryInput & {
    packKey: PublicPackCode;
  }) | null;
  initialCatalogConfiguration?: CatalogConfigurationInput | null;
};

type SignupState =
  | { status: "idle" | "submitting" }
  | { status: "success"; message: string }
  | { status: "error"; message: string };

type SignupResponse = {
  code: string;
  message: string;
  correlation_id?: string;
};

const USER_SIZE_OPTIONS = [
  { value: "1", label: "1 utilisateur" },
  { value: "2-4", label: "2 à 4 utilisateurs" },
  { value: "5-9", label: "5 à 9 utilisateurs" },
  { value: "10-24", label: "10 à 24 utilisateurs" },
  { value: "25-49", label: "25 à 49 utilisateurs" },
  { value: "50-249", label: "50 à 249 utilisateurs" },
  { value: "250-999", label: "250 à 999 utilisateurs" },
  { value: "1000+", label: "1000 utilisateurs ou plus" },
] as const;

export function SignupForm({
  hcaptchaSiteKey,
  initialPackSelection = null,
  initialCatalogConfiguration = null,
}: SignupFormProps) {
  const isSubmittingRef = useRef(false);
  const renderedAtRef = useRef<number>(0);
  const [customerType, setCustomerType] = useState("professional");
  const [companyName, setCompanyName] = useState("");
  const [userSize, setUserSize] = useState("");
  const [addressLine1, setAddressLine1] = useState("");
  const [addressLine2, setAddressLine2] = useState("");
  const [postalCode, setPostalCode] = useState("");
  const [city, setCity] = useState("");
  const [country, setCountry] = useState("France");
  const [personalTitle, setPersonalTitle] = useState("");
  const [birthDate, setBirthDate] = useState("");
  const [givenName, setGivenName] = useState("");
  const [surname, setSurname] = useState("");
  const [initials, setInitials] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [message, setMessage] = useState("");
  const [state, setState] = useState<SignupState>({ status: "idle" });

  const isIndividual = customerType === "individual";
  const displayCompanyField = !isIndividual;
  const displayUserSizeField = !isIndividual;

  useEffect(() => {
    renderedAtRef.current = Date.now();
  }, []);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current) {
      return;
    }

    const form = event.currentTarget;
    const formData = new FormData(form);
    const honeypot = String(formData.get("website") ?? "");
    const hcaptchaToken = String(formData.get("h-captcha-response") ?? "");

    if (hcaptchaSiteKey && !hcaptchaToken) {
      setState({
        status: "error",
        message: "Merci de valider le contrôle anti-robot avant d'envoyer.",
      });
      return;
    }

    isSubmittingRef.current = true;
    setState({ status: "submitting" });

    try {
      const response = await requestBffJson<SignupResponse>("/api/signup", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          customerType,
          companyName,
          userSize,
          addressLine1,
          addressLine2,
          postalCode,
          city,
          country,
          personalTitle,
          birthDate,
          givenName,
          surname,
          initials,
          email,
          phone,
          message,
          packKey: initialPackSelection?.packKey ?? null,
          commitmentMonths: initialPackSelection?.commitmentMonths ?? null,
          paymentMode: initialPackSelection?.paymentMode ?? null,
          users: initialCatalogConfiguration?.users ?? null,
          storageGb: initialCatalogConfiguration?.storageGb ?? null,
          needsVpn: initialCatalogConfiguration?.needsVpn ?? null,
          needsWindowsDesktop:
            initialCatalogConfiguration?.needsWindowsDesktop ?? null,
          hcaptchaToken: hcaptchaToken || null,
          website: honeypot,
          formRenderedAt: renderedAtRef.current,
        }),
      });

      if (!response.ok) {
        resetCaptcha();
        setState({ status: "error", message: response.error.message });
        return;
      }

      setState({
        status: "success",
        message: initialPackSelection
          ? "Demande envoyée. Vérifiez votre boîte mail pour confirmer votre adresse, puis attendez notre validation avant de définir le mot de passe et de reprendre le pack depuis votre espace client."
          : "Demande envoyée. Vérifiez votre boîte mail pour confirmer votre adresse, puis attendez notre validation avant de définir votre mot de passe.",
      });
    } finally {
      isSubmittingRef.current = false;
    }
  }

  if (state.status === "success") {
    return (
      <FormMessage title="Demande envoyée" tone="success">
        <p>{state.message}</p>
      </FormMessage>
    );
  }

  return (
    <>
      {hcaptchaSiteKey ? (
        <Script
          src="https://js.hcaptcha.com/1/api.js"
          strategy="afterInteractive"
        />
      ) : null}
      <form
        action="/api/signup"
        className={`form-card ${styles.form}`}
        method="post"
        noValidate
        onSubmit={handleSubmit}
      >
        {state.status === "error" ? (
          <FormMessage title="Envoi impossible" tone="error">
            <p>{state.message}</p>
          </FormMessage>
        ) : null}

        <div className={styles.intro}>
          <p className="field-hint">
            Ces informations alimentent à la fois votre fiche client et le futur
            compte d&apos;accès rattaché lorsque l&apos;identité est finalisée.
          </p>
        </div>

        <div className={styles.layout}>
          <section className={styles.panel} aria-labelledby="signup-structure-heading">
            <div className={styles.panelHeader}>
              <p className={styles.panelKicker}>Structure</p>
              <h2 id="signup-structure-heading">Structure et besoin</h2>
              <p className="field-hint">
                Renseignez ici les informations liées à votre structure, à
                l&apos;adresse postale et au contexte de votre demande.
              </p>
            </div>

            <div className={styles.fields}>
              <label>
                Type de structure
                <select
                  name="customerType"
                  onChange={(event) => setCustomerType(event.target.value)}
                  required
                  value={customerType}
                >
                  <option value="professional">Professionnel</option>
                  <option value="association">Association</option>
                  <option value="individual">Particulier</option>
                </select>
              </label>

              {displayCompanyField ? (
                <label>
                  Raison sociale
                  <input
                    autoComplete="organization"
                    maxLength={200}
                    name="companyName"
                    onChange={(event) => setCompanyName(event.target.value)}
                    required={displayCompanyField}
                    type="text"
                    value={companyName}
                  />
                </label>
              ) : null}

              {displayUserSizeField ? (
                <label>
                  Tranche d&apos;utilisateurs
                  <select
                    name="userSize"
                    onChange={(event) => setUserSize(event.target.value)}
                    required={displayUserSizeField}
                    value={userSize}
                  >
                    <option value="">Sélectionnez une tranche</option>
                    {USER_SIZE_OPTIONS.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </label>
              ) : null}

              <label className={styles.fieldSpan2}>
                Adresse postale
                <input
                  autoComplete="address-line1"
                  maxLength={255}
                  name="addressLine1"
                  onChange={(event) => setAddressLine1(event.target.value)}
                  required
                  type="text"
                  value={addressLine1}
                />
              </label>

              <label className={styles.fieldSpan2}>
                Complément d&apos;adresse (facultatif)
                <input
                  autoComplete="address-line2"
                  maxLength={255}
                  name="addressLine2"
                  onChange={(event) => setAddressLine2(event.target.value)}
                  type="text"
                  value={addressLine2}
                />
              </label>

              <label>
                Code postal
                <input
                  autoComplete="postal-code"
                  maxLength={32}
                  name="postalCode"
                  onChange={(event) => setPostalCode(event.target.value)}
                  required
                  type="text"
                  value={postalCode}
                />
              </label>

              <label>
                Ville
                <input
                  autoComplete="address-level2"
                  maxLength={160}
                  name="city"
                  onChange={(event) => setCity(event.target.value)}
                  required
                  type="text"
                  value={city}
                />
              </label>

              <label className={styles.fieldSpan2}>
                Pays
                <input
                  autoComplete="country-name"
                  maxLength={100}
                  name="country"
                  onChange={(event) => setCountry(event.target.value)}
                  required
                  type="text"
                  value={country}
                />
              </label>

              <label className={styles.fieldSpan2}>
                Votre besoin (facultatif)
                <textarea
                  maxLength={2000}
                  name="message"
                  onChange={(event) => setMessage(event.target.value)}
                  placeholder={
                    initialPackSelection
                      ? "Précisez ce qu'il faut savoir avant l'ouverture du compte ou la reprise du pack."
                      : "Précisez votre contexte, vos contraintes ou ce que vous attendez de l'ouverture du compte."
                  }
                  rows={5}
                  value={message}
                />
              </label>
            </div>
          </section>

          <section className={styles.panel} aria-labelledby="signup-contact-heading">
            <div className={styles.panelHeader}>
              <p className={styles.panelKicker}>Contact principal</p>
              <h2 id="signup-contact-heading">Informations client</h2>
              <p className="field-hint">
                Ce contact principal recevra les messages d&apos;ouverture et
                définira le mot de passe initial.
              </p>
            </div>

            <div className={styles.fields}>
              <label>
                Civilité
                <select
                  autoComplete="honorific-prefix"
                  name="personalTitle"
                  onChange={(event) => setPersonalTitle(event.target.value)}
                  required
                  value={personalTitle}
                >
                  <option value="">Sélectionnez</option>
                  <option value="madame">Madame</option>
                  <option value="monsieur">Monsieur</option>
                </select>
              </label>

              <label>
                Date de naissance
                <input
                  max={new Date().toISOString().slice(0, 10)}
                  name="birthDate"
                  onChange={(event) => setBirthDate(event.target.value)}
                  required
                  type="date"
                  value={birthDate}
                />
              </label>

              <label>
                Prénom
                <input
                  autoComplete="given-name"
                  maxLength={120}
                  name="givenName"
                  onChange={(event) => setGivenName(event.target.value)}
                  required
                  type="text"
                  value={givenName}
                />
              </label>

              <label>
                Nom
                <input
                  autoComplete="family-name"
                  maxLength={120}
                  name="surname"
                  onChange={(event) => setSurname(event.target.value)}
                  required
                  type="text"
                  value={surname}
                />
              </label>

              <label>
                Initiales (facultatif)
                <input
                  maxLength={16}
                  name="initials"
                  onChange={(event) => setInitials(event.target.value)}
                  type="text"
                  value={initials}
                />
              </label>

              <label>
                Téléphone (facultatif)
                <input
                  autoComplete="tel"
                  maxLength={40}
                  name="phone"
                  onChange={(event) => setPhone(event.target.value)}
                  type="tel"
                  value={phone}
                />
              </label>

              <label className={styles.fieldSpan2}>
                Adresse e-mail de connexion
                <input
                  autoComplete="email"
                  maxLength={320}
                  name="email"
                  onChange={(event) => setEmail(event.target.value)}
                  required
                  type="email"
                  value={email}
                />
              </label>
            </div>
          </section>
        </div>

        {initialPackSelection ? (
          <PublicPackSelectionSummary
            commitmentMonths={initialPackSelection.commitmentMonths}
            description="Ce résumé sera repris avec votre demande pour conserver le contexte du pack sélectionné."
            eyebrow="Pack associé à la demande"
            fiscalMention={initialPackSelection.fiscalMention}
            fiscalRegime={initialPackSelection.fiscalRegime}
            firstChargeAmountCents={initialPackSelection.firstChargeAmountCents}
            monthlyPriceAmountCents={initialPackSelection.monthlyPriceAmountCents}
            packLabel={initialPackSelection.packLabel}
            paymentMode={initialPackSelection.paymentMode}
            setupFeeAmountCents={initialPackSelection.setupFeeAmountCents}
            title={initialPackSelection.packLabel}
          />
        ) : null}

        {initialCatalogConfiguration ? (
          <section
            aria-label="Configuration demandee"
            className="catalog-configuration-summary"
          >
            <h2>Configuration demandee</h2>
            <dl>
              <div>
                <dt>Utilisateurs</dt>
                <dd>{initialCatalogConfiguration.users ?? "A preciser"}</dd>
              </div>
              <div>
                <dt>Stockage</dt>
                <dd>
                  {initialCatalogConfiguration.storageGb
                    ? `${initialCatalogConfiguration.storageGb} Go`
                    : "A preciser"}
                </dd>
              </div>
              <div>
                <dt>VPN</dt>
                <dd>{formatBooleanNeed(initialCatalogConfiguration.needsVpn)}</dd>
              </div>
              <div>
                <dt>Bureau Windows distant</dt>
                <dd>
                  {formatBooleanNeed(
                    initialCatalogConfiguration.needsWindowsDesktop,
                  )}
                </dd>
              </div>
            </dl>
          </section>
        ) : null}

        {initialPackSelection ? (
          <>
            <input
              name="packKey"
              type="hidden"
              value={initialPackSelection.packKey}
            />
            <input
              name="commitmentMonths"
              type="hidden"
              value={String(initialPackSelection.commitmentMonths)}
            />
            <input
              name="paymentMode"
              type="hidden"
              value={initialPackSelection.paymentMode}
            />
            {initialCatalogConfiguration ? (
              <>
                <input
                  name="users"
                  type="hidden"
                  value={String(initialCatalogConfiguration.users ?? "")}
                />
                <input
                  name="storageGb"
                  type="hidden"
                  value={String(initialCatalogConfiguration.storageGb ?? "")}
                />
                <input
                  name="needsVpn"
                  type="hidden"
                  value={formatNullableBooleanValue(
                    initialCatalogConfiguration.needsVpn,
                  )}
                />
                <input
                  name="needsWindowsDesktop"
                  type="hidden"
                  value={formatNullableBooleanValue(
                    initialCatalogConfiguration.needsWindowsDesktop,
                  )}
                />
              </>
            ) : null}
          </>
        ) : null}

        <div aria-hidden="true" className="signup-honeypot">
          <label>
            Ne remplissez pas ce champ
            <input
              autoComplete="off"
              name="website"
              tabIndex={-1}
              type="text"
            />
          </label>
        </div>

        {hcaptchaSiteKey ? (
          <div className="h-captcha" data-sitekey={hcaptchaSiteKey} />
        ) : null}

        <p className="signup-form-note">
          En envoyant ce formulaire, vous demandez l&apos;ouverture d&apos;un accès
          client. Vous confirmerez d&apos;abord votre adresse e-mail, puis notre
          équipe validera la demande avant la définition du mot de passe
          {initialPackSelection
            ? " et la reprise du pack dans l'espace client."
            : "."}
        </p>

        <SubmitButton
          idleLabel="Envoyer ma demande"
          isSubmitting={state.status === "submitting"}
          submittingLabel="Envoi en cours..."
        />
      </form>
    </>
  );
}

function formatBooleanNeed(value: boolean | null) {
  if (value === true) {
    return "Oui";
  }
  if (value === false) {
    return "Non";
  }
  return "A preciser";
}

function formatNullableBooleanValue(value: boolean | null) {
  if (value === true) {
    return "yes";
  }
  if (value === false) {
    return "no";
  }
  return "";
}

function resetCaptcha() {
  const globalWithHcaptcha = window as typeof window & {
    hcaptcha?: { reset: () => void };
  };
  globalWithHcaptcha.hcaptcha?.reset();
}
