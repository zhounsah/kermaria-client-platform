import { redirect } from "next/navigation";

import { requireClientSession } from "@/lib/auth";
import { billingV2SelectionToSearchParams } from "@/lib/billing-v2-selection";
import { getPendingBillingV2Selection } from "@/lib/internal-api";

export const dynamic = "force-dynamic";

export default async function ResumeBillingV2FormulePage() {
  await requireClientSession("/formules/reprendre");
  const result = await getPendingBillingV2Selection();

  if (result.error || !result.data) {
    redirect("/formules");
  }

  const selection = result.data.selection;
  const query = billingV2SelectionToSearchParams(selection).toString();
  redirect(
    selection.presetCode
      ? `/formules/${encodeURIComponent(selection.presetCode)}?${query}`
      : `/souscrire?${query}`,
  );
}
