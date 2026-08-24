import type { SubscriptionSummary } from "@kermaria/shared";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MetricCard } from "@/components/MetricCard";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatCommercialAmountFromCents } from "@/lib/fiscal-formatters";
import {
  formatDateTime,
  formatSubscriptionRailLabel,
  subscriptionStatus,
} from "@/lib/formatters";
import {
  getAdminBillingV2Readiness,
  getAdminBillingV2Subscriptions,
} from "@/lib/internal-api";
import type { BillingV2AdminProviderReadiness } from "@/lib/internal-api";

export const metadata = {
  title: "Billing V2 - Administration",
};

export const dynamic = "force-dynamic";

export default async function AdminBillingV2Page() {
  await requireAdminSession();
  const [readinessResult, subscriptionsResult] = await Promise.all([
    getAdminBillingV2Readiness(),
    getAdminBillingV2Subscriptions(),
  ]);
  const snapshot = readinessResult.data;
  const billingV2Subscriptions = subscriptionsResult.data;

  return (
    <>
      <PageHeader
        action={
          <StatusBadge
            label={snapshot?.canRequestFirstRealSubscription ? "Prêt" : "Bloqué"}
            tone={snapshot?.canRequestFirstRealSubscription ? "success" : "warning"}
          />
        }
        description="Contrôles lecture seule avant toute demande de premier abonnement réel en Billing V2."
        eyebrow="Administration interne"
        title="Billing V2"
      />

      {readinessResult.error ? (
        <ErrorState
          description="Impossible de charger la readiness Billing V2 pour le moment."
          reference={readinessResult.correlationId}
          title="Readiness indisponible"
        />
      ) : snapshot ? (
        <>
          <section
            aria-label="Synthèse readiness Billing V2"
            className="metrics-grid metrics-grid-three"
          >
            <MetricCard
              detail="Migration 071 appliquée"
              label="Modèle commercial legacy"
              tone={
                snapshot.launchReadiness.legacyBillingSchemaRemoved
                && snapshot.launchReadiness.verifiedAgainstPersistentSql
                  ? "green"
                  : "amber"
              }
              value={
                snapshot.launchReadiness.legacyBillingSchemaRemoved
                  ? "Supprimé"
                  : "Présent"
              }
            />
            <MetricCard
              detail="Tables encore présentes en base"
              label="Reliquats legacy"
              tone={
                snapshot.launchReadiness.remainingLegacyTables.length === 0
                  ? "slate"
                  : "amber"
              }
              value={String(
                snapshot.launchReadiness.remainingLegacyTables.length,
              )}
            />
            <MetricCard
              detail={snapshot.reasonCode}
              label="Décision"
              tone={snapshot.canRequestFirstRealSubscription ? "green" : "amber"}
              value={snapshot.canRequestFirstRealSubscription ? "Autorisable" : "Fermé"}
            />
          </section>

          <section className="content-panel admin-safety-panel">
            <div>
              <span className="card-kicker">Gate premier abonnement</span>
              <h2>
                {snapshot.canRequestFirstRealSubscription
                  ? "Toutes les conditions consultées sont satisfaites"
                  : "Billing V2 reste fermé"}
              </h2>
              <p>
                Billing V2 est la seule autorité commerciale. La porte reste
                fermée tant que le schéma legacy n&apos;a pas disparu : deux
                catalogues simultanés produiraient deux vérités tarifaires.
              </p>
            </div>
            <StatusBadge
              label={snapshot.reasonCode}
              tone={snapshot.canRequestFirstRealSubscription ? "success" : "warning"}
            />
          </section>

          <div className="stack-panels">
            {snapshot.operationalLimitations.length > 0 ? (
              <section className="content-panel">
                <div className="section-heading">
                  <div>
                    <span className="card-kicker">Limites opérationnelles</span>
                    <h2>Validation humaine requise</h2>
                    <p>
                      Ces points ne déclenchent aucune action automatique, mais
                      doivent être connus avant le premier vrai abonnement V2.
                    </p>
                  </div>
                  <StatusBadge
                    label={`${snapshot.operationalLimitations.length} point(s)`}
                    tone="warning"
                  />
                </div>
                <div className="stack-panels">
                  {snapshot.operationalLimitations.map((limitation) => (
                    <article key={limitation.code}>
                      <div className="section-heading">
                        <div>
                          <span className="card-kicker">{limitation.severity}</span>
                          <h2>{limitation.code}</h2>
                          <p>{limitation.message}</p>
                        </div>
                        <StatusBadge label="Revue" tone="warning" />
                      </div>
                    </article>
                  ))}
                </div>
              </section>
            ) : null}

            <section className="content-panel">
              <div className="section-heading">
                <div>
                  <span className="card-kicker">Flags runtime</span>
                  <h2>Activation contrôlée</h2>
                  <p>
                    Flags globaux, checkout, provider, SQL, mappings et
                    validation humaine sont composés dans une décision unique.
                  </p>
                </div>
              </div>
              <dl className="detail-grid">
                {Object.entries(snapshot.runtimeFlags).map(([key, value]) => (
                  <div key={key}>
                    <dt>{formatFlagLabel(key)}</dt>
                    <dd>
                      <StatusBadge
                        label={value ? "true" : "false"}
                        tone={value ? "success" : "neutral"}
                      />
                    </dd>
                  </div>
                ))}
              </dl>
            </section>

            <BillingV2SubscriptionsPanel
              error={subscriptionsResult.error ? subscriptionsResult.correlationId : null}
              subscriptions={billingV2Subscriptions}
            />

            <section className="content-panel">
              <div className="section-heading">
                <div>
                  <span className="card-kicker">Schéma et base</span>
                  <h2>Préconditions SQL</h2>
                  <p>
                    La vérification attend un SQL persistant et toutes les
                    tables Billing V2 additives.
                  </p>
                </div>
                <div className="badge-stack">
                  <StatusBadge
                    label={snapshot.persistentSqlAvailable ? "SQL persistant" : "SQL absent"}
                    tone={snapshot.persistentSqlAvailable ? "success" : "warning"}
                  />
                  <StatusBadge
                    label={
                      snapshot.launchReadiness.verifiedAgainstPersistentSql
                        ? "Schéma vérifié"
                        : "Schéma non vérifié"
                    }
                    tone={
                      snapshot.launchReadiness.verifiedAgainstPersistentSql
                        ? "success"
                        : "warning"
                    }
                  />
                  <StatusBadge
                    label={snapshot.schemaReady ? "Schéma prêt" : "Schéma incomplet"}
                    tone={snapshot.schemaReady ? "success" : "warning"}
                  />
                </div>
              </div>
              {snapshot.missingSchemaTables.length === 0 ? (
                <p className="field-hint">Aucune table Billing V2 requise ne manque.</p>
              ) : (
                <p className="field-hint">
                  Tables manquantes : {snapshot.missingSchemaTables.join(", ")}
                </p>
              )}
            </section>

            {snapshot.launchReadiness.remainingLegacyTables.length > 0 ? (
              <section className="content-panel">
                <div className="section-heading">
                  <div>
                    <span className="card-kicker">Reliquats bloquants</span>
                    <h2>Tables du modèle commercial legacy encore présentes</h2>
                    <p>
                      Ces noms viennent d&apos;une lecture seule de
                      <code> information_schema</code>. Tant qu&apos;une de ces
                      tables existe, un second catalogue reste interrogeable et
                      la porte de lancement reste fermée. La migration 071 les
                      supprime définitivement.
                    </p>
                  </div>
                  <StatusBadge
                    label={`${snapshot.launchReadiness.remainingLegacyTables.length} table(s)`}
                    tone="warning"
                  />
                </div>
                <ul className="field-hint">
                  {snapshot.launchReadiness.remainingLegacyTables.map(
                    (table) => (
                      <li key={table}>{table}</li>
                    ),
                  )}
                </ul>
              </section>
            ) : null}

            <section className="content-panel">
              <div className="section-heading">
                <div>
                  <span className="card-kicker">Stripe / PayPal</span>
                  <h2>Mappings provider</h2>
                  <p>
                    Chaque prix de service actif doit avoir exactement un id
                    provider résolu dans l&apos;environnement courant.
                  </p>
                </div>
              </div>
              <div className="stack-panels">
                {snapshot.providers.length === 0 ? (
                  <p className="field-hint">Aucun provider vérifiable dans ce snapshot.</p>
                ) : (
                  snapshot.providers.map((provider) => (
                    <ProviderReadiness
                      key={`${provider.provider}-${provider.environment}`}
                      provider={provider}
                    />
                  ))
                )}
              </div>
            </section>
          </div>
        </>
      ) : (
        <EmptyState
          description="Aucun snapshot de readiness Billing V2 n'a été retourné."
          title="Snapshot absent"
        />
      )}

      <MockNotice
        correlationId={readinessResult.correlationId}
        source={readinessResult.source}
      />
    </>
  );
}

function BillingV2SubscriptionsPanel({
  error,
  subscriptions,
}: {
  error: string | null;
  subscriptions: SubscriptionSummary[];
}) {
  const activeCount = subscriptions.filter(
    (subscription) => subscription.status === "active",
  ).length;
  const monthlyEquivalentCents = subscriptions
    .filter((subscription) => subscription.status === "active")
    .reduce(
      (sum, subscription) =>
        sum
        + Math.round(
          subscription.priceAmountCents
          / Math.max(1, subscription.billingIntervalMonths),
        ),
      0,
    );

  return (
    <section className="content-panel">
      <div className="section-heading">
        <div>
          <span className="card-kicker">Souscriptions V2</span>
          <h2>Abonnements autoritaires</h2>
          <p className="field-hint">
            {activeCount} active(s) · {formatCommercialAmountFromCents(
              monthlyEquivalentCents,
              { fiscalRegime: "franchise_base" },
            )} mensuel équivalent
          </p>
        </div>
        <StatusBadge
          label={`${subscriptions.length} visible(s)`}
          tone={subscriptions.length > 0 ? "info" : "neutral"}
        />
      </div>
      {error ? (
        <ErrorState
          description="Impossible de charger les abonnements Billing V2 pour le moment."
          reference={error}
          title="Souscriptions V2 indisponibles"
        />
      ) : subscriptions.length === 0 ? (
        <p className="field-hint">Aucune souscription Billing V2 autoritaire.</p>
      ) : (
        <div className="table-scroll">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Abonnement</th>
                <th>Client</th>
                <th>Statut</th>
                <th>Rail</th>
                <th>Montant</th>
                <th>Échéance</th>
              </tr>
            </thead>
            <tbody>
              {subscriptions.map((subscription) => {
                const status = subscriptionStatus[subscription.status];
                return (
                  <tr key={subscription.id}>
                    <td>{subscription.id}</td>
                    <td>
                      {subscription.customerName}
                      <span className="field-hint">
                        {subscription.customerReference}
                      </span>
                    </td>
                    <td>
                      <StatusBadge label={status.label} tone={status.tone} />
                    </td>
                    <td>{formatSubscriptionRailLabel(subscription.rail)}</td>
                    <td>
                      {formatCommercialAmountFromCents(
                        subscription.priceAmountCents,
                        { fiscalRegime: subscription.fiscalRegime },
                      )}
                    </td>
                    <td>
                      {subscription.nextBillingAt
                        ? formatDateTime(subscription.nextBillingAt)
                        : "À déterminer"}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function ProviderReadiness({
  provider,
}: {
  provider: BillingV2AdminProviderReadiness;
}) {
  return (
    <article>
      <div className="section-heading">
        <div>
          <span className="card-kicker">{provider.environment}</span>
          <h2>{provider.provider}</h2>
          <p>
            {provider.resolvedMappingCount} mapping(s) résolu(s) sur{" "}
            {provider.requiredServicePriceCount} prix requis.
          </p>
        </div>
        <div className="badge-stack">
          <StatusBadge
            label={provider.providerConfigured ? "Provider configuré" : "Provider fermé"}
            tone={provider.providerConfigured ? "success" : "warning"}
          />
          <StatusBadge
            label={provider.readyForCheckout ? "Checkout prêt" : "Checkout bloqué"}
            tone={provider.readyForCheckout ? "success" : "warning"}
          />
        </div>
      </div>
      {provider.missingServicePriceIds.length > 0 ? (
        <p className="field-hint">
          Prix sans mapping : {provider.missingServicePriceIds.join(", ")}
        </p>
      ) : null}
      {provider.ambiguousServicePriceIds.length > 0 ? (
        <p className="field-hint">
          Prix ambigus : {provider.ambiguousServicePriceIds.join(", ")}
        </p>
      ) : null}
      {provider.missingServicePriceIds.length === 0
        && provider.ambiguousServicePriceIds.length === 0 ? (
          <p className="field-hint">Aucun mapping manquant ou ambigu détecté.</p>
        ) : null}
    </article>
  );
}

function formatFlagLabel(key: string) {
  return key
    .replace(/Enabled$/, "")
    .replace(/([A-Z])/g, " $1")
    .replace(/^./, (char) => char.toUpperCase())
    .trim();
}
