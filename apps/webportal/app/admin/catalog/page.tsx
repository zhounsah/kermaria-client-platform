import Link from "next/link";

import { CatalogHome } from "@/components/admin/catalog/CatalogHome";
import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { requireAdminSession } from "@/lib/auth";
import { getAdminBillingV2Catalog, getBillingV2FormulesCatalog } from "@/lib/internal-api";

export const metadata = {
  title: "Catalogue commercial - Administration",
};

export const dynamic = "force-dynamic";

/**
 * Administration commerciale.
 *
 * Cet écran porte la <b>conception</b> : services, paliers, versions de prix,
 * formules, engagements et rattachements provider. L'exploitation courante —
 * readiness, abonnements, paiements, provisioning, réconciliation — reste sur
 * `/admin/billing-v2`. Deux métiers, deux écrans : les fusionner rendrait les
 * deux illisibles, les dupliquer recréerait deux catalogues.
 */
type PageProps = { searchParams: Promise<{ section?: string }> };

export default async function AdminCatalogPage({ searchParams }: PageProps) {
  await requireAdminSession();
  const [{ section: requestedSection }, catalogResult, publicCatalog] = await Promise.all([
    searchParams,
    getAdminBillingV2Catalog(),
    getBillingV2FormulesCatalog(),
  ]);
  const section = requestedSection === "formules" || requestedSection === "engagements" ? requestedSection : "services";
  const baselineByCode = Object.fromEntries(publicCatalog.data.presets.map((preset) => [preset.code, preset.baselineMonthlyAmountCents]));

  return (
    <>
      <PageHeader
        action={
          <Link className="button button-secondary" href="/admin/billing-v2">
            Exploitation Billing V2
          </Link>
        }
        description="Seule autorité commerciale du produit. Un tarif ne se modifie pas : il se remplace par une nouvelle version, l'ancienne restant l'autorité des factures qu'elle a produites."
        eyebrow="Administration interne"
        title="Catalogue commercial"
      />

      <p>
        <Link className="button button-secondary" href="/admin/public-pack-catalog">
          Gérer la vitrine des formules
        </Link>
      </p>

      {catalogResult.error ? (
        <ErrorState
          description="Impossible de charger le catalogue Billing V2 pour le moment."
          reference={catalogResult.correlationId}
          title="Catalogue indisponible"
        />
      ) : (
        catalogResult.data.editable ? <CatalogHome asOf={new Date().toISOString()} baselineByCode={baselineByCode} section={section} snapshot={catalogResult.data} /> : <ErrorState description="La persistance catalogue n’est pas disponible sur cet environnement. Aucune modification n’est possible." reference={catalogResult.correlationId} title="Catalogue non administrable" />
      )}
    </>
  );
}
