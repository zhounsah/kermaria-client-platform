import { CatalogIntegrations } from "@/components/admin/catalog/CatalogIntegrations";
import { ErrorState } from "@/components/ErrorState";
import { requireAdminSession } from "@/lib/auth";
import { getAdminBillingV2Catalog, getAdminBillingV2CatalogProviders, getAdminBillingV2Readiness } from "@/lib/internal-api";
export const dynamic = "force-dynamic";
export default async function Page() { await requireAdminSession(); const [catalog, coverage, readiness] = await Promise.all([getAdminBillingV2Catalog(), getAdminBillingV2CatalogProviders(), getAdminBillingV2Readiness()]); if (catalog.error) return <ErrorState description="Impossible de charger les intégrations du catalogue." reference={catalog.correlationId} title="Intégrations indisponibles" />; return <CatalogIntegrations asOf={new Date().toISOString()} coverage={coverage.data} readiness={readiness.data?.providers ?? []} snapshot={catalog.data} />; }
