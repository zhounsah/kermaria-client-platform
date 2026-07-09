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
  formatSubscriptionRailLabel,
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
  "pending_payment",
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
        description="Suivi des souscriptions récurrentes, engagements, cycles de paiement et résiliations programmées."
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
          detail="Équivalent mensuel HT sur les souscriptions actives"
          label="Revenu mensuel équivalent"
          tone="amber"
          value={formatCurrencyFromCents(monthlyEquivalentCents)}
        />
        <MetricCard
          detail="Tous statuts confondus"
          label="Total enregistrées"
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
              <option value="pending_payment">En attente de paiement</option>
              <option value="pending_activation">Approuvée, activation</option>
              <option value="pending_cancellation">Résiliation programmée</option>
              <option value="active">Active</option>
              <option value="suspended">Suspendue</option>
              <option value="cancelled">Annulée</option>
              <option value="expired">Expirée</option>
            </select>
          </div>
          <div className="field">
            <label htmlFor="customer-filter">Client (référence ou nom)</label>
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
          title="Aucun résultat"
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
                      label={formatSubscriptionRailLabel(item.rail)}
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
                  {item.rail === "stripe"
                    ? (
                        <>
                          Prix Stripe : {item.stripePriceId ?? "—"} · Souscription
                          Stripe : {item.stripeSubscriptionId ?? "—"}
                        </>
                      )
                    : item.rail === "paypal"
                      ? (
                          <>
                            Plan PayPal : {item.paypalPlanId ?? "—"} · Souscription
                            PayPal : {item.paypalSubscriptionId ?? "—"}
                          </>
                        )
                      : "Facture locale Kermaria"}
                </p>
                <p className="field-hint">
                  Prochaine échéance :{" "}
                  {item.nextBillingAt
                    ? formatDateTime(item.nextBillingAt)
                    : "À déterminer"}
                  {" · "}fin d&apos;engagement :{" "}
                  {item.commitmentEndsAt
                    ? formatDateTime(item.commitmentEndsAt)
                    : "—"}
                </p>
                <Link className="button" href={`/admin/subscriptions/${item.id}`}>
                  Voir le détail
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
