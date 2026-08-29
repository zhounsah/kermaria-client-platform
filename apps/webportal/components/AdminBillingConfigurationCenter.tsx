"use client";

import type {
  BillingV2ConfigurationOverview,
  BillingV2FeatureFlagItem,
  FiscalMentionCreatePayload,
  FiscalPolicyAdminView,
  FiscalPolicyMutationResponse,
  FiscalPolicyRegimeView,
} from "@kermaria/shared";
import Link from "next/link";
import { useMemo, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { StatusBadge } from "@/components/StatusBadge";
import { requestBffJson } from "@/lib/client-api";

const riskLabels: Record<string, string> = {
  low: "Risque faible",
  medium: "Risque modéré",
  high: "Risque élevé",
  critical: "Risque critique",
};

function formatDate(value: string | null): string {
  if (!value) return "—";
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? value
    : parsed.toLocaleString("fr-FR", { timeZone: "Europe/Paris" });
}

export function AdminBillingConfigurationCenter({
  initialFiscalPolicy,
  billingV2,
}: {
  initialFiscalPolicy: FiscalPolicyAdminView;
  billingV2: BillingV2ConfigurationOverview | null;
}) {
  const [policy, setPolicy] = useState(initialFiscalPolicy);
  const [message, setMessage] = useState<
    { tone: "success" | "error"; text: string } | null
  >(null);
  const [surface, setSurface] = useState<"fiscal" | "billing">("fiscal");
  const [selectedRegimeKey, setSelectedRegimeKey] = useState(
    initialFiscalPolicy.regimes[0]?.regime ?? "",
  );
  const selectedRegime = policy.regimes.find(
    (regime) => regime.regime === selectedRegimeKey,
  ) ?? policy.regimes[0] ?? null;

  return (
    <section
      aria-label="Facturation et fiscalité"
      className="content-panel section-card admin-settings-surface admin-settings-focused-page admin-billing-center"
    >
      <header className="admin-settings-focused-header">
        <div>
          <h2>Facturation et fiscalité</h2>
          <p>
            Séparez les mentions fiscales administrables de l’état opérationnel de
            Billing V2. Chaque modification reste limitée à l’élément sélectionné.
          </p>
        </div>
        <span className="admin-settings-persistence-note">
          {policy.persistent ? "Persistance MariaDB" : "Mode temporaire"}
        </span>
      </header>

      {message ? (
        <FormMessage
          tone={message.tone}
          title={message.tone === "success" ? "Enregistré" : "Refusé"}
        >
          {message.text}
        </FormMessage>
      ) : null}

      <div
        aria-label="Rubrique de facturation"
        className="admin-settings-segmented"
        role="tablist"
      >
        <button
          aria-selected={surface === "fiscal"}
          className={surface === "fiscal" ? "is-active" : undefined}
          onClick={() => {
            setSurface("fiscal");
            setMessage(null);
          }}
          role="tab"
          type="button"
        >
          Fiscalité
        </button>
        <button
          aria-selected={surface === "billing"}
          className={surface === "billing" ? "is-active" : undefined}
          onClick={() => {
            setSurface("billing");
            setMessage(null);
          }}
          role="tab"
          type="button"
        >
          Billing V2
        </button>
      </div>

      {surface === "fiscal" ? (
        <div className="admin-settings-workspace admin-settings-billing-workspace">
          <aside aria-label="Régimes fiscaux" className="admin-settings-selector">
            <div className="admin-settings-selector-heading">
              <strong>
                {policy.regimes.length} régime{policy.regimes.length > 1 ? "s" : ""}
              </strong>
              <span>Choisissez le régime dont vous voulez gérer la mention.</span>
            </div>
            <div className="admin-settings-selector-list">
              {policy.regimes.map((regime) => (
                <button
                  aria-current={
                    selectedRegime?.regime === regime.regime ? "true" : undefined
                  }
                  className="admin-settings-selector-item"
                  key={regime.regime}
                  onClick={() => {
                    setSelectedRegimeKey(regime.regime);
                    setMessage(null);
                  }}
                  type="button"
                >
                  <span>{regime.label}</span>
                  <small>
                    {regime.activeSource === "code"
                      ? "Mention du code"
                      : "Mention enregistrée"}
                    {" · "}
                    v{regime.version}
                  </small>
                </button>
              ))}
            </div>
          </aside>

          <div className="admin-settings-detail-panel">
            {selectedRegime ? (
              <RegimeEditor
                key={`${selectedRegime.regime}-${selectedRegime.version}`}
                regime={selectedRegime}
                onResult={(result) => {
                  if (result.view) setPolicy(result.view);
                  setMessage({
                    tone: result.view ? "success" : "error",
                    text: result.message,
                  });
                }}
              />
            ) : (
              <p className="admin-settings-inline-state muted">
                Aucun régime fiscal disponible.
              </p>
            )}
          </div>
        </div>
      ) : (
        <section
          aria-labelledby="billing-v2-title"
          className="admin-settings-single-surface"
        >
          <header>
            <h3 id="billing-v2-title">Billing V2</h3>
            <p>
              État du catalogue, des prestataires, des feature flags et de la préparation
              au checkout. Cette vue reste en lecture.
            </p>
          </header>
          {billingV2 === null ? (
            <p className="admin-settings-inline-state muted">
              Le résumé Billing V2 n&apos;est pas disponible pour le moment.
            </p>
          ) : (
            <BillingV2Summary overview={billingV2} />
          )}
        </section>
      )}
    </section>
  );
}

function RegimeEditor({
  regime,
  onResult,
}: {
  regime: FiscalPolicyRegimeView;
  onResult: (result: FiscalPolicyMutationResponse) => void;
}) {
  const [mention, setMention] = useState(regime.activeMention);
  const [effectiveFrom, setEffectiveFrom] = useState("");
  const [busy, setBusy] = useState(false);
  const scheduled = useMemo(
    () => regime.versions.filter((version) => version.scheduled),
    [regime.versions],
  );
  const applied = useMemo(
    () => regime.versions.filter((version) => !version.scheduled),
    [regime.versions],
  );

  async function submit() {
    const trimmed = mention.trim();
    if (!trimmed || !effectiveFrom) return;
    // Confirmation renforcée : la mention part sur de vraies factures.
    if (
      !window.confirm(
        `Cette mention sera imprimée sur les documents établis à partir du ${formatDate(new Date(effectiveFrom).toISOString())}.\n\n« ${trimmed} »\n\nConfirmer ?`,
      )
    ) {
      return;
    }
    setBusy(true);
    const result = await requestBffJson<FiscalPolicyMutationResponse>(
      "/api/admin/settings/fiscal-policy/mentions",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          regime: regime.regime,
          mention: trimmed,
          effectiveFrom: new Date(effectiveFrom).toISOString(),
          expectedVersion: regime.version,
        } satisfies FiscalMentionCreatePayload),
      },
    );
    setBusy(false);
    if (!result.ok) {
      onResult({
        code: result.error.code,
        message: result.error.message,
        view: null,
        correlationId: result.error.correlationId ?? "",
      });
      return;
    }
    setEffectiveFrom("");
    onResult(result.data);
  }

  async function cancelScheduled(id: string) {
    if (!window.confirm("Annuler cette mention planifiée ?")) return;
    setBusy(true);
    const result = await requestBffJson<FiscalPolicyMutationResponse>(
      `/api/admin/settings/fiscal-policy/mentions/${id}`,
      { method: "DELETE" },
    );
    setBusy(false);
    onResult(
      result.ok
        ? result.data
        : {
            code: result.error.code,
            message: result.error.message,
            view: null,
            correlationId: result.error.correlationId ?? "",
          },
    );
  }

  return (
    <article className="admin-billing-regime">
      <header>
        <h3>{regime.label}</h3>
        <p>{regime.description}</p>
      </header>
      <dl>
        <div>
          <dt>Mention appliquée</dt>
          <dd>{regime.activeMention}</dd>
        </div>
        <div>
          <dt>Origine</dt>
          <dd>
            {regime.activeSource === "code"
              ? "Mention intégrée au code"
              : `Version enregistrée, en vigueur depuis le ${formatDate(regime.activeEffectiveFrom)}`}
          </dd>
        </div>
        <div>
          <dt>Mention du code</dt>
          <dd>{regime.defaultMention}</dd>
        </div>
      </dl>

      <form
        onSubmit={(event) => {
          event.preventDefault();
          void submit();
        }}
      >
        <label htmlFor={`${regime.regime}-mention`}>Nouvelle mention</label>
        <textarea
          id={`${regime.regime}-mention`}
          maxLength={300}
          onChange={(event) => setMention(event.target.value)}
          rows={2}
          value={mention}
        />
        <label htmlFor={`${regime.regime}-date`}>Date d&apos;effet</label>
        <input
          id={`${regime.regime}-date`}
          onChange={(event) => setEffectiveFrom(event.target.value)}
          type="datetime-local"
          value={effectiveFrom}
        />
        <p className="muted">
          La date doit être future : une mention ne peut pas modifier un
          document déjà émis.
        </p>
        <button
          className="button button-secondary"
          disabled={busy || !effectiveFrom || mention.trim().length === 0}
          type="submit"
        >
          {busy ? "Enregistrement…" : "Planifier cette mention"}
        </button>
      </form>

      {scheduled.length > 0 ? (
        <section aria-label={`Mentions planifiées — ${regime.label}`}>
          <h4>Planifiées</h4>
          <ul>
            {scheduled.map((version) => (
              <li key={version.id}>
                <span>{version.mention}</span>
                <small>À partir du {formatDate(version.effectiveFrom)}</small>
                <button
                  className="button button-link"
                  disabled={busy}
                  onClick={() => void cancelScheduled(version.id)}
                  type="button"
                >
                  Annuler
                </button>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {applied.length > 0 ? (
        <section aria-label={`Historique — ${regime.label}`}>
          <h4>Historique</h4>
          <ul>
            {applied.map((version) => (
              <li key={version.id}>
                <span>{version.mention}</span>
                <small>
                  Depuis le {formatDate(version.effectiveFrom)}
                  {version.active ? " · en vigueur" : ""}
                </small>
              </li>
            ))}
          </ul>
          <p className="muted">
            Une version déjà appliquée n&apos;est jamais supprimée : elle
            documente ce qui a été imprimé sur de vrais documents.
          </p>
        </section>
      ) : null}
    </article>
  );
}

function BillingV2Summary({
  overview,
}: {
  overview: BillingV2ConfigurationOverview;
}) {
  return (
    <>
      <dl className="admin-billing-summary">
        <div>
          <dt>Catalogue</dt>
          <dd>
            {overview.catalog
              ? `${overview.catalog.activeServiceCount}/${overview.catalog.serviceCount} services actifs · ${overview.catalog.activePresetCount}/${overview.catalog.presetCount} formules actives · ${overview.catalog.commitmentCount} engagements`
              : "Indisponible"}
          </dd>
        </div>
        <div>
          <dt>Persistance</dt>
          <dd>
            {overview.readiness
              ? overview.readiness.persistentSqlAvailable
                ? overview.readiness.schemaReady
                  ? "MariaDB, schéma complet"
                  : "MariaDB, schéma incomplet"
                : "Aucune persistance"
              : "Indisponible"}
          </dd>
        </div>
        <div>
          <dt>Première souscription réelle</dt>
          <dd>
            {overview.readiness
              ? overview.readiness.canRequestFirstRealSubscription
                ? "Possible"
                : `Bloquée (${overview.readiness.reasonCode})`
              : "Indisponible"}
          </dd>
        </div>
        <div>
          <dt>Réconciliation</dt>
          <dd>Toutes les {overview.reconciliationIntervalSeconds} secondes</dd>
        </div>
      </dl>

      {overview.readiness && overview.readiness.providers.length > 0 ? (
        <div className="admin-settings-table-scroll">
          <table className="admin-billing-providers">
          <caption>Prestataires et correspondances de prix</caption>
          <thead>
            <tr>
              <th scope="col">Prestataire</th>
              <th scope="col">Environnement</th>
              <th scope="col">Configuré</th>
              <th scope="col">Correspondances</th>
              <th scope="col">Checkout</th>
            </tr>
          </thead>
          <tbody>
            {overview.readiness.providers.map((provider) => (
              <tr key={`${provider.provider}-${provider.environment}`}>
                <th scope="row">{provider.provider}</th>
                <td>{provider.environment}</td>
                <td><StatusBadge label={provider.providerConfigured ? "Configur\u00e9" : "Incomplet"} tone={provider.providerConfigured ? "success" : "warning"} /></td>
                <td>
                  {provider.resolvedMappingCount}/
                  {provider.requiredServicePriceCount}
                </td>
                <td><StatusBadge label={provider.readyForCheckout ? "Pr\u00eat" : "Non pr\u00eat"} tone={provider.readyForCheckout ? "success" : "warning"} /></td>
              </tr>
            ))}
          </tbody>
        </table>
        </div>
      ) : null}

      {overview.readiness && overview.readiness.limitations.length > 0 ? (
        <section aria-label="Limitations opérationnelles">
          <h3>Limitations connues</h3>
          <ul>
            {overview.readiness.limitations.map((limitation) => (
              <li key={limitation.code}>
                <strong>{limitation.severity}</strong> — {limitation.message}
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      <section aria-label="Drapeaux Billing V2">
        <h3>Drapeaux</h3>
        <p className="muted">
          Ces drapeaux sont résolus au démarrage du service : les modifier
          demande une intervention sur la machine, puis un redémarrage. Ils sont
          donc en lecture seule ici — activer un appel sortant réel depuis une
          page web sans qu&apos;un exploitant soit devant la machine serait un
          risque disproportionné.
        </p>
        <div className="admin-billing-flags">
          {overview.flags.map((flag) => (
            <FlagCard flag={flag} key={flag.key} />
          ))}
        </div>
      </section>

      <nav aria-label="Administration Billing V2" className="admin-billing-links">
        <Link className="button button-secondary" href="/admin/catalog">
          Catalogue Billing V2
        </Link>
        <Link className="button button-secondary" href="/admin/billing-v2">
          Readiness et souscriptions
        </Link>
      </nav>
    </>
  );
}

function FlagCard({ flag }: { flag: BillingV2FeatureFlagItem }) {
  return (
    <article
      className={`admin-billing-flag admin-billing-flag-${flag.enabled ? "on" : "off"}`}
    >
      <header>
        <p className="eyebrow">{riskLabels[flag.risk] ?? flag.risk}</p>
        <h4>{flag.label}</h4>
      </header>
      <p>{flag.description}</p>
      <dl>
        <div>
          <dt>État</dt>
          <dd>
            <StatusBadge label={flag.enabled ? "Activ\u00e9" : "D\u00e9sactiv\u00e9"} tone={flag.enabled ? "success" : "neutral"} />
          </dd>
        </div>
        <div>
          <dt>Variable</dt>
          <dd>
            <code>{flag.environmentVariable}</code>
          </dd>
        </div>
        <div>
          <dt>Dépendances</dt>
          <dd>
            {flag.dependencies.length === 0
              ? "Aucune"
              : flag.dependencies.join(", ")}
          </dd>
        </div>
        <div>
          <dt>Modification</dt>
          <dd>Redémarrage requis</dd>
        </div>
      </dl>
      {flag.enabled && flag.unsatisfiedDependencies.length > 0 ? (
        <p role="status">
          Activé mais sans effet : {flag.unsatisfiedDependencies.join(", ")}{" "}
          {flag.unsatisfiedDependencies.length > 1
            ? "sont désactivés"
            : "est désactivé"}
          .
        </p>
      ) : null}
    </article>
  );
}
