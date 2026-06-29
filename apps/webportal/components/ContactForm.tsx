"use client";

import { useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type ContactFormProps = {
  defaultSubject: string;
  offerReference: string | null;
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
  offerReference,
}: ContactFormProps) {
  const isSubmittingRef = useRef(false);
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [subject, setSubject] = useState(defaultSubject);
  const [message, setMessage] = useState("");
  const [state, setState] = useState<ContactState>({ status: "idle" });
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current) {
      return;
    }

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
            offerReference,
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
        message:
          "Message envoyé. Nous reviendrons vers vous par e-mail.",
      });
      setName("");
      setEmail("");
      setSubject(defaultSubject);
      setMessage("");
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
          <span className="field-error">{fieldErrors.name}</span>
        ) : null}
      </label>

      <label>
        Adresse e-mail
        <input
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
          <span className="field-error">{fieldErrors.email}</span>
        ) : null}
      </label>

      <label>
        Sujet
        <input
          aria-invalid={Boolean(fieldErrors.subject)}
          maxLength={150}
          name="subject"
          onChange={(event) => setSubject(event.target.value)}
          type="text"
          value={subject}
        />
        {fieldErrors.subject ? (
          <span className="field-error">{fieldErrors.subject}</span>
        ) : null}
      </label>

      <label>
        Message
        <textarea
          aria-invalid={Boolean(fieldErrors.message)}
          maxLength={5000}
          name="message"
          onChange={(event) => setMessage(event.target.value)}
          required
          rows={7}
          value={message}
        />
        {fieldErrors.message ? (
          <span className="field-error">{fieldErrors.message}</span>
        ) : null}
      </label>

      {offerReference ? (
        <input type="hidden" name="offerReference" value={offerReference} />
      ) : null}

      <p className="contact-form-note">
        Vos données ne sont utilisées que pour répondre à votre message.
        Aucun traceur ni cookie de mesure n&apos;est déposé sur ce site.
      </p>

      <SubmitButton
        idleLabel="Envoyer le message"
        isSubmitting={state.status === "submitting"}
        submittingLabel="Envoi en cours..."
      />
    </form>
  );
}
