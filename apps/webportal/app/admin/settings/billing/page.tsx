import { AdminBillingConfigurationCenter } from "@/components/AdminBillingConfigurationCenter";
import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { AdminSettingsNavigation } from "@/components/AdminSettingsNavigation";
import { requireAdminSession } from "@/lib/auth";
import {
  getAdminBillingV2Configuration,
  getAdminFiscalPolicy,
} from "@/lib/internal-api";

export const dynamic = "force-dynamic";
export const metadata = {
  title: "Facturation et fiscalité - Administration",
};

export default async function AdminBillingSettingsPage() {
  await requireAdminSession();
  const [fiscalResult, billingResult] = await Promise.all([
    getAdminFiscalPolicy(),
    getAdminBillingV2Configuration(),
  ]);
  return (
    <>
      <PageHeader
        eyebrow="Centre de configuration"
        title="Facturation et fiscalité"
        description="Mentions fiscales datées, résumé Billing V2 et état des drapeaux. Le calcul des montants et des taxes reste l'autorité d'API-INTERNAL."
      />
      <AdminSettingsNavigation />
      {fiscalResult.error ? (
        <ErrorState
          title="Fiscalité indisponible"
          description="Les mentions fiscales ne peuvent pas être chargées pour le moment."
          reference={fiscalResult.correlationId}
        />
      ) : (
        <AdminBillingConfigurationCenter
          billingV2={billingResult.error ? null : billingResult.data}
          initialFiscalPolicy={fiscalResult.data}
        />
      )}
    </>
  );
}
