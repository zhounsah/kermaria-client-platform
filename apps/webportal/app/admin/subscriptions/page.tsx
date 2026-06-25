import Link from "next/link";

import type { SubscriptionStatus } from "@kermaria/shared";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import {
  formatCurrencyFromCents,
  formatDateTime,
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

  const activeCount = result.data.filter(
    (item) => item.status === "active",
  ).length;
  const mrrCents = result.data
    .filter((item) => item.status === "active")
    .reduce((sum, item) => sum + item.priceAmountCents, 0);

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Vue admin" tone="info" />}
        description="Suivi des souscriptions mensuelles PayPal et de leurs facturations BPCE."
        eyebrow="Administration"
        title="Abonnements"
      />

      <section className="metrics-grid" aria-label="Indicateurs abonnements">
        <article className="content-panel">
          <span className="card-kicker">Souscriptions actives</span>
          <h2>{activeCount}</h2>
        </article>
        <article className="content-panel">
          <span className="card-kicker">MRR estimé HT</span>
          <h2>{formatCurrencyFromCents(mrrCents)}</h2>
        </article>
        <article className="content-panel">
          <span className="card-kicker">Total enregistrées</span>
          <h2>{result.data.length}</h2>
        </article>
      </section>

      <section className="content-panel">
        <form className="form-grid" action="/admin/subscriptions" method="GET">
          <label>
            Statut
            <select defaultValue={status} name="status">
              <option value="all">Tous</option>
              <option value="pending_approval">En attente d&apos;approbation</option>
              <option value="pending_activation">Approuvée, activation</option>
              <option value="active">Active</option>
              <option value="suspended">Suspendue</option>
              <option value="cancelled">Annulée</option>
              <option value="expired">Expirée</option>
            </select>
          </label>
          <label>
            Client (référence ou nom)
            <input
              defaultValue={customerFilter}
              maxLength={120}
              name="customer"
              placeholder="ex. CLI-DEMO-0042"
            />
          </label>
          <button className="button" type="submit">
            Filtrer
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
            const status = subscriptionStatus[item.status];
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
                      {formatCurrencyFromCents(item.priceAmountCents)} HT / mois
                    </p>
                  </div>
                  <div className="badge-stack">
                    <StatusBadge label={status.label} tone={status.tone} />
                  </div>
                </div>
                <p className="field-hint">
                  Plan PayPal : {item.paypalPlanId} · Souscription PayPal :{" "}
                  {item.paypalSubscriptionId ?? "—"}
                </p>
                <p className="field-hint">
                  Prochaine échéance :{" "}
                  {item.nextBillingAt
                    ? formatDateTime(item.nextBillingAt)
                    : "À déterminer"}
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
