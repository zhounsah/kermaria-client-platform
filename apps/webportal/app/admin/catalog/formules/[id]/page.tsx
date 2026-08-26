import { notFound } from "next/navigation";
import { FormulaCatalogEditor } from "@/components/admin/catalog/FormulaCatalogEditor";
import { ErrorState } from "@/components/ErrorState";
import { requireAdminSession } from "@/lib/auth";
import { getAdminBillingV2Catalog, getBillingV2FormulesCatalog } from "@/lib/internal-api";
export const dynamic = "force-dynamic";
type Props = { params: Promise<{ id: string }>; searchParams: Promise<{ tab?: string }> };
export default async function Page({ params, searchParams }: Props) { await requireAdminSession(); const [{ id }, { tab: requested }, adminResult, publicResult] = await Promise.all([params, searchParams, getAdminBillingV2Catalog(), getBillingV2FormulesCatalog()]); if (adminResult.error) return <ErrorState description="Impossible de charger le catalogue Billing V2." reference={adminResult.correlationId} title="Formule indisponible" />; const preset = adminResult.data.presets.find((item) => item.id === id); if (!preset) notFound(); const publicPreset = publicResult.data.presets.find((item) => item.code === preset.code); const tab = requested === "composition" || requested === "preview" ? requested : "essential"; return <FormulaCatalogEditor baselineMonthlyAmountCents={publicPreset?.baselineMonthlyAmountCents ?? null} preset={preset} services={adminResult.data.services} tab={tab} />; }
