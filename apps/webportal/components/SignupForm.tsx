"use client";

import Script from "next/script";
import { useEffect, useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type SignupFormProps = {
  hcaptchaSiteKey: string | null;
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

export function SignupForm({ hcaptchaSiteKey }: SignupFormProps) {
  const isSubmittingRef = useRef(false);
  const renderedAtRef = useRef<number>(0);
  const [companyName, setCompanyName] = useState("");
  const [contactName, setContactName] = useState("");
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
          companyName,
          contactName,
          email,
          phone,
          message,
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
        message:
          "Demande envoyée. Vérifiez votre boîte mail pour confirmer votre adresse, puis attendez la validation de notre équipe.",
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
          Nom du contact
          <input
            autoComplete="name"
            maxLength={200}
            name="contactName"
            onChange={(event) => setContactName(event.target.value)}
            required
            type="text"
            value={contactName}
          />
        </label>

        <label>
          Adresse e-mail
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
            rows={5}
            value={message}
          />
        </label>

        {/* Honeypot anti-bot : masqué et hors flux de tabulation. */}
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
          En envoyant ce formulaire, vous demandez la création d&apos;un
          accès. Un e-mail de confirmation vous sera adressé, puis notre
          équipe validera votre demande avant l&apos;ouverture de
          l&apos;accès. Aucune donnée n&apos;est utilisée à des fins
          publicitaires.
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
