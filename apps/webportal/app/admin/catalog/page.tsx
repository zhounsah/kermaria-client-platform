import Link from "next/link";

import { AdminBillingV2Catalog } from "@/components/AdminBillingV2Catalog";
import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { requireAdminSession } from "@/lib/auth";
import {
  getAdminBillingV2Catalog,
  getAdminBillingV2CatalogProviders,
} from "@/lib/internal-api";

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
export default async function AdminCatalogPage() {
  await requireAdminSession();
  const [catalogResult, providersResult] = await Promise.all([
    getAdminBillingV2Catalog(),
    getAdminBillingV2CatalogProviders(),
  ]);

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
        title="Catalogue Billing V2"
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
        <AdminBillingV2Catalog
          providers={providersResult.data}
          snapshot={catalogResult.data}
        />
      )}
    </>
  );
}
