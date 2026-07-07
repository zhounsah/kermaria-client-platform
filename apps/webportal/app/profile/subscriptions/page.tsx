import Link from "next/link";

import { ClientCancelSubscriptionButton } from "@/components/ClientCancelSubscriptionButton";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionHeading } from "@/components/SectionHeading";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
import {
  formatBillingIntervalMonths,
  formatCommitmentMonths,
  formatCurrencyFromCents,
  formatDateTime,
  formatPaymentModeLabel,
  subscriptionStatus,
} from "@/lib/formatters";
import { getClientSubscriptions } from "@/lib/internal-api";

export const metadata = {
  title: "Mes souscriptions",
};

export const dynamic = "force-dynamic";

const FLASH_MESSAGES: Record<
  string,
  { tone: "success" | "warning" | "danger"; label: string; text: string }
> = {
  approved: {
    tone: "success",
    label: "Souscription approuvee",
    text:
      "Votre souscription est en cours d'activation. Le provisioning demarrera automatiquement apres validation.",
  },
  cancelled: {
    tone: "warning",
    label: "Parcours interrompu",
    text: "La creation de souscription a ete interrompue avant activation.",
  },
  scheduled: {
    tone: "warning",
    label: "Resiliation programmee",
    text:
      "La resiliation prendra effet a la fin du terme deja engage ou deja paye.",
  },
  terminated: {
    tone: "success",
    label: "Souscription resiliee",
    text:
      "La resiliation a bien ete enregistree. Les acces associes sont en cours de reconciliation.",
  },
  error: {
    tone: "danger",
    label: "Souscription en erreur",
    text:
      "Un probleme est survenu lors du retour de paiement. Verifiez la liste ou reessayez.",
  },
};

function resolveFlash(value: unknown) {
  if (typeof value !== "string") {
    return null;
  }

  return FLASH_MESSAGES[value] ?? null;
}

export default async function ProfileSubscriptionsPage({
  searchParams,
}: {
  searchParams: Promise<{ subscription?: string }>;
}) {
  await requireClientSession();
  const result = await getClientSubscriptions();
  const { subscription } = await searchParams;
  const flash = resolveFlash(subscription);

  return (
    <>
      <PageHeader
        action={
          <Link className="button" href="/services">
            Ajouter / remplacer une offre
          </Link>
        }
        description="Suivi de vos abonnements recurrents, avec resiliation immediate ou differee selon le terme en cours."
        eyebrow="Compte"
        title="Mes souscriptions"
      />

      {flash ? (
        <section className="content-panel" aria-label={flash.label}>
          <StatusBadge label={flash.label} tone={flash.tone} />
          <p style={{ marginTop: 12 }}>{flash.text}</p>
        </section>
      ) : null}

      <section className="request-history-section">
        <SectionHeading
          description="Pour remplacer une offre, souscrivez d'abord a la nouvelle, puis resiliez l'ancienne une fois l'activation confirmee."
          title="Souscriptions actives et historiques recents"
        />
        {result.error ? (
          <ErrorState
            description="Impossible de charger vos souscriptions pour le moment."
            reference={result.correlationId}
            title="Souscriptions indisponibles"
          />
        ) : result.data.length === 0 ? (
          <EmptyState
            action={
              <Link className="button" href="/services">
                Voir les offres
              </Link>
            }
            description="Aucune souscription en cours. Vous pouvez en demarrer une depuis le catalogue de services."
            title="Aucune souscription"
          />
        ) : (
          <div className="stack-panels">
            {result.data.map((item) => {
              const status = subscriptionStatus[item.status];
              const cancellable =
                item.status !== "cancelled"
                && item.status !== "expired"
                && item.status !== "pending_cancellation";

              return (
                <article
                  className="content-panel stack-panel"
                  key={item.id}
                  aria-label={`Souscription ${item.offerName}`}
                >
                  <div className="section-heading">
                    <div>
                      <span className="card-kicker">
                        {item.publicPackCode
                          ? "Pack grand public"
                          : "Offre recurrente"}
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
                      <StatusBadge label={status.label} tone={status.tone} />
                    </div>
                  </div>
                  <dl className="profile-details">
                    <div>
                      <dt>Demarree le</dt>
                      <dd>
                        {item.startedAt
                          ? formatDateTime(item.startedAt)
                          : "En attente"}
                      </dd>
                    </div>
                    <div>
                      <dt>Engagement</dt>
                      <dd>{formatCommitmentMonths(item.commitmentMonths)}</dd>
                    </div>
                    <div>
                      <dt>Mode de paiement</dt>
                      <dd>{formatPaymentModeLabel(item.paymentMode)}</dd>
                    </div>
                    <div>
                      <dt>Mise en service</dt>
                      <dd>{formatCurrencyFromCents(item.setupFeeAmountCents)} HT</dd>
                    </div>
                    <div>
                      <dt>Prochaine echeance</dt>
                      <dd>
                        {item.nextBillingAt
                          ? formatDateTime(item.nextBillingAt)
                          : "A determiner"}
                      </dd>
                    </div>
                    <div>
                      <dt>Fin d&apos;engagement</dt>
                      <dd>
                        {item.commitmentEndsAt
                          ? formatDateTime(item.commitmentEndsAt)
                          : "—"}
                      </dd>
                    </div>
                    <div>
                      <dt>Cycles payes</dt>
                      <dd>{String(item.paidCyclesCount)}</dd>
                    </div>
                    <div>
                      <dt>Reference offre</dt>
                      <dd>{item.offerExternalReference ?? "—"}</dd>
                    </div>
                    <div>
                      <dt>Identifiant paiement</dt>
                      <dd>
                        {item.rail === "stripe"
                          ? item.stripeSubscriptionId ?? "—"
                          : item.paypalSubscriptionId ?? "—"}
                      </dd>
                    </div>
                  </dl>
                  {item.cancelAtTermEnd ? (
                    <p className="field-hint">
                      Resiliation demandee le{" "}
                      {item.cancelRequestedAt
                        ? formatDateTime(item.cancelRequestedAt)
                        : "date indisponible"}
                      {" · "}le service restera actif jusqu&apos;a la fin du terme
                      en cours.
                    </p>
                  ) : null}
                  <p className="field-hint">
                    Souscrite le {formatDateTime(item.createdAt)} · mise a jour
                    le {formatDateTime(item.updatedAt)}
                  </p>
                  <div
                    style={{
                      display: "flex",
                      gap: 12,
                      flexWrap: "wrap",
                      alignItems: "center",
                    }}
                  >
                    <ClientCancelSubscriptionButton
                      disabled={!cancellable}
                      subscriptionId={item.id}
                    />
                    <Link className="button button-secondary" href="/services">
                      Ajouter / remplacer une offre
                    </Link>
                  </div>
                </article>
              );
            })}
          </div>
        )}
      </section>

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
