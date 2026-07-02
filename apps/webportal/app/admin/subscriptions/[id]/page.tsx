import Link from "next/link";
import { notFound } from "next/navigation";

import { AdminCancelSubscriptionButton } from "@/components/AdminCancelSubscriptionButton";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import {
  commercialDocumentStatus,
  formatCurrencyFromCents,
  formatDateTime,
  subscriptionStatus,
} from "@/lib/formatters";
import { getAdminSubscription } from "@/lib/internal-api";

export const metadata = {
  title: "Détail abonnement - Administration",
};

export const dynamic = "force-dynamic";

export default async function AdminSubscriptionDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  await requireAdminSession();
  const { id } = await params;
  const result = await getAdminSubscription(id);

  if (result.error) {
    return (
      <>
        <PageHeader
          description="Détail de la souscription PayPal."
          eyebrow="Administration"
          title="Abonnement"
        />
        <ErrorState
          description="Impossible de charger la souscription pour le moment."
          reference={result.correlationId}
          title="Abonnement indisponible"
        />
      </>
    );
  }

  if (!result.data) {
    notFound();
  }

  const { subscription, documents } = result.data;
  const status = subscriptionStatus[subscription.status];
  const cancellable =
    subscription.status !== "cancelled" && subscription.status !== "expired";

  return (
    <>
      <PageHeader
        action={<StatusBadge label={status.label} tone={status.tone} />}
        description={`${subscription.customerReference} · ${subscription.customerName}`}
        eyebrow="Administration"
        title={subscription.offerName}
      />

      <SectionCard ariaLabel="Informations générales">
        <h2>Informations générales</h2>
        <dl className="profile-details">
          <div>
            <dt>Offre</dt>
            <dd>{subscription.offerName}</dd>
          </div>
          <div>
            <dt>Prix mensuel HT</dt>
            <dd>{formatCurrencyFromCents(subscription.priceAmountCents)}</dd>
          </div>
          <div>
            <dt>Rail</dt>
            <dd>{subscription.rail === "stripe" ? "Stripe" : "PayPal"}</dd>
          </div>
          {subscription.rail === "stripe" ? (
            <>
              <div>
                <dt>Prix Stripe</dt>
                <dd>{subscription.stripePriceId ?? "—"}</dd>
              </div>
              <div>
                <dt>Souscription Stripe</dt>
                <dd>{subscription.stripeSubscriptionId ?? "—"}</dd>
              </div>
            </>
          ) : (
            <>
              <div>
                <dt>Plan PayPal</dt>
                <dd>{subscription.paypalPlanId ?? "—"}</dd>
              </div>
              <div>
                <dt>Souscription PayPal</dt>
                <dd>{subscription.paypalSubscriptionId ?? "—"}</dd>
              </div>
            </>
          )}
          <div>
            <dt>Démarrée le</dt>
            <dd>
              {subscription.startedAt
                ? formatDateTime(subscription.startedAt)
                : "En attente"}
            </dd>
          </div>
          <div>
            <dt>Prochaine échéance</dt>
            <dd>
              {subscription.nextBillingAt
                ? formatDateTime(subscription.nextBillingAt)
                : "À déterminer"}
            </dd>
          </div>
          <div>
            <dt>Annulée le</dt>
            <dd>
              {subscription.cancelledAt
                ? formatDateTime(subscription.cancelledAt)
                : "—"}
            </dd>
          </div>
        </dl>
        <AdminCancelSubscriptionButton
          disabled={!cancellable}
          subscriptionId={subscription.id}
        />
      </SectionCard>

      <SectionCard ariaLabel="Factures BPCE liées">
        <h2>Factures BPCE générées</h2>
        {documents.length === 0 ? (
          <p className="field-hint">
            Aucun document n&apos;a encore été généré pour cette souscription.
          </p>
        ) : (
          <ul className="stack-list">
            {documents.map((doc) => {
              const docStatus = commercialDocumentStatus[doc.status];
              return (
                <li className="stack-row" key={doc.id}>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <strong>{doc.internalReference}</strong>
                    <p className="field-hint">
                      {doc.title} · {formatDateTime(doc.createdAt)}
                    </p>
                  </div>
                  <strong>{formatCurrencyFromCents(doc.totalAmountCents)}</strong>
                  <StatusBadge label={docStatus.label} tone={docStatus.tone} />
                  <Link
                    className="button"
                    href={`/admin/commercial-documents/${doc.id}`}
                  >
                    Voir
                  </Link>
                </li>
              );
            })}
          </ul>
        )}
      </SectionCard>

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
