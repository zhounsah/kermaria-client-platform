import Link from "next/link";
import { notFound } from "next/navigation";

import { AdminCancelSubscriptionButton } from "@/components/AdminCancelSubscriptionButton";
import { AdminReconcileProvisioningButton } from "@/components/AdminReconcileProvisioningButton";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import {
  commercialDocumentStatus,
  formatBillingIntervalMonths,
  formatCommitmentMonths,
  formatCurrencyFromCents,
  formatDateTime,
  formatPaymentModeLabel,
  subscriptionProvisioningStatus,
  subscriptionStatus,
} from "@/lib/formatters";
import { getAdminSubscription } from "@/lib/internal-api";

export const metadata = {
  title: "Detail abonnement - Administration",
};

export const dynamic = "force-dynamic";

const ACTION_STATUS_BADGE = {
  requested: { label: "Demande", tone: "info" },
  running: { label: "En cours", tone: "warning" },
  succeeded: { label: "Succes", tone: "success" },
  unchanged: { label: "Sans changement", tone: "neutral" },
  failed: { label: "Echec", tone: "danger" },
} as const;

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
          description="Detail de la souscription client."
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

  const { subscription, documents, provisioning } = result.data;
  const status = subscriptionStatus[subscription.status];
  const provisioningStatus =
    subscriptionProvisioningStatus[provisioning.status];
  const cancellable =
    subscription.status !== "cancelled"
    && subscription.status !== "expired"
    && subscription.status !== "pending_cancellation";

  return (
    <>
      <PageHeader
        action={<StatusBadge label={status.label} tone={status.tone} />}
        description={`${subscription.customerReference} · ${subscription.customerName}`}
        eyebrow="Administration"
        title={subscription.offerName}
      />

      <SectionCard ariaLabel="Informations generales">
        <h2>Informations generales</h2>
        <dl className="profile-details">
          <div>
            <dt>Offre</dt>
            <dd>{subscription.offerName}</dd>
          </div>
          <div>
            <dt>Reference offre</dt>
            <dd>{subscription.offerExternalReference ?? "—"}</dd>
          </div>
          <div>
            <dt>Pack public</dt>
            <dd>{subscription.publicPackCode ?? "—"}</dd>
          </div>
          <div>
            <dt>Prix par echeance HT</dt>
            <dd>{formatCurrencyFromCents(subscription.priceAmountCents)}</dd>
          </div>
          <div>
            <dt>Mise en service</dt>
            <dd>{formatCurrencyFromCents(subscription.setupFeeAmountCents)} HT</dd>
          </div>
          <div>
            <dt>Cadence</dt>
            <dd>{formatBillingIntervalMonths(subscription.billingIntervalMonths)}</dd>
          </div>
          <div>
            <dt>Engagement</dt>
            <dd>{formatCommitmentMonths(subscription.commitmentMonths)}</dd>
          </div>
          <div>
            <dt>Mode de paiement</dt>
            <dd>{formatPaymentModeLabel(subscription.paymentMode)}</dd>
          </div>
          <div>
            <dt>Cycles payes</dt>
            <dd>{String(subscription.paidCyclesCount)}</dd>
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
            <dt>Demarree le</dt>
            <dd>
              {subscription.startedAt
                ? formatDateTime(subscription.startedAt)
                : "En attente"}
            </dd>
          </div>
          <div>
            <dt>Prochaine echeance</dt>
            <dd>
              {subscription.nextBillingAt
                ? formatDateTime(subscription.nextBillingAt)
                : "A determiner"}
            </dd>
          </div>
          <div>
            <dt>Fin d&apos;engagement</dt>
            <dd>
              {subscription.commitmentEndsAt
                ? formatDateTime(subscription.commitmentEndsAt)
                : "—"}
            </dd>
          </div>
          <div>
            <dt>Resiliation demandee le</dt>
            <dd>
              {subscription.cancelRequestedAt
                ? formatDateTime(subscription.cancelRequestedAt)
                : "—"}
            </dd>
          </div>
          <div>
            <dt>Fin de terme programmee</dt>
            <dd>{subscription.cancelAtTermEnd ? "Oui" : "Non"}</dd>
          </div>
          <div>
            <dt>Annulee le</dt>
            <dd>
              {subscription.cancelledAt
                ? formatDateTime(subscription.cancelledAt)
                : "—"}
            </dd>
          </div>
        </dl>
        {subscription.cancelAtTermEnd ? (
          <p className="field-hint">
            La souscription reste active jusqu&apos;a la prochaine fin de terme
            enregistree.
          </p>
        ) : null}
        <AdminCancelSubscriptionButton
          disabled={!cancellable}
          subscriptionId={subscription.id}
        />
      </SectionCard>

      <SectionCard ariaLabel="Provisioning Active Directory">
        <div className="section-heading">
          <div>
            <h2>Provisioning Active Directory</h2>
            <p className="field-hint">
              Reconciliation calculee au niveau du client, sur tous les liens
              AD utilisateur associes a ce compte.
            </p>
          </div>
          <div className="badge-stack">
            <StatusBadge
              label={provisioningStatus.label}
              tone={provisioningStatus.tone}
            />
          </div>
        </div>
        <dl className="profile-details">
          <div>
            <dt>Groupes mappes</dt>
            <dd>
              {provisioning.mappedGroups.length > 0
                ? provisioning.mappedGroups.join(", ")
                : "Aucun"}
            </dd>
          </div>
          <div>
            <dt>Groupes reconcilies</dt>
            <dd>
              {provisioning.reconciledGroups.length > 0
                ? provisioning.reconciledGroups.join(", ")
                : "Aucun"}
            </dd>
          </div>
          <div>
            <dt>Utilisateurs AD cibles</dt>
            <dd>{String(provisioning.targetUsers.length)}</dd>
          </div>
          <div>
            <dt>Dernier resultat</dt>
            <dd>{provisioning.lastResultCode ?? "—"}</dd>
          </div>
        </dl>
        {provisioning.canRetry ? (
          <AdminReconcileProvisioningButton subscriptionId={subscription.id} />
        ) : (
          <p className="field-hint">
            Relance indisponible tant que le mapping ou la configuration AD
            n&apos;est pas exploitable pour cette offre.
          </p>
        )}

        <div style={{ marginTop: 18 }}>
          <h3>Utilisateurs vises</h3>
          {provisioning.targetUsers.length === 0 ? (
            <p className="field-hint">
              Aucun lien AD utilisateur n&apos;est actuellement rattache a ce
              client.
            </p>
          ) : (
            <ul className="stack-list">
              {provisioning.targetUsers.map((user) => (
                <li className="stack-row" key={user.samAccountName}>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <strong>{user.samAccountName}</strong>
                    <p className="field-hint">
                      {user.displayName}
                      {user.userPrincipalName
                        ? ` · ${user.userPrincipalName}`
                        : ""}
                    </p>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div style={{ marginTop: 18 }}>
          <h3>Dernieres actions</h3>
          {provisioning.recentActions.length === 0 ? (
            <p className="field-hint">
              Aucune action de provisioning enregistree pour cette souscription.
            </p>
          ) : (
            <ul className="stack-list">
              {provisioning.recentActions.map((action) => {
                const actionStatus =
                  ACTION_STATUS_BADGE[
                    action.status as keyof typeof ACTION_STATUS_BADGE
                  ] ?? {
                    label: action.status,
                    tone: "neutral" as const,
                  };
                return (
                  <li className="stack-row" key={action.id}>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <strong>{action.actionType}</strong>
                      <p className="field-hint">
                        {formatDateTime(action.requestedAt)} · correlation{" "}
                        {action.correlationId}
                        {action.resultCode ? ` · ${action.resultCode}` : ""}
                      </p>
                    </div>
                    <StatusBadge
                      label={action.changed ? "Modifie" : "Idempotent"}
                      tone={action.changed ? "success" : "neutral"}
                    />
                    <StatusBadge
                      label={actionStatus.label}
                      tone={actionStatus.tone}
                    />
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      </SectionCard>

      <SectionCard ariaLabel="Factures BPCE liees">
        <h2>Factures BPCE generees</h2>
        {documents.length === 0 ? (
          <p className="field-hint">
            Aucun document n&apos;a encore ete genere pour cette souscription.
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
