import type { Metadata } from "next";

import { BillingV2CartPage } from "@/components/BillingV2CartPage";
import { getBillingV2FormulesCatalog } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";

export const dynamic = "force-dynamic";

export const metadata: Metadata = buildPublicMetadata({
  title: "Votre panier",
  description: "Consultez et ajustez les services sélectionnés avant votre souscription.",
  path: "/panier",
  robots: { index: false, follow: false },
});

export default async function PanierPage() {
  const result = await getBillingV2FormulesCatalog();
  return <BillingV2CartPage catalog={result.data} />;
}
