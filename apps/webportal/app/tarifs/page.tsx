import type { Metadata } from "next";

import { ErrorState } from "@/components/ErrorState";
import { PublicStorefrontPage } from "@/components/PublicStorefrontPage";
import { getBillingV2FormulesCatalog, getPublicManagedContent } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";
import { parseStorefrontPageContent } from "@/lib/storefront-content";

export const dynamic = "force-dynamic";

export async function generateMetadata(): Promise<Metadata> {
  const result = await getPublicManagedContent("storefront:tarifs");
  const content = result.data ? parseStorefrontPageContent(result.data.bodyMarkdown) : null;
  return buildPublicMetadata({
    title: content?.seoTitle ?? "Tarifs Zachary IT",
    description: content?.seoDescription ?? "Unités de facturation et prestations sur devis Zachary IT.",
    path: "/tarifs",
  });
}

export default async function TarifsPage() {
  const [result, catalogResult] = await Promise.all([
    getPublicManagedContent("storefront:tarifs"),
    getBillingV2FormulesCatalog(),
  ]);
  const content = result.data ? parseStorefrontPageContent(result.data.bodyMarkdown) : null;
  return content ? <><PublicStorefrontPage content={content} /><BillingPriceProjection catalog={catalogResult.data} /></> : (
    <ErrorState description="Les informations tarifaires sont temporairement indisponibles." reference={result.correlationId} title="Tarifs indisponibles" />
  );
}

function BillingPriceProjection({
  catalog,
}: {
  catalog: Awaited<ReturnType<typeof getBillingV2FormulesCatalog>>["data"];
}) {
  const rows = catalog.services.flatMap((service) => {
    const flat = service.flatMonthlyAmountCents === null
      ? []
      : [{ label: service.name, amountCents: service.flatMonthlyAmountCents }];
    return [
      ...flat,
      ...service.tiers.map((tier) => ({
        label: `${service.name} — ${tier.label}`,
        amountCents: tier.monthlyAmountCents,
      })),
    ];
  });

  if (catalog.source === "unavailable" || rows.length === 0) {
    return null;
  }

  return (
    <section className="service-section billing-price-projection" aria-labelledby="billing-price-title">
      <header className="service-section-heading">
        <h2 id="billing-price-title">Unités et tarifs du catalogue public</h2>
        <p>Montants affichés en lecture seule depuis Billing. Ils n’ouvrent pas un achat libre-service lorsque le service est traité sur devis.</p>
      </header>
      <div className="service-offer-grid">
        {rows.map((row) => (
          <article className="service-offer-card" key={row.label}>
            <h3>{row.label}</h3>
            <p className="billing-price-projection-amount">{formatCents(row.amountCents)} / mois</p>
            <a className="service-inline-link" href="/contact">Demander un devis</a>
          </article>
        ))}
      </div>
    </section>
  );
}

function formatCents(amountCents: number) {
  return new Intl.NumberFormat("fr-FR", { style: "currency", currency: "EUR" }).format(amountCents / 100);
}
