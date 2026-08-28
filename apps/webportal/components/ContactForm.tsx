"use client";

import { useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";
import { SYSTEM_SNIPPET_DEFAULTS } from "@/lib/system-snippet-defaults";

type ContactFormProps = {
  defaultSubject: string;
  formuleCode: string | null;
  defaultMessage?: string;
  submitLabel?: string;
  /** Texte systeme administrable ; repli sur la valeur de code. */
  confirmationText?: string;
  /** Note de confidentialite administrable ; repli sur la valeur de code. */
  privacyNotice?: string;
};

type ContactState =
  | { status: "idle" | "submitting" }
  | { status: "success"; message: string }
  | { status: "error"; message: string };

type FieldName = "name" | "email" | "subject" | "message";
type FieldErrors = Partial<Record<FieldName, string>>;

type ContactResponse = {
  code: string;
  message: string;
  correlation_id?: string;
  field_errors?: FieldErrors;
};

export function ContactForm({
  defaultSubject,
  formuleCode,
  defaultMessage = "",
  submitLabel = "Envoyer le message",
  confirmationText = SYSTEM_SNIPPET_DEFAULTS.contact_form_confirmation,
  privacyNotice = SYSTEM_SNIPPET_DEFAULTS.contact_form_privacy_notice,
}: ContactFormProps) {
  const fieldErrorId = (field: FieldName) => `contact-${field}-error`;
  const isSubmittingRef = useRef(false);
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [subject, setSubject] = useState(defaultSubject);
  const [message, setMessage] = useState(defaultMessage);
  const [state, setState] = useState<ContactState>({ status: "idle" });
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current) return;

    isSubmittingRef.current = true;
    setState({ status: "submitting" });
    setFieldErrors({});

    try {
      const response = await requestBffJson<ContactResponse>(
        "/api/contact",
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            name,
            email,
            subject,
            message,
            formuleCode,
          }),
        },
      );

      if (!response.ok) {
        const fallbackMessage = response.error.message;
        const payload = (response as unknown as {
          payload?: ContactResponse;
        }).payload;
        if (payload?.field_errors) {
          setFieldErrors(payload.field_errors);
        }
        setState({ status: "error", message: fallbackMessage });
        return;
      }

      setState({
        status: "success",
        message: confirmationText,
      });
      setName("");
      setEmail("");
      setSubject(defaultSubject);
      setMessage(defaultMessage);
    } finally {
      isSubmittingRef.current = false;
    }
  }

  return (
    <form
      action="/api/contact"
      className="form-card contact-form"
      method="post"
      noValidate
      onSubmit={handleSubmit}
    >
      {state.status === "success" ? (
        <FormMessage title="Message envoyé" tone="success">
          <p>{state.message}</p>
        </FormMessage>
      ) : null}

      {state.status === "error" ? (
        <FormMessage title="Envoi impossible" tone="error">
          <p>{state.message}</p>
        </FormMessage>
      ) : null}

      <label>
        Nom ou raison sociale
        <input
          aria-describedby={fieldErrors.name ? fieldErrorId("name") : undefined}
          aria-invalid={Boolean(fieldErrors.name)}
          autoComplete="name"
          maxLength={120}
          name="name"
          onChange={(event) => setName(event.target.value)}
          required
          type="text"
          value={name}
        />
        {fieldErrors.name ? (
          <span className="field-error" id={fieldErrorId("name")}>
            {fieldErrors.name}
          </span>
        ) : null}
      </label>

      <label>
        Adresse e-mail
        <input
          aria-describedby={fieldErrors.email ? fieldErrorId("email") : undefined}
          aria-invalid={Boolean(fieldErrors.email)}
          autoComplete="email"
          maxLength={254}
          name="email"
          onChange={(event) => setEmail(event.target.value)}
          required
          type="email"
          value={email}
        />
        {fieldErrors.email ? (
          <span className="field-error" id={fieldErrorId("email")}>
            {fieldErrors.email}
          </span>
        ) : null}
      </label>

      <label>
        Sujet
        <input
          aria-describedby={
            fieldErrors.subject ? fieldErrorId("subject") : undefined
          }
          aria-invalid={Boolean(fieldErrors.subject)}
          maxLength={150}
          name="subject"
          onChange={(event) => setSubject(event.target.value)}
          type="text"
          value={subject}
        />
        {fieldErrors.subject ? (
          <span className="field-error" id={fieldErrorId("subject")}>
            {fieldErrors.subject}
          </span>
        ) : null}
      </label>

      <label>
        Message
        <textarea
          aria-describedby={
            fieldErrors.message ? fieldErrorId("message") : undefined
          }
          aria-invalid={Boolean(fieldErrors.message)}
          maxLength={5000}
          name="message"
          onChange={(event) => setMessage(event.target.value)}
          required
          rows={7}
          value={message}
        />
        {fieldErrors.message ? (
          <span className="field-error" id={fieldErrorId("message")}>
            {fieldErrors.message}
          </span>
        ) : null}
      </label>

      {formuleCode ? (
        <input type="hidden" name="formuleCode" value={formuleCode} />
      ) : null}

      <p className="contact-form-note">{privacyNotice}</p>

      <SubmitButton
        idleLabel={submitLabel}
        isSubmitting={state.status === "submitting"}
        submittingLabel="Envoi en cours..."
      />
    </form>
  );
}
