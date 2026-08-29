"use client";

import type {
  IntegrationTestResponse,
  IntegrationView,
} from "@kermaria/shared";
import { useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { StatusBadge } from "@/components/StatusBadge";
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
  const [selectedKey, setSelectedKey] = useState(integrations[0]?.key ?? "");
  const selected = integrations.find((integration) => integration.key === selectedKey)
    ?? integrations[0]
    ?? null;

  return (
    <section
      aria-label="Intégrations"
      className="content-panel section-card admin-settings-surface admin-settings-focused-page admin-integrations"
    >
      <header className="admin-settings-focused-header">
        <div>
          <h2>Intégrations</h2>
          <p>
            Sélectionnez le service externe à contrôler. Les secrets restent masqués et
            une seule intégration est détaillée à la fois.
          </p>
        </div>
        <span className="admin-settings-persistence-note">
          Relevé {formatDate(checkedAt)}
        </span>
      </header>

      {message ? (
        <FormMessage
          tone={message.tone}
          title={message.tone === "success" ? "Test effectué" : "Test refusé"}
        >
          {message.text}
        </FormMessage>
      ) : null}

      <div className="admin-settings-workspace admin-settings-integrations-workspace">
        <aside aria-label="Intégrations disponibles" className="admin-settings-selector">
          <div className="admin-settings-selector-heading">
            <strong>{integrations.length} intégration{integrations.length > 1 ? "s" : ""}</strong>
            <span>Ouvrez uniquement le service que vous voulez examiner.</span>
          </div>
          <div className="admin-settings-selector-list">
            {integrations.map((integration) => (
              <button
                aria-current={selected?.key === integration.key ? "true" : undefined}
                className="admin-settings-selector-item"
                key={integration.key}
                onClick={() => {
                  setSelectedKey(integration.key);
                  setMessage(null);
                }}
                type="button"
              >
                <span>{integration.label}</span>
                <small>
                  {integration.configured ? "Configurée" : "Configuration incomplète"}
                  {" · "}
                  {stateLabels[integration.state]}
                </small>
                <StatusBadge
                  label={integration.state === "healthy" ? "Prêt" : stateLabels[integration.state]}
                  tone={
                    integration.state === "healthy"
                      ? "success"
                      : integration.state === "warning"
                        ? "warning"
                        : integration.state === "critical"
                          ? "danger"
                          : "info"
                  }
                />
              </button>
            ))}
          </div>
        </aside>

        <div className="admin-settings-detail-panel">
          {selected ? (
            <article className={`admin-integration admin-integration-${selected.state}`}>
              <header className="admin-integration-heading">
                <div>
                  <p className="eyebrow">Service sélectionné</p>
                  <h3>{selected.label}</h3>
                  <p className="muted">
                    Mode {selected.mode} {" · "}
                    {selected.configured ? "configuration complète" : "configuration incomplète"}
                  </p>
                </div>
                <StatusBadge
                  label={stateLabels[selected.state]}
                  tone={
                    selected.state === "healthy"
                      ? "success"
                      : selected.state === "warning"
                        ? "warning"
                        : selected.state === "critical"
                          ? "danger"
                          : "info"
                  }
                />
              </header>

              {selected.warning ? <p role="status">{selected.warning}</p> : null}
              {selected.riskNote ? <p className="muted">{selected.riskNote}</p> : null}

              <section className="admin-settings-detail-section" aria-labelledby="integration-config-title">
                <header>
                  <h4 id="integration-config-title">Configuration effective</h4>
                  <p>Valeurs non sensibles observées pour ce service.</p>
                </header>
                <dl className="admin-integration-facts">
                  {selected.facts.map((fact) => (
                    <div key={fact.label}>
                      <dt>{fact.label}</dt>
                      <dd>
                        {fact.value}
                        {fact.kind === "secret" ? (
                          <span className="admin-integration-secret"> (valeur jamais transmise)</span>
                        ) : null}
                      </dd>
                    </div>
                  ))}
                </dl>
              </section>

              <section className="admin-settings-detail-section" aria-labelledby="integration-health-title">
                <header>
                  <h4 id="integration-health-title">Activité récente</h4>
                  <p>Dernier succès et dernière erreur connus.</p>
                </header>
                <dl className="admin-integration-facts">
                  <div>
                    <dt>Dernier succès</dt>
                    <dd>{formatDate(selected.lastSuccessAt)}</dd>
                  </div>
                  <div>
                    <dt>Dernière erreur</dt>
                    <dd>
                      {formatDate(selected.lastErrorAt)}
                      {selected.lastErrorSummary ? ` · ${selected.lastErrorSummary}` : ""}
                    </dd>
                  </div>
                </dl>
              </section>

              {selected.operations.length > 0 ? (
                <section className="admin-settings-detail-section" aria-labelledby="integration-actions-title">
                  <header>
                    <h4 id="integration-actions-title">Actions disponibles</h4>
                    <p>Les opérations restent bornées par API-INTERNAL.</p>
                  </header>
                  <ul className="admin-integration-operations">
                    {selected.operations.map((operation) => (
                      <li key={operation.key}>
                        <strong>{operation.label}</strong>
                        <small>{operation.description}</small>
                        {operation.available && operation.key === "smtp_test" ? (
                          <SmtpTestForm onResult={setMessage} />
                        ) : (
                          <small>
                            {operation.available ? "Disponible" : "Indisponible"}
                            {operation.unavailableReason ? ` · ${operation.unavailableReason}` : ""}
                          </small>
                        )}
                      </li>
                    ))}
                  </ul>
                </section>
              ) : null}
            </article>
          ) : (
            <p className="admin-settings-inline-state muted">Aucune intégration disponible.</p>
          )}
        </div>
      </div>
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
