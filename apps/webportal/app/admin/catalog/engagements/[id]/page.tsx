import { notFound } from "next/navigation";
import { CommitmentCatalogEditor } from "@/components/admin/catalog/CommitmentCatalogEditor";
import { ErrorState } from "@/components/ErrorState";
import { requireAdminSession } from "@/lib/auth";
import { getAdminBillingV2Catalog } from "@/lib/internal-api";
export const dynamic = "force-dynamic";
type Props = { params: Promise<{ id: string }>; searchParams: Promise<{ tab?: string }> };
export default async function Page({ params, searchParams }: Props) { await requireAdminSession(); const [{ id }, { tab: requested }, result] = await Promise.all([params, searchParams, getAdminBillingV2Catalog()]); if (result.error) return <ErrorState description="Impossible de charger le catalogue Billing V2." reference={result.correlationId} title="Engagement indisponible" />; const commitment = result.data.commitments.find((item) => item.id === id); if (!commitment) notFound(); return <CommitmentCatalogEditor commitment={commitment} tab={requested === "payments" ? "payments" : "essential"} />; }
