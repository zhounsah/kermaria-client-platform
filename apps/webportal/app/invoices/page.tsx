import Link from "next/link";

import { ErrorState } from "@/components/ErrorState";
import { InvoiceTable } from "@/components/InvoiceTable";
import { MetricCard } from "@/components/MetricCard";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { requireClientSession } from "@/lib/auth";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { getCommercialDocuments } from "@/lib/internal-api";

export const metadata = {
  title: "Documents commerciaux",
};

export const dynamic = "force-dynamic";

export default async function InvoicesPage() {
  await requireClientSession();
  const result = await getCommercialDocuments();
  const sharedCount = result.data.length;
  const cancelledCount = result.data.filter(
    (document) => document.status === "cancelled",
  ).length;
  const totalAmount = result.data.reduce(
    (total, document) => total + document.totalAmountCents,
    0,
  );

  return (
    <>
      <PageHeader
        description="Vos documents commerciaux et factures émises."
        eyebrow="Facturation"
        title="Mes documents et factures"
      />

      {result.error ? (
        <ErrorState
          action={
            <Link className="button" href="/invoices">
              Réessayer
            </Link>
          }
          description="Impossible de charger les documents commerciaux informatifs pour le moment."
          reference={result.correlationId}
          title="Documents indisponibles"
        />
      ) : (
        <>
          <section
            aria-label="Synthèse des documents commerciaux"
            className="metrics-grid metrics-grid-three"
          >
            <MetricCard
              detail="Documents partagés dans le portail"
              label="Documents disponibles"
              tone="slate"
              value={String(sharedCount)}
            />
            <MetricCard
              detail="Montant total purement informatif"
              label="Total affiché"
              tone="amber"
              value={formatCurrencyFromCents(totalAmount)}
            />
            <MetricCard
              detail="Annulés mais conservés au suivi"
              label="Documents annulés"
              tone="green"
              value={String(cancelledCount)}
            />
          </section>

          <InvoiceTable invoices={result.data} />
        </>
      )}

      {result.source !== "unavailable" ? (
        <MockNotice
          correlationId={result.correlationId}
          source={result.source}
        />
      ) : null}
    </>
  );
}
