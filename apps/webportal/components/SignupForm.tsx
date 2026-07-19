"use client";

import Script from "next/script";
import { useEffect, useRef, useState } from "react";

import type { PublicPackCode } from "@kermaria/shared";

import { FormMessage } from "@/components/FormMessage";
import {
  PublicPackSelectionSummary,
  type PublicPackSelectionSummaryInput,
} from "@/components/PublicPackSelectionSummary";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type SignupFormProps = {
  hcaptchaSiteKey: string | null;
  initialPackSelection?: (PublicPackSelectionSummaryInput & {
    packKey: PublicPackCode;
  }) | null;
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

export function SignupForm({
  hcaptchaSiteKey,
  initialPackSelection = null,
}: SignupFormProps) {
  const isSubmittingRef = useRef(false);
  const renderedAtRef = useRef<number>(0);
  const [customerType, setCustomerType] = useState("professional");
  const [companyName, setCompanyName] = useState("");
  const [addressLine1, setAddressLine1] = useState("");
  const [addressLine2, setAddressLine2] = useState("");
  const [postalCode, setPostalCode] = useState("");
  const [city, setCity] = useState("");
  const [country, setCountry] = useState("France");
  const [personalTitle, setPersonalTitle] = useState("");
  const [givenName, setGivenName] = useState("");
  const [surname, setSurname] = useState("");
  const [initials, setInitials] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [message, setMessage] = useState("");
  const [state, setState] = useState<SignupState>({ status: "idle" });

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
          addressLine1,
          addressLine2,
          postalCode,
          city,
          country,
          personalTitle,
          givenName,
          surname,
          initials,
          email,
          phone,
          message,
          packKey: initialPackSelection?.packKey ?? null,
          commitmentMonths: initialPackSelection?.commitmentMonths ?? null,
          paymentMode: initialPackSelection?.paymentMode ?? null,
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
        className="form-card signup-form"
        method="post"
        noValidate
        onSubmit={handleSubmit}
      >
        {state.status === "error" ? (
          <FormMessage title="Envoi impossible" tone="error">
            <p>{state.message}</p>
          </FormMessage>
        ) : null}

        <p className="field-hint">
          Ces informations alimentent à la fois votre fiche client et le futur
          compte d'accès rattaché à <code>clients.home.bzh</code> lorsque
          l'identité est finalisée.
        </p>

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

        <label>
          Nom ou raison sociale
          <input
            autoComplete="organization"
            maxLength={200}
            name="companyName"
            onChange={(event) => setCompanyName(event.target.value)}
            required
            type="text"
            value={companyName}
          />
        </label>

        <label>
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

        <label>
          Complement d'adresse (facultatif)
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

        <label>
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

        <p className="field-hint">
          Contact principal qui recevra les messages d'ouverture et définira le
          mot de passe initial.
        </p>

        <label>
          Civilité (facultatif)
          <select
            autoComplete="honorific-prefix"
            name="personalTitle"
            onChange={(event) => setPersonalTitle(event.target.value)}
            value={personalTitle}
          >
            <option value="">Non précisé</option>
            <option value="madame">Madame</option>
            <option value="monsieur">Monsieur</option>
            <option value="autre">Autre</option>
          </select>
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

        <label>
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

        {initialPackSelection ? (
          <PublicPackSelectionSummary
            commitmentMonths={initialPackSelection.commitmentMonths}
            description="Ce résumé sera repris avec votre demande pour conserver le contexte du pack sélectionné."
            eyebrow="Pack associé à la demande"
            firstChargeAmountCents={initialPackSelection.firstChargeAmountCents}
            monthlyPriceAmountCents={initialPackSelection.monthlyPriceAmountCents}
            packLabel={initialPackSelection.packLabel}
            paymentMode={initialPackSelection.paymentMode}
            setupFeeAmountCents={initialPackSelection.setupFeeAmountCents}
            title={initialPackSelection.packLabel}
          />
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
          En envoyant ce formulaire, vous demandez l'ouverture d'un accès
          client. Vous confirmerez d'abord votre adresse e-mail, puis notre
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

function resetCaptcha() {
  const globalWithHcaptcha = window as typeof window & {
    hcaptcha?: { reset: () => void };
  };
  globalWithHcaptcha.hcaptcha?.reset();
}
