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
  formatSubscriptionRailLabel,
  subscriptionProvisioningStatus,
  subscriptionStatus,
} from "@/lib/formatters";
import { getAdminSubscription } from "@/lib/internal-api";

export const metadata = {
  title: "Détail abonnement - Administration",
};

export const dynamic = "force-dynamic";

const ACTION_STATUS_BADGE = {
  requested: { label: "Demande", tone: "info" },
  running: { label: "En cours", tone: "warning" },
  succeeded: { label: "Succès", tone: "success" },
  unchanged: { label: "Sans changement", tone: "neutral" },
  failed: { label: "Échec", tone: "danger" },
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
          description="Détail de la souscription client."
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
        description={`${subscription.customerReference} - ${subscription.customerName}`}
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
            <dt>Référence offre</dt>
            <dd>{subscription.offerExternalReference ?? "—"}</dd>
          </div>
          <div>
            <dt>Pack public</dt>
            <dd>{subscription.publicPackCode ?? "—"}</dd>
          </div>
          <div>
            <dt>Prix par échéance HT</dt>
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
            <dt>Cycles payés</dt>
            <dd>{String(subscription.paidCyclesCount)}</dd>
          </div>
          <div>
            <dt>Rail</dt>
            <dd>{formatSubscriptionRailLabel(subscription.rail)}</dd>
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
          ) : subscription.rail === "paypal" ? (
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
          ) : (
            <>
              <div>
                <dt>Facturation</dt>
                <dd>Facture locale Kermaria</dd>
              </div>
              <div>
                <dt>Encaissement</dt>
                <dd>Stripe, PayPal ou virement depuis la facture</dd>
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
            <dt>Fin d&apos;engagement</dt>
            <dd>
              {subscription.commitmentEndsAt
                ? formatDateTime(subscription.commitmentEndsAt)
                : "—"}
            </dd>
          </div>
          <div>
            <dt>Résiliation demandée le</dt>
            <dd>
              {subscription.cancelRequestedAt
                ? formatDateTime(subscription.cancelRequestedAt)
                : "—"}
            </dd>
          </div>
          <div>
            <dt>Fin de terme programmée</dt>
            <dd>{subscription.cancelAtTermEnd ? "Oui" : "Non"}</dd>
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
        {subscription.cancelAtTermEnd ? (
          <p className="field-hint">
            La souscription reste active jusqu&apos;à la prochaine fin de terme
            enregistrée.
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
              Réconciliation calculée au niveau du client, sur tous les liens
              AD utilisateur associés à ce compte.
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
            <dt>Groupes mappés</dt>
            <dd>
              {provisioning.mappedGroups.length > 0
                ? provisioning.mappedGroups.join(", ")
                : "Aucun"}
            </dd>
          </div>
          <div>
            <dt>Groupes réconciliés</dt>
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
            <dt>Dernier résultat</dt>
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
          <h3>Utilisateurs visés</h3>
          {provisioning.targetUsers.length === 0 ? (
            <p className="field-hint">
              Aucun lien AD utilisateur n&apos;est actuellement rattaché à ce
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
                        ? ` - ${user.userPrincipalName}`
                        : ""}
                    </p>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div style={{ marginTop: 18 }}>
          <h3>Dernières actions</h3>
          {provisioning.recentActions.length === 0 ? (
            <p className="field-hint">
              Aucune action de provisioning enregistrée pour cette souscription.
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
                        {formatDateTime(action.requestedAt)} - corrélation{" "}
                        {action.correlationId}
                        {action.resultCode ? ` - ${action.resultCode}` : ""}
                      </p>
                    </div>
                    <StatusBadge
                      label={action.changed ? "Modifié" : "Idempotent"}
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
                      {doc.title} - {formatDateTime(doc.createdAt)}
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
