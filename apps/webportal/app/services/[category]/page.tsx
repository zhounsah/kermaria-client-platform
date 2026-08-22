import type { Metadata } from "next";
import { notFound } from "next/navigation";
import type { ManagedContentKey } from "@kermaria/shared";
import { ErrorState } from "@/components/ErrorState";
import { PublicStorefrontPage } from "@/components/PublicStorefrontPage";
import { buildPublicMetadata } from "@/lib/public-metadata";
import { getBillingV2FormulesCatalog, getPublicManagedContent } from "@/lib/internal-api";
import {
  parseStorefrontPageContent,
  resolveStorefrontBreadcrumb,
  storefrontContentKeyForServiceSlug,
  storefrontServiceSelfServiceOrderable,
  STOREFRONT_SERVICE_SLUGS,
  type StorefrontServiceSlug,
} from "@/lib/storefront-content";
type CategoryPageProps = {
  params: Promise<{ category: string }>;
};
export const dynamic = "force-dynamic";
export async function generateMetadata({ params }: CategoryPageProps): Promise<Metadata> {
  const { category: slug } = await params;
  const key = resolveStorefrontKey(slug);
  if (!key) {
    return {};
  }
  const result = await getPublicManagedContent(key);
  const content = result.data
    ? parseStorefrontPageContent(result.data.bodyMarkdown)
    : null;
  return buildPublicMetadata({
    title: content?.seoTitle ?? "Services Zachary IT",
    description: content?.seoDescription ?? "Services IT gérés, sur devis ou accompagnés par Zachary IT.",
    path: `/services/${slug}`,
  });
}
export default async function ServiceCategoryRoute({ params }: CategoryPageProps) {
  const { category: slug } = await params;
  const key = resolveStorefrontKey(slug);
  if (!key) {
    notFound();
  }
  const serviceSlug = resolveServiceSlug(slug);
  const [result, catalogResult] = await Promise.all([
    getPublicManagedContent(key),
    serviceSlug ? getBillingV2FormulesCatalog() : Promise.resolve(null),
  ]);
  const content = result.data
    ? parseStorefrontPageContent(result.data.bodyMarkdown)
    : null;
  const selfServiceOrderable = serviceSlug
    ? storefrontServiceSelfServiceOrderable(
      serviceSlug,
      catalogResult?.data ?? { source: "unavailable", currency: "EUR", presets: [], services: [], commitments: [], checkoutRoutes: [] },
    )
    : null;
  return content ? (
    <PublicStorefrontPage
      breadcrumbItems={resolveStorefrontBreadcrumb(`/services/${slug}`)!}
      content={content}
      selfServiceOrderable={selfServiceOrderable}
    />
  ) : (
    <ErrorState
      description="Cette page de service est temporairement indisponible."
      reference={result.correlationId}
      title="Service indisponible"
    />
  );
}
function resolveStorefrontKey(slug: string): ManagedContentKey | null {
  if (["cloud-hebergement", "domaines-messagerie", "reseau-securite", "support-it"].includes(slug)) {
    return `storefront:${slug}` as ManagedContentKey;
  }
  const serviceSlug = resolveServiceSlug(slug);
  return serviceSlug ? storefrontContentKeyForServiceSlug(serviceSlug) : null;
}
function resolveServiceSlug(slug: string): StorefrontServiceSlug | null {
  return STOREFRONT_SERVICE_SLUGS.includes(slug as StorefrontServiceSlug)
    ? slug as StorefrontServiceSlug
    : null;
}
