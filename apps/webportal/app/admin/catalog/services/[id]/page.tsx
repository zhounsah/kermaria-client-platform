import { notFound } from "next/navigation";
import { ServiceCatalogEditor } from "@/components/admin/catalog/ServiceCatalogEditor";
import { ErrorState } from "@/components/ErrorState";
import { requireAdminSession } from "@/lib/auth";
import { getAdminBillingV2Catalog } from "@/lib/internal-api";
export const dynamic = "force-dynamic";
type Props = { params: Promise<{ id: string }>; searchParams: Promise<{ tab?: string }> };
export default async function Page({ params, searchParams }: Props) {
  await requireAdminSession(); const [{ id }, { tab: requested }, result] = await Promise.all([params, searchParams, getAdminBillingV2Catalog()]);
  if (result.error) return <ErrorState description="Impossible de charger le catalogue Billing V2." reference={result.correlationId} title="Service indisponible" />;
  const service = result.data.services.find((item) => item.id === id); if (!service) notFound();
  const tab = requested === "tiers" || requested === "pricing" || requested === "commercialization" ? requested : "essential";
  return <ServiceCatalogEditor asOf={new Date().toISOString()} service={service} tab={tab} />;
}
