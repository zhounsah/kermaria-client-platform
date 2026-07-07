import Link from "next/link";

import type { SubscriptionStatus } from "@kermaria/shared";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MetricCard } from "@/components/MetricCard";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import {
  formatBillingIntervalMonths,
  formatCommitmentMonths,
  formatCurrencyFromCents,
  formatDateTime,
  formatPaymentModeLabel,
  subscriptionStatus,
} from "@/lib/formatters";
import { getAdminSubscriptions } from "@/lib/internal-api";

export const metadata = {
  title: "Abonnements - Administration",
};

export const dynamic = "force-dynamic";

const STATUS_FILTERS: ReadonlyArray<"all" | SubscriptionStatus> = [
  "all",
  "pending_approval",
  "pending_activation",
  "pending_cancellation",
  "active",
  "suspended",
  "cancelled",
  "expired",
];

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

export default async function AdminSubscriptionsPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  await requireAdminSession();
  const params = await searchParams;
  const rawStatus = first(params.status);
  const status = (
    STATUS_FILTERS.includes(rawStatus as "all" | SubscriptionStatus)
      ? rawStatus
      : "all"
  ) as "all" | SubscriptionStatus;
  const customerFilter = first(params.customer)?.trim().toLowerCase() ?? "";

  const result = await getAdminSubscriptions();

  const filtered = result.data.filter((subscription) => {
    if (status !== "all" && subscription.status !== status) {
      return false;
    }
    if (
      customerFilter.length > 0
      && !subscription.customerReference.toLowerCase().includes(customerFilter)
      && !subscription.customerName.toLowerCase().includes(customerFilter)
    ) {
      return false;
    }
    return true;
  });

  const activeCount = result.data.filter((item) => item.status === "active").length;
  const monthlyEquivalentCents = result.data
    .filter((item) => item.status === "active")
    .reduce(
      (sum, item) =>
        sum
        + Math.round(
          item.priceAmountCents / Math.max(1, item.billingIntervalMonths),
        ),
      0,
    );

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Vue admin" tone="info" />}
        description="Suivi des souscriptions recurrentes, engagements, cycles de paiement et resiliations programmees."
        eyebrow="Administration"
        title="Abonnements"
      />

      <section
        aria-label="Indicateurs abonnements"
        className="metrics-grid metrics-grid-three"
      >
        <MetricCard
          detail="Souscriptions actuellement actives"
          label="Souscriptions actives"
          tone="green"
          value={String(activeCount)}
        />
        <MetricCard
          detail="Equivalent mensuel HT sur les souscriptions actives"
          label="Revenu mensuel equivalent"
          tone="amber"
          value={formatCurrencyFromCents(monthlyEquivalentCents)}
        />
        <MetricCard
          detail="Tous statuts confondus"
          label="Total enregistrees"
          tone="slate"
          value={String(result.data.length)}
        />
      </section>

      <section className="content-panel">
        <form
          className="admin-filters"
          action="/admin/subscriptions"
          method="GET"
        >
          <div className="field">
            <label htmlFor="status-filter">Statut</label>
            <select defaultValue={status} id="status-filter" name="status">
              <option value="all">Tous</option>
              <option value="pending_approval">En attente d&apos;approbation</option>
              <option value="pending_activation">Approuvee, activation</option>
              <option value="pending_cancellation">Resiliation programmee</option>
              <option value="active">Active</option>
              <option value="suspended">Suspendue</option>
              <option value="cancelled">Annulee</option>
              <option value="expired">Expiree</option>
            </select>
          </div>
          <div className="field">
            <label htmlFor="customer-filter">Client (reference ou nom)</label>
            <input
              defaultValue={customerFilter}
              id="customer-filter"
              maxLength={120}
              name="customer"
              placeholder="ex. CLI-DEMO-0042"
            />
          </div>
          <button className="button button-secondary" type="submit">
            Appliquer
          </button>
        </form>
      </section>

      {result.error ? (
        <ErrorState
          description="Impossible de charger la liste des abonnements pour le moment."
          reference={result.correlationId}
          title="Abonnements indisponibles"
        />
      ) : filtered.length === 0 ? (
        <EmptyState
          description="Aucune souscription ne correspond aux filtres choisis."
          title="Aucun resultat"
        />
      ) : (
        <div className="stack-panels">
          {filtered.map((item) => {
            const statusBadge = subscriptionStatus[item.status];
            return (
              <SectionCard
                ariaLabel={`Abonnement ${item.offerName}`}
                className="stack-panel"
                key={item.id}
              >
                <div className="section-heading">
                  <div>
                    <span className="card-kicker">
                      {item.customerReference} · {item.customerName}
                    </span>
                    <h2>{item.offerName}</h2>
                    <p>
                      {formatCurrencyFromCents(item.priceAmountCents)} HT ·{" "}
                      {formatBillingIntervalMonths(item.billingIntervalMonths)}
                    </p>
                  </div>
                  <div className="badge-stack">
                    <StatusBadge
                      label={item.rail === "stripe" ? "Stripe" : "PayPal"}
                      tone="info"
                    />
                    <StatusBadge
                      label={statusBadge.label}
                      tone={statusBadge.tone}
                    />
                  </div>
                </div>
                <p className="field-hint">
                  {formatCommitmentMonths(item.commitmentMonths)} ·{" "}
                  {formatPaymentModeLabel(item.paymentMode)} · mise en service{" "}
                  {formatCurrencyFromCents(item.setupFeeAmountCents)} HT
                </p>
                <p className="field-hint">
                  {item.rail === "stripe" ? (
                    <>
                      Prix Stripe : {item.stripePriceId ?? "—"} · Souscription
                      Stripe : {item.stripeSubscriptionId ?? "—"}
                    </>
                  ) : (
                    <>
                      Plan PayPal : {item.paypalPlanId ?? "—"} · Souscription
                      PayPal : {item.paypalSubscriptionId ?? "—"}
                    </>
                  )}
                </p>
                <p className="field-hint">
                  Prochaine echeance :{" "}
                  {item.nextBillingAt
                    ? formatDateTime(item.nextBillingAt)
                    : "A determiner"}
                  {" · "}fin d&apos;engagement :{" "}
                  {item.commitmentEndsAt
                    ? formatDateTime(item.commitmentEndsAt)
                    : "—"}
                </p>
                <Link className="button" href={`/admin/subscriptions/${item.id}`}>
                  Voir le detail
                </Link>
              </SectionCard>
            );
          })}
        </div>
      )}

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
