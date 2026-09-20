import type { Metadata } from "next";

import { ErrorState } from "@/components/ErrorState";
import { PublicCommercialTariffCatalog } from "@/components/PublicCommercialTariffCatalog";
import { PublicStorefrontPage } from "@/components/PublicStorefrontPage";
import { getBillingV2FormulesCatalog, getPublicManagedContent } from "@/lib/internal-api";
import { buildPublicCommercialCatalog } from "@/lib/public-commercial-catalog";
import {
  buildPublicMetadata,
  CONTENT_UNAVAILABLE_ROBOTS,
} from "@/lib/public-metadata";
import {
  parseStorefrontPageContent,
  resolveStorefrontBreadcrumb,
} from "@/lib/storefront-content";

export const dynamic = "force-dynamic";

export async function generateMetadata(): Promise<Metadata> {
  const result = await getPublicManagedContent("storefront:tarifs");
  const content = result.data ? parseStorefrontPageContent(result.data.bodyMarkdown) : null;
  return buildPublicMetadata({
    title: content?.seoTitle ?? "Tarifs",
    description: content?.seoDescription ?? "Découvrez les services Zachary IT, leurs tarifs de départ et les prestations proposées sur devis.",
    path: "/tarifs",
    // Sans contenu, le corps rend un `ErrorState` : ne pas laisser cet
    // instantane entrer dans l'index a la place de la page.
    ...(content ? {} : { robots: CONTENT_UNAVAILABLE_ROBOTS }),
  });
}

export default async function TarifsPage() {
  const [contentResult, billingCatalogResult] = await Promise.all([
    getPublicManagedContent("storefront:tarifs"),
    getBillingV2FormulesCatalog(),
  ]);
  const content = contentResult.data
    ? parseStorefrontPageContent(contentResult.data.bodyMarkdown)
    : null;

  if (!content) {
    return (
      <ErrorState
        description="Les informations tarifaires sont temporairement indisponibles."
        reference={contentResult.correlationId}
        title="Tarifs indisponibles"
      />
    );
  }

  return (
    <>
      <PublicStorefrontPage
        breadcrumbItems={resolveStorefrontBreadcrumb("/tarifs")!}
        beforeSections={(
          <PublicCommercialTariffCatalog
            catalog={buildPublicCommercialCatalog(billingCatalogResult.data)}
            currency={billingCatalogResult.data.currency}
          />
        )}
        compactHero
        content={content}
        heroLead="Consultez les prix des services, leurs options et leur mode de souscription."
        heroTitle="Tarifs des services Zachary IT"
        showHeroActions={false}
      />
    </>
  );
}
