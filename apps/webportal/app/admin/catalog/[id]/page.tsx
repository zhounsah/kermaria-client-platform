import Link from "next/link";
import { notFound } from "next/navigation";

import { AdminCatalogOfferForm } from "@/components/AdminCatalogOfferForm";
import { ErrorState } from "@/components/ErrorState";
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
  title: "Détail de l'offre - Catalogue",
};

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ id: string }>;
};

export default async function AdminCatalogOfferPage({ params }: PageProps) {
  await requireAdminSession();
  const { id } = await params;
  const result = await getAdminCatalog();

  if (result.error) {
    return (
      <>
        <PageHeader
          description="L'offre demandée est temporairement indisponible."
          eyebrow="Catalogue commercial"
          title="Détail de l'offre"
        />
        <ErrorState
          description="Impossible de charger le catalogue pour le moment."
          reference={result.correlationId}
          title="Catalogue indisponible"
        />
      </>
    );
  }

  const offer = result.data.find((item) => item.id === id);
  if (!offer) {
    notFound();
  }

  const status = commercialOfferStatus[offer.status];
  const cadenceBadge = commercialOfferBillingCadence[offer.billingCadence];

  return (
    <>
      <PageHeader
        action={
          <div className="badge-stack">
            <StatusBadge label={cadenceBadge.label} tone={cadenceBadge.tone} />
            <StatusBadge label={status.label} tone={status.tone} />
          </div>
        }
        description={`${offer.category} · ${formatCurrencyFromCents(offer.priceAmountCents)} HT · ${offer.unitLabel}`}
        eyebrow="Catalogue commercial"
        title={offer.name}
      />

      <p>
        <Link className="text-link" href="/admin/catalog">
          ← Retour au catalogue
        </Link>
      </p>

      <SectionCard ariaLabel={`Aperçu de l'offre ${offer.name}`}>
        <h2>Description publique</h2>
        <p className="request-description">{offer.description}</p>
        {offer.billingCadence === "monthly" ? (
          <p className="field-hint">
            PayPal Plan sandbox : {offer.paypalPlanIdSandbox ?? "non créé"} ·
            live : {offer.paypalPlanIdLive ?? "non créé"}
          </p>
        ) : null}
        <p className="field-hint">
          Créée le {formatDateTime(offer.createdAt)} · mise à jour le{" "}
          {formatDateTime(offer.updatedAt)}
        </p>
      </SectionCard>

      <SectionCard ariaLabel={`Édition de l'offre ${offer.name}`}>
        <h2>Modifier l&apos;offre</h2>
        <AdminCatalogOfferForm offer={offer} />
      </SectionCard>

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
