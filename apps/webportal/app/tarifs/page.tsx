import type { Metadata } from "next";
import Link from "next/link";

import { ErrorState } from "@/components/ErrorState";
import { PublicStorefrontPage } from "@/components/PublicStorefrontPage";
import { describeTierAttributes } from "@/lib/billing-v2-formules";
import { getBillingV2FormulesCatalog, getPublicManagedContent } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";
import {
  parseStorefrontPageContent,
  resolveStorefrontBreadcrumb,
  resolveStorefrontTariffAction,
} from "@/lib/storefront-content";

type TariffRow = {
  serviceCode: string;
  tierCode: string | null;
  label: string;
  amountCents: number | null;
  tierAttributeDescription: string[] | null;
  vpsOverview: boolean;
};

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
  return content ? <><PublicStorefrontPage breadcrumbItems={resolveStorefrontBreadcrumb("/tarifs")!} content={content} /><BillingPriceProjection catalog={catalogResult.data} /></> : (
    <ErrorState description="Les informations tarifaires sont temporairement indisponibles." reference={result.correlationId} title="Tarifs indisponibles" />
  );
}

function BillingPriceProjection({
  catalog,
}: {
  catalog: Awaited<ReturnType<typeof getBillingV2FormulesCatalog>>["data"];
}) {
  const rows: TariffRow[] = catalog.services.flatMap((service): TariffRow[] => {
    if (service.code === "VPS-LOCAL" || service.code === "VPS-CLOUD") {
      return [{
        serviceCode: service.code,
        tierCode: null,
        label: service.name,
        amountCents: null,
        tierAttributeDescription: null,
        vpsOverview: true,
      }];
    }
    const flat = service.flatMonthlyAmountCents === null
      ? []
      : [{
          serviceCode: service.code,
          tierCode: null,
          label: service.name,
          amountCents: service.flatMonthlyAmountCents,
          tierAttributeDescription: null,
          vpsOverview: false,
        }];
    return [
      ...flat,
      ...service.tiers.map((tier) => ({
        serviceCode: service.code,
        tierCode: tier.code,
        label: `${service.name} — ${tier.label}`,
        amountCents: tier.monthlyAmountCents,
        tierAttributeDescription: describeTierAttributes(tier),
        vpsOverview: false,
      })),
    ];
  });

  if (catalog.source === "unavailable" || rows.length === 0) {
    return null;
  }

  return (
    <section className="service-section billing-price-projection" aria-labelledby="billing-price-title">
      <header className="service-section-heading">
        <h2 id="billing-price-title">Unités et tarifs des services</h2>
        <p>Les montants sont affichés à titre d’information. Un service reste disponible à la commande en ligne uniquement lorsque le parcours le précise.</p>
      </header>
      <div className="service-offer-grid">
        {rows.map((row) => {
          const action = row.vpsOverview
            ? { label: "Voir les offres VPS", href: "/services/vps" }
            : resolveStorefrontTariffAction(row.serviceCode, catalog);
          return (
            <article
              className="service-offer-card"
              key={`${row.serviceCode}-${row.tierCode ?? "flat"}`}
            >
              <h3>{row.label}</h3>
              {row.tierAttributeDescription && row.tierAttributeDescription.length > 0 ? (
                <p className="billing-price-projection-specifications">
                  {row.tierAttributeDescription.join(" · ")}
                </p>
              ) : null}
              {row.vpsOverview ? <p>Comparez les paliers et leurs caractéristiques sur la page VPS.</p> : <p className="billing-price-projection-amount">{formatCents(row.amountCents!)} / mois</p>}
              <Link className="service-inline-link" href={action.href}>{action.label}</Link>
            </article>
          );
        })}
      </div>
    </section>
  );
}

function formatCents(amountCents: number) {
  return new Intl.NumberFormat("fr-FR", { style: "currency", currency: "EUR" }).format(amountCents / 100);
}
