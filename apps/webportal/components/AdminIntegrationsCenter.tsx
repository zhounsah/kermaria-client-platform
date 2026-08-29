"use client";

import type {
  IntegrationTestResponse,
  IntegrationView,
} from "@kermaria/shared";
import { useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { requestBffJson } from "@/lib/client-api";

const stateLabels: Record<IntegrationView["state"], string> = {
  healthy: "Opérationnel",
  warning: "À surveiller",
  critical: "Bloquant",
  info: "Information",
};

function formatDate(value: string | null): string {
  if (!value) return "—";
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? value
    : parsed.toLocaleString("fr-FR", { timeZone: "Europe/Paris" });
}

export function AdminIntegrationsCenter({
  integrations,
  checkedAt,
}: {
  integrations: IntegrationView[];
  checkedAt: string;
}) {
  const [message, setMessage] = useState<
    { tone: "success" | "error"; text: string } | null
  >(null);

  return (
    <section aria-label="Intégrations" className="admin-integrations">
      <p className="muted">
        Cette page observe les intégrations sans jamais afficher leurs secrets :
        un mot de passe, une clé ou un jeton n&apos;y apparaît que par sa
        présence. Les modes ne se règlent pas ici — ils commandent des appels
        réels chez des tiers et se changent sur la machine, avant un redémarrage
        du service.
      </p>
      <p className="muted">Dernier relevé : {formatDate(checkedAt)}</p>

      {message ? (
        <FormMessage
          tone={message.tone}
          title={message.tone === "success" ? "Test effectué" : "Test refusé"}
        >
          {message.text}
        </FormMessage>
      ) : null}

      <ul className="admin-integration-list">
        {integrations.map((integration) => (
          <li
            className={`admin-integration admin-integration-${integration.state}`}
            key={integration.key}
          >
            <header>
              <h3>{integration.label}</h3>
              <p className="muted">
                {stateLabels[integration.state]} · mode {integration.mode} ·{" "}
                {integration.configured ? "configurée" : "incomplète"}
              </p>
            </header>

            {integration.warning ? (
              <p role="status">{integration.warning}</p>
            ) : null}
            {integration.riskNote ? (
              <p className="muted">{integration.riskNote}</p>
            ) : null}

            <dl className="admin-integration-facts">
              {integration.facts.map((fact) => (
                <div key={fact.label}>
                  <dt>{fact.label}</dt>
                  <dd>
                    {fact.value}
                    {fact.kind === "secret" ? (
                      <span className="admin-integration-secret">
                        {" "}
                        (valeur jamais transmise)
                      </span>
                    ) : null}
                  </dd>
                </div>
              ))}
            </dl>

            <dl className="admin-integration-facts">
              <div>
                <dt>Dernier succès</dt>
                <dd>{formatDate(integration.lastSuccessAt)}</dd>
              </div>
              <div>
                <dt>Dernière erreur</dt>
                <dd>
                  {formatDate(integration.lastErrorAt)}
                  {integration.lastErrorSummary
                    ? ` — ${integration.lastErrorSummary}`
                    : ""}
                </dd>
              </div>
            </dl>

            <ul className="admin-integration-operations">
              {integration.operations.map((operation) => (
                <li key={operation.key}>
                  <strong>{operation.label}</strong>
                  <small>{operation.description}</small>
                  {operation.available && operation.key === "smtp_test" ? (
                    <SmtpTestForm onResult={setMessage} />
                  ) : (
                    <small>
                      Indisponible
                      {operation.unavailableReason
                        ? ` — ${operation.unavailableReason}`
                        : ""}
                    </small>
                  )}
                </li>
              ))}
            </ul>
          </li>
        ))}
      </ul>
    </section>
  );
}

function SmtpTestForm({
  onResult,
}: {
  onResult: (message: { tone: "success" | "error"; text: string }) => void;
}) {
  const [recipient, setRecipient] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit() {
    setBusy(true);
    const result = await requestBffJson<IntegrationTestResponse>(
      "/api/admin/settings/integrations/smtp-test",
      {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ recipient: recipient.trim() }),
      },
    );
    setBusy(false);
    onResult(
      result.ok
        ? {
            tone: result.data.code === "SMTP_TEST_SENT" ? "success" : "error",
            text: result.data.message,
          }
        : { tone: "error", text: result.error.message },
    );
  }

  return (
    <form
      className="admin-integration-test"
      onSubmit={(event) => {
        event.preventDefault();
        void submit();
      }}
    >
      <label htmlFor="smtp-test-recipient">Destinataire du test</label>
      <input
        id="smtp-test-recipient"
        onChange={(event) => setRecipient(event.target.value)}
        placeholder="adresse de l'allowlist"
        type="email"
        value={recipient}
      />
      <button
        className="button button-secondary"
        disabled={busy || recipient.trim().length < 5}
        type="submit"
      >
        {busy ? "Envoi…" : "Envoyer un test"}
      </button>
      <small>
        Un destinataire hors allowlist est refusé par API-INTERNAL : ce test ne
        peut pas atteindre un vrai client par erreur.
      </small>
    </form>
  );
}
