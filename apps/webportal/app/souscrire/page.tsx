import { SubscribeCatalogSections } from "@/components/SubscribeCatalogSections";
import { requireClientSession } from "@/lib/auth";
import {
  getCheckoutSummary,
  getCommercialCatalog,
  getPublicCommercialCatalog,
  getPublicPackCatalogContent,
  getServiceCatalog,
  resolveDataSource,
} from "@/lib/internal-api";
import { resolvePackCatalog } from "@/lib/public-packs";

export const metadata = {
  title: "Souscrire",
};

export const dynamic = "force-dynamic";

export default async function SubscribePage() {
  await requireClientSession();
  const [
    catalogResult,
    packContentResult,
    serviceCatalogResult,
    commercialCatalogResult,
    checkoutResult,
  ] = await Promise.all([
    getPublicCommercialCatalog(),
    getPublicPackCatalogContent(),
    getServiceCatalog(),
    getCommercialCatalog(),
    getCheckoutSummary(),
  ]);
  const source = resolveDataSource([
    catalogResult.source,
    packContentResult.source,
    serviceCatalogResult.source,
    commercialCatalogResult.source,
    checkoutResult.source,
  ]);
  const packs = resolvePackCatalog(catalogResult.data, packContentResult.data);

  const aLaCarteOffers = commercialCatalogResult.data
    .filter(
      (offer) =>
        offer.status === "active"
        && offer.billingCadence === "one_time"
        && offer.publicPackCode === null
        && offer.priceAmountCents > 0,
    )
    .sort((a, b) => a.displayOrder - b.displayOrder);
  const checkout = checkoutResult.data;

  return (
    <SubscribeCatalogSections
      aLaCarteOffers={aLaCarteOffers}
      catalogCorrelationId={catalogResult.correlationId}
      catalogError={Boolean(catalogResult.error)}
      checkout={checkout}
      commercialCatalogCorrelationId={commercialCatalogResult.correlationId}
      commercialCatalogError={Boolean(commercialCatalogResult.error)}
      packContent={packContentResult.data}
      packs={packs}
      serviceCatalog={serviceCatalogResult.data}
      serviceCatalogCorrelationId={serviceCatalogResult.correlationId}
      serviceCatalogError={Boolean(serviceCatalogResult.error)}
      source={source}
    />
  );
}
