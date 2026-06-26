import Link from "next/link";

import { AdminDataTable } from "@/components/AdminDataTable";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import {
  commercialOfferBillingCadence,
  commercialOfferStatus,
  formatCurrencyFromCents,
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

  const offers = result.data
    .filter((offer) =>
      cadenceFilter === "all"
        ? true
        : offer.billingCadence === cadenceFilter,
    )
    .sort((a, b) => a.displayOrder - b.displayOrder);

  return (
    <>
      <PageHeader
        action={
          <Link className="button" href="/admin/catalog/new">
            Créer une offre
          </Link>
        }
        description="Catalogue informatif. Chaque ligne ouvre la fiche de l'offre pour modification."
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

      <section className="content-panel admin-filter-panel">
        <div>
          <span className="card-kicker">Filtre</span>
          <h2>Cadence de facturation</h2>
        </div>
        <nav aria-label="Filtre cadence" className="filter-links">
          <Link
            aria-current={cadenceFilter === "all" ? "page" : undefined}
            href="/admin/catalog"
          >
            Toutes
          </Link>
          <Link
            aria-current={cadenceFilter === "one_time" ? "page" : undefined}
            href="/admin/catalog?cadence=one_time"
          >
            Ponctuelles
          </Link>
          <Link
            aria-current={cadenceFilter === "monthly" ? "page" : undefined}
            href="/admin/catalog?cadence=monthly"
          >
            Mensuelles
          </Link>
        </nav>
      </section>

      {result.error ? (
        <ErrorState
          description="Impossible de charger le catalogue administré pour le moment."
          reference={result.correlationId}
          title="Catalogue indisponible"
        />
      ) : offers.length === 0 ? (
        <EmptyState
          description={
            cadenceFilter === "all"
              ? "Aucune offre n'est référencée pour le moment."
              : "Aucune offre ne correspond à ce filtre."
          }
          title="Aucune offre"
        />
      ) : (
        <AdminDataTable
          caption="Offres du catalogue"
          columns={[
            "Ordre",
            "Nom",
            "Catégorie",
            "Prix HT",
            "Cadence",
            "Statut",
            "Action",
          ]}
          rows={offers.map((offer) => {
            const status = commercialOfferStatus[offer.status];
            const cadenceBadge =
              commercialOfferBillingCadence[offer.billingCadence];
            return [
              String(offer.displayOrder),
              <>
                <strong key={`${offer.id}-name`}>{offer.name}</strong>
                <div className="cell-secondary">{offer.unitLabel}</div>
              </>,
              offer.category,
              formatCurrencyFromCents(offer.priceAmountCents),
              <StatusBadge
                key={`${offer.id}-cadence`}
                label={cadenceBadge.label}
                tone={cadenceBadge.tone}
              />,
              <StatusBadge
                key={`${offer.id}-status`}
                label={status.label}
                tone={status.tone}
              />,
              <Link
                className="table-action"
                href={`/admin/catalog/${encodeURIComponent(offer.id)}`}
                key={`${offer.id}-detail`}
              >
                Modifier
              </Link>,
            ];
          })}
        />
      )}

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
