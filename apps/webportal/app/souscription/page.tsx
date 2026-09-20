import type { Metadata } from "next";

import { BillingV2CartCheckoutReview } from "@/components/BillingV2CartCheckoutReview";

export const metadata: Metadata = { title: "Vérifier votre souscription" };

export default function SouscriptionPage() {
  return <BillingV2CartCheckoutReview />;
}
