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
        description="Documents strictement fictifs. Aucune facturation réelle ni aucun paiement ne sont connectés."
        eyebrow="Démonstration de facturation"
        title="Mes factures"
      />

      <section className="metrics-grid metrics-grid-three">
        <MetricCard
          detail="Sur la période affichée"
          label="Documents fictifs"
          tone="slate"
          value={String(result.data.length)}
        />
        <MetricCard
          detail={
            pendingInvoices.length
              ? "Montant de démonstration"
              : "Aucun montant en attente"
          }
          label="Montant fictif en attente"
          tone="amber"
          value={formatCurrency(pendingTotal)}
        />
        <MetricCard
          detail="Aucun moyen de paiement disponible"
          label="Situation mock"
          tone="green"
          value={pendingInvoices.length ? "À vérifier" : "À jour"}
        />
      </section>

      <InvoiceTable invoices={result.data} />

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
