import Link from "next/link";

import { ErrorState } from "@/components/ErrorState";
import { InvoiceTable } from "@/components/InvoiceTable";
import { MetricCard } from "@/components/MetricCard";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { requireClientSession } from "@/lib/auth";
import { formatCurrency } from "@/lib/formatters";
import { getInvoices } from "@/lib/internal-api";

export const metadata = {
  title: "Factures",
};

export const dynamic = "force-dynamic";

export default async function InvoicesPage() {
  await requireClientSession();
  const result = await getInvoices();
  const pendingInvoices = result.data.filter(
    (invoice) => invoice.status === "pending",
  );
  const pendingTotal = pendingInvoices.reduce(
    (total, invoice) => total + invoice.totalAmount,
    0,
  );

  return (
    <>
      <PageHeader
        description="La facturation affichée dans cet espace reste informative tant que le module de facturation réel n’est pas activé."
        eyebrow="Informations de facturation"
        title="Mes documents"
      />

      {result.error ? (
        <ErrorState
          action={
            <Link className="button" href="/invoices">
              Réessayer
            </Link>
          }
          description="Impossible de charger les informations de facturation pour le moment."
          reference={result.correlationId}
          title="Informations indisponibles"
        />
      ) : (
        <>
          <section
            aria-label="Synthèse des informations de facturation"
            className="metrics-grid metrics-grid-three"
          >
            <MetricCard
              detail="Sur la période affichée"
              label="Documents disponibles"
              tone="slate"
              value={String(result.data.length)}
            />
            <MetricCard
              detail={
                pendingInvoices.length
                  ? "Montant informatif"
                  : "Aucun montant en attente"
              }
              label="Montant en attente"
              tone="amber"
              value={formatCurrency(pendingTotal)}
            />
            <MetricCard
              detail="Aucun paiement disponible dans le portail"
              label="Situation affichée"
              tone="green"
              value={pendingInvoices.length ? "À vérifier" : "À jour"}
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
