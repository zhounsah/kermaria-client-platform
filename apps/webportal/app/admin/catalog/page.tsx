import Link from "next/link";

import { ErrorState } from "@/components/ErrorState";
import { AdminCatalogOfferForm } from "@/components/AdminCatalogOfferForm";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import {
  commercialOfferBillingCadence,
  commercialOfferStatus,
  formatCurrencyFromCents,
  formatDateTime,
} from "@/lib/formatters";
import { getAdminCatalog } from "@/lib/internal-api";

export const metadata = {
  title: "Catalogue - Administration",
};

export const dynamic = "force-dynamic";

type CadenceFilter = "all" | "one_time" | "monthly";

function resolveCadenceFilter(value: unknown): CadenceFilter {
  return value === "monthly" || value === "one_time" ? value : "all";
}

export default async function AdminCatalogPage({
  searchParams,
}: {
  searchParams: Promise<{ cadence?: string }>;
}) {
  await requireAdminSession();
  const result = await getAdminCatalog();
  const { cadence } = await searchParams;
  const cadenceFilter = resolveCadenceFilter(cadence);

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Catalogue administré" tone="info" />}
        description="Ces offres servent au suivi commercial informatif. Aucune commande, facture officielle ou paiement n'est généré depuis cette interface."
        eyebrow="Administration interne"
        title="Catalogue commercial"
      />

      <section className="content-panel admin-safety-panel">
        <div>
          <span className="card-kicker">Avertissement</span>
          <h2>Socle commercial informatif</h2>
          <p>
            Ces documents sont informatifs et ne constituent pas des factures
            officielles. Aucune numérotation fiscale définitive n&apos;est générée
            dans cette version.
          </p>
        </div>
        <StatusBadge label="Aucun paiement possible" tone="warning" />
      </section>

      <SectionCard ariaLabel="Création d&apos;une offre commerciale">
        <h2>Créer une offre</h2>
        <AdminCatalogOfferForm />
      </SectionCard>

      {result.error ? (
        <ErrorState
          description="Impossible de charger le catalogue administré pour le moment."
          reference={result.correlationId}
          title="Catalogue indisponible"
        />
      ) : (
        <section className="request-history-section">
          <div className="section-heading">
            <div>
              <h2>Offres existantes</h2>
              <p>Modification sans suppression définitive.</p>
            </div>
            <nav aria-label="Filtre cadence" className="filter-links">
              <Link
                aria-current={cadenceFilter === "all" ? "page" : undefined}
                href="/admin/catalog"
              >
                Toutes
              </Link>
              <Link
                aria-current={
                  cadenceFilter === "one_time" ? "page" : undefined
                }
                href="/admin/catalog?cadence=one_time"
              >
                Ponctuelles
              </Link>
              <Link
                aria-current={
                  cadenceFilter === "monthly" ? "page" : undefined
                }
                href="/admin/catalog?cadence=monthly"
              >
                Mensuelles
              </Link>
            </nav>
          </div>
          <div className="stack-panels">
            {result.data
              .filter((offer) =>
                cadenceFilter === "all"
                  ? true
                  : offer.billingCadence === cadenceFilter,
              )
              .map((offer) => {
                const status = commercialOfferStatus[offer.status];
                const cadenceBadge =
                  commercialOfferBillingCadence[offer.billingCadence];

                return (
                  <SectionCard
                    ariaLabel={`Offre ${offer.name}`}
                    className="stack-panel"
                    key={offer.id}
                  >
                    <div className="section-heading">
                      <div>
                        <span className="card-kicker">{offer.category}</span>
                        <h2>{offer.name}</h2>
                        <p>
                          {formatCurrencyFromCents(offer.priceAmountCents)} HT · {offer.unitLabel}
                        </p>
                      </div>
                      <div className="badge-stack">
                        <StatusBadge
                          label={cadenceBadge.label}
                          tone={cadenceBadge.tone}
                        />
                        <StatusBadge label={status.label} tone={status.tone} />
                      </div>
                    </div>
                    <p className="request-description">{offer.description}</p>
                    {offer.billingCadence === "monthly" ? (
                      <p className="field-hint">
                        PayPal Plan : {offer.paypalPlanId ?? "non configuré"}
                      </p>
                    ) : null}
                    <p className="field-hint">
                      Créée le {formatDateTime(offer.createdAt)} · mise à jour le{" "}
                      {formatDateTime(offer.updatedAt)}
                    </p>
                    <AdminCatalogOfferForm offer={offer} />
                  </SectionCard>
                );
              })}
          </div>
        </section>
      )}

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
