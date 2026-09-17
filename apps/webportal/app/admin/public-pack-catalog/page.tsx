import Link from "next/link";

import { buildPackSheetContentKey } from "@kermaria/shared";

import { AdminPublicPackCatalogForm } from "@/components/AdminPublicPackCatalogForm";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { requireAdminSession } from "@/lib/auth";
import { formatCurrencyFromCents } from "@/lib/formatters";
import {
  getAdminPublicPackCatalogContent,
  getBillingV2FormulesCatalog,
} from "@/lib/internal-api";
import { buildPublicPackViews } from "@/lib/public-packs";

export const metadata = {
  title: "Présentation des offres - Administration",
};

export const dynamic = "force-dynamic";

/**
 * Présentation des offres : contenu public seulement.
 *
 * Cet écran pilote ce que le visiteur lit — libellés, badges, lignes du
 * comparatif. Il ne pilote aucun tarif : services, paliers, versions de prix,
 * formules et engagements se règlent dans `/admin/catalog`, seule autorité
 * commerciale. Rendre les montants modifiables des deux côtés recréerait
 * exactement le second catalogue que Billing V2 remplace.
 */
export default async function AdminPublicPackCatalogPage() {
  await requireAdminSession();

  const [contentResult, catalogResult] = await Promise.all([
    getAdminPublicPackCatalogContent(),
    getBillingV2FormulesCatalog(),
  ]);

  const publicPacks = catalogResult.error
    ? []
    : buildPublicPackViews(catalogResult.data, contentResult.data);

  return (
    <>
      <PageHeader
        description="Pilotez la présentation publique des offres sans modifier le code ni toucher au socle de facturation."
        eyebrow="Administration interne"
        title="Présentation publique des offres"
      />

      <section className="content-panel page-header-split">
        <div>
          <span className="card-kicker">Pilotage back-office</span>
          <h2>Présentation publique et modèle technique séparés</h2>
          <p>
            Cette page pilote ce que le client lit : textes, badges et lignes du
            comparatif. Une offre publique s&apos;appuie sur un preset Billing V2 ;
            les tarifs, versions de prix, services, paliers et rattachements de
            provider se règlent exclusivement dans le catalogue Billing V2.
          </p>
        </div>
        <div className="stack-row">
          <Link className="button button-secondary" href="/offres">
            Voir la page /offres
          </Link>
          <Link className="button button-secondary" href="/admin/catalog">
            Ouvrir le catalogue Billing V2
          </Link>
        </div>
      </section>

      {publicPacks.length > 0 ? (
        <SectionCard ariaLabel="Offres publiques">
          <h2>Offres publiques</h2>
          <p className="field-hint">
            Chaque offre publique apparaît ici lorsqu&apos;elle est reliée à un preset
            Billing V2. Le montant indiqué est le point de départ mensuel calculé
            par le serveur pour la configuration recommandée ; il n&apos;est pas
            modifiable depuis cet écran.
          </p>

          <div className="public-pack-admin-table-wrap">
            <table className="public-pack-admin-table">
              <thead>
                <tr>
                  <th>Offre publique</th>
                  <th>Preset Billing V2</th>
                  <th>À partir de</th>
                  <th>Contenu de l&apos;offre</th>
                  <th>Tarifs</th>
                </tr>
              </thead>
              <tbody>
                {publicPacks.map((pack) => (
                  <tr key={pack.key}>
                    <td>
                      <strong>{pack.label}</strong>
                      <div className="cell-secondary">{pack.audience}</div>
                    </td>
                    <td>
                      <code>{pack.presetCode}</code>
                    </td>
                    <td>
                      {formatCurrencyFromCents(pack.baselineMonthlyAmountCents)}
                      {" / mois"}
                    </td>
                    <td>
                      <Link
                        aria-label={`Modifier le contenu de l’offre ${pack.label}`}
                        className="table-action"
                        href={`/admin/content/${encodeURIComponent(buildPackSheetContentKey(pack.key))}`}
                      >
                        Modifier
                      </Link>
                    </td>
                    <td>
                      <Link className="table-action" href="/admin/catalog">
                        Ouvrir le catalogue
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </SectionCard>
      ) : (
        <ErrorState
          compact
          description="Aucune offre publique : soit le catalogue Billing V2 est injoignable, soit aucun preset Billing V2 ne correspond à une offre de la vitrine."
          reference={catalogResult.correlationId}
          title="Aucune offre publique"
        />
      )}

      {contentResult.error ? (
        <ErrorState
          description="Impossible de charger la présentation publique des offres pour le moment."
          reference={contentResult.correlationId}
          title="Vitrine indisponible"
        />
      ) : (
        <SectionCard ariaLabel="Configuration de la présentation publique des offres">
          <h2>Modifier la présentation publique</h2>
          <p className="field-hint">
            Cette zone pilote uniquement la présentation client et le tableau
            comparatif visible sur le site public.
          </p>
          <AdminPublicPackCatalogForm initialContent={contentResult.data} />
        </SectionCard>
      )}

      <MockNotice
        correlationId={contentResult.correlationId}
        source={contentResult.source}
      />
    </>
  );
}
