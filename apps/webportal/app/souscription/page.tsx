import type { Metadata } from "next";

import { BillingV2CartCheckoutReview } from "@/components/BillingV2CartCheckoutReview";
import { getPublicSitePageLayout } from "@/lib/internal-api";

export const metadata: Metadata = { title: "Vérifier votre souscription" };
export const dynamic = "force-dynamic";

export default async function SouscriptionPage() {
  const layout = await getPublicSitePageLayout("/souscription");
  return <BillingV2CartCheckoutReview initialLayout={layout.data} />;
}
