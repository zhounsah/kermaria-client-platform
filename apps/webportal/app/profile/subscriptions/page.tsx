import Link from "next/link";

import { ClientCancelSubscriptionButton } from "@/components/ClientCancelSubscriptionButton";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionHeading } from "@/components/SectionHeading";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
import { formatCommercialAmountFromCents } from "@/lib/fiscal-formatters";
import {
  formatBillingIntervalMonths,
  formatClientPaymentModeLabel,
  formatCommitmentMonths,
  formatDateTime,
  formatSubscriptionRailLabel,
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
    label: "Souscription approuvée",
    text:
      "Votre souscription est en cours d'activation. Le provisionnement démarrera automatiquement après validation.",
  },
  cancelled: {
    tone: "warning",
    label: "Parcours interrompu",
    text: "La création de souscription a été interrompue avant activation.",
  },
  scheduled: {
    tone: "warning",
    label: "Résiliation programmée",
    text:
      "La résiliation prendra effet à la fin du terme déjà engagé ou déjà payé.",
  },
  terminated: {
    tone: "success",
    label: "Souscription résiliée",
    text:
      "La résiliation a bien été enregistrée. Les accès associés sont en cours de réconciliation.",
  },
  error: {
    tone: "danger",
    label: "Souscription en erreur",
    text:
      "Un problème est survenu lors du retour de paiement. Vérifiez la liste ou réessayez.",
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
        description="Suivi de vos abonnements récurrents, avec résiliation immédiate ou différée selon le terme en cours."
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
          description="Pour remplacer une offre, souscrivez d'abord à la nouvelle, puis résiliez l'ancienne une fois l'activation confirmée."
          title="Souscriptions actives et historiques récents"
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
            description="Aucune souscription en cours. Vous pouvez en démarrer une depuis le catalogue de services."
            title="Aucune souscription"
          />
        ) : (
          <div className="stack-panels">
            {result.data.map((item) => {
              const status = subscriptionStatus[item.status];
              const isBillingV2 = item.billingSystem === "billing_v2";
              // Un contrat regle en une fois n'a pas d'echeance suivante :
              // annoncer une « prochaine echeance », meme « a determiner »,
              // laisse croire a un prelevement qui n'arrivera jamais.
              const isUpfront = item.paymentMode === "upfront";
              // Places « utilisateur supplementaire » reellement portees par
              // le contrat, comptees cote API. Sans place, pas de renvoi : la
              // souscription n'en ouvre aucune, et proposer l'ecran donnerait
              // une page vide sans explication.
              const additionalUserSlots = item.additionalUserSlotsCount ?? 0;
              const assignedAdditionalUsers =
                item.assignedAdditionalUsersCount ?? 0;
              const cancellable =
                !isBillingV2
                && item.status !== "cancelled"
                && item.status !== "expired"
                && item.status !== "pending_cancellation";

              return (
                <article
                  className="content-panel stack-panel"
                  key={item.id}
                  aria-label={`Souscription ${item.label}`}
                >
                  <div className="section-heading">
                    <div>
                      <span className="card-kicker">
                        {item.presetCode ? "Formule" : "Sélection à la carte"}
                      </span>
                      <h2>{item.label}</h2>
                      <p>
                        {formatCommercialAmountFromCents(
                          item.priceAmountCents,
                          { fiscalRegime: item.fiscalRegime },
                        )} ·{" "}
                        {formatBillingIntervalMonths(item.billingIntervalMonths)}
                      </p>
                    </div>
                    <div className="badge-stack">
                      <StatusBadge
                        label={formatSubscriptionRailLabel(item.rail)}
                        tone="info"
                      />
                      <StatusBadge label={status.label} tone={status.tone} />
                    </div>
                  </div>
                  <dl className="profile-details">
                    <div>
                      <dt>Démarrée le</dt>
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
                      <dd>{formatClientPaymentModeLabel(item.paymentMode)}</dd>
                    </div>
                    <div>
                      <dt>Mise en service</dt>
                      <dd>
                        {formatCommercialAmountFromCents(
                          item.setupFeeAmountCents,
                          { fiscalRegime: item.fiscalRegime },
                        )}
                      </dd>
                    </div>
                    {isUpfront ? (
                      <div>
                        <dt>Renouvellement</dt>
                        <dd>Aucun renouvellement automatique</dd>
                      </div>
                    ) : (
                      <div>
                        <dt>Prochaine échéance</dt>
                        <dd>
                          {item.nextBillingAt
                            ? formatDateTime(item.nextBillingAt)
                            : "À déterminer"}
                        </dd>
                      </div>
                    )}
                    <div>
                      <dt>
                        {isUpfront
                          ? "Contrat actif jusqu'au"
                          : "Fin d'engagement"}
                      </dt>
                      <dd>
                        {item.commitmentEndsAt
                          ? formatDateTime(item.commitmentEndsAt)
                          : "—"}
                      </dd>
                    </div>
                    <div>
                      <dt>Cycles payés</dt>
                      <dd>{String(item.paidCyclesCount)}</dd>
                    </div>
                    <div>
                      <dt>Formule</dt>
                      <dd>{item.presetCode ?? "À la carte"}</dd>
                    </div>
                    <div>
                        <dt>Identifiant de paiement</dt>
                      <dd>
                        {item.rail === "stripe"
                          ? item.stripeSubscriptionId ?? "—"
                          : item.rail === "paypal"
                            ? item.paypalSubscriptionId ?? "—"
                            : "Facture locale"}
                      </dd>
                    </div>
                  </dl>
                  {item.cancelAtTermEnd ? (
                    <p className="field-hint">
                      Résiliation demandée le{" "}
                      {item.cancelRequestedAt
                        ? formatDateTime(item.cancelRequestedAt)
                        : "date indisponible"}
                      {" · "}le service restera actif jusqu&apos;à la fin du terme
                      en cours.
                    </p>
                  ) : null}
                  {isBillingV2 ? (
                    <p className="field-hint">
                      Souscription Billing V2 affichée en lecture seule.
                    </p>
                  ) : null}
                  {additionalUserSlots > 0 ? (
                    <p className="field-hint">
                      Utilisateurs supplémentaires : {assignedAdditionalUsers}{" "}
                      / {additionalUserSlots} configurés{" · "}
                      <Link
                        href={`/profile/subscriptions/${encodeURIComponent(item.id)}/users`}
                      >
                        Gérer les utilisateurs
                      </Link>
                    </p>
                  ) : null}
                  <p className="field-hint">
                    Souscrite le {formatDateTime(item.createdAt)} · mise à jour
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
