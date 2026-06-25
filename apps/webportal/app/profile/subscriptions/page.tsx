import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionHeading } from "@/components/SectionHeading";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
import {
  formatCurrencyFromCents,
  formatDateTime,
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
    text: "Votre souscription est en cours d'activation. La facturation démarre dès qu'elle est confirmée par PayPal.",
  },
  cancelled: {
    tone: "warning",
    label: "Souscription annulée",
    text: "Vous avez annulé la souscription avant son activation.",
  },
  error: {
    tone: "danger",
    label: "Souscription en erreur",
    text: "Un problème est survenu lors du retour PayPal. Vérifiez la liste ou réessayez.",
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
        action={<StatusBadge label="Vue client" tone="info" />}
        description="Suivi des abonnements mensuels rattachés à votre compte."
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
          description="Les souscriptions sont gérées par PayPal pour la partie paiement et par notre back-office pour la facturation BPCE."
          title="Souscriptions actives et en cours"
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
              return (
                <article
                  className="content-panel stack-panel"
                  key={item.id}
                  aria-label={`Souscription ${item.offerName}`}
                >
                  <div className="section-heading">
                    <div>
                      <span className="card-kicker">Offre mensuelle</span>
                      <h2>{item.offerName}</h2>
                      <p>
                        {formatCurrencyFromCents(item.priceAmountCents)} HT /
                        mois
                      </p>
                    </div>
                    <StatusBadge label={status.label} tone={status.tone} />
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
                      <dt>Prochaine échéance</dt>
                      <dd>
                        {item.nextBillingAt
                          ? formatDateTime(item.nextBillingAt)
                          : "À déterminer"}
                      </dd>
                    </div>
                    <div>
                      <dt>Identifiant PayPal</dt>
                      <dd>{item.paypalSubscriptionId ?? "—"}</dd>
                    </div>
                  </dl>
                  <p className="field-hint">
                    Souscrite le {formatDateTime(item.createdAt)} · mise à jour
                    le {formatDateTime(item.updatedAt)}
                  </p>
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
