import Link from "next/link";

import {
  buildPackSheetContentKey,
  type ResolvedPublicPackManifest,
} from "@kermaria/shared";

import { AdminPublicPackCatalogForm } from "@/components/AdminPublicPackCatalogForm";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { requireAdminSession } from "@/lib/auth";
import {
  formatCommitmentMonths,
  formatCurrencyFromCents,
  formatPaymentModeLabel,
} from "@/lib/formatters";
import {
  getAdminCatalog,
  getAdminPublicPackCatalogContent,
} from "@/lib/internal-api";
import { resolvePackCatalog } from "@/lib/public-packs";

export const metadata = {
  title: "Vitrine packs - Administration",
};

export const dynamic = "force-dynamic";

function buildVariantRows(pack: ResolvedPublicPackManifest) {
  return [
    {
      label: "1 mois - mensuel",
      variant: pack.variantsByCommitment[1].monthly,
    },
    {
      label: "6 mois - mensuel",
      variant: pack.variantsByCommitment[6].monthly,
    },
    {
      label: "6 mois - comptant",
      variant: pack.variantsByCommitment[6].upfront,
    },
    {
      label: "12 mois - mensuel",
      variant: pack.variantsByCommitment[12].monthly,
    },
    {
      label: "12 mois - comptant",
      variant: pack.variantsByCommitment[12].upfront,
    },
  ].filter(
    (
      entry,
    ): entry is {
      label: string;
      variant: NonNullable<typeof entry.variant>;
    } => entry.variant !== null,
  );
}

export default async function AdminPublicPackCatalogPage() {
  await requireAdminSession();

  const [contentResult, catalogResult] = await Promise.all([
    getAdminPublicPackCatalogContent(),
    getAdminCatalog(),
  ]);

  const publicPacks = catalogResult.error
    ? []
    : resolvePackCatalog(catalogResult.data, contentResult.data);

  return (
    <>
      <PageHeader
        description="Pilotez la presentation publique des packs sans modifier le code ni toucher au socle de facturation."
        eyebrow="Administration interne"
        title="Vitrine packs"
      />

      <section className="content-panel page-header-split">
        <div>
          <span className="card-kicker">Pilotage back-office</span>
          <h2>Tout gerer sans retoucher le code</h2>
          <p>
            Cette page centralise la vitrine publique. Les textes, badges et
            lignes du comparatif se modifient ici. Les prix, frais de mise en
            service et identifiants de paiement se reglent dans les fiches du
            catalogue commercial.
          </p>
        </div>
        <div className="stack-row">
          <Link className="button button-secondary" href="/offres">
            Voir la page /offres
          </Link>
          <Link className="button button-secondary" href="/admin/catalog">
            Ouvrir le catalogue
          </Link>
        </div>
      </section>

      <p>
        <Link className="text-link" href="/admin/catalog">
          ← Retour au catalogue commercial
        </Link>
      </p>

      {publicPacks.length > 0 ? (
        <SectionCard ariaLabel="Variantes facturables des packs publics">
          <h2>Tarification et variantes facturables</h2>
          <p className="field-hint">
            Chaque ligne ci-dessous renvoie vers la fiche facturable utilisee
            par le site et par le provisionnement. Vous pouvez y ajuster les
            montants ou les references PSP depuis le back-office.
          </p>

          <div className="public-pack-admin-grid">
            {publicPacks.map((pack) => (
              <section className="public-pack-admin-card" key={pack.key}>
                <div>
                  <h3>{pack.label}</h3>
                  <p className="field-hint">{pack.audience}</p>
                  <p>
                    <Link
                      className="text-link"
                      href={`/admin/content/${encodeURIComponent(buildPackSheetContentKey(pack.key))}`}
                    >
                      Modifier la fiche technique
                    </Link>
                  </p>
                </div>

                <div className="public-pack-admin-table-wrap">
                  <table className="public-pack-admin-table">
                    <thead>
                      <tr>
                        <th>Variante</th>
                        <th>Prix HT</th>
                        <th>Mise en service</th>
                        <th>Reference</th>
                        <th>Action</th>
                      </tr>
                    </thead>
                    <tbody>
                      {buildVariantRows(pack).map(({ label, variant }) => (
                        <tr key={variant.offer.id}>
                          <td>
                            <strong>{label}</strong>
                            <div className="cell-secondary">
                              {formatCommitmentMonths(variant.commitmentMonths)}
                              {" - "}
                              {formatPaymentModeLabel(variant.paymentMode)}
                            </div>
                          </td>
                          <td>
                            <strong>
                              {variant.paymentMode === "upfront"
                                ? formatCurrencyFromCents(
                                    variant.billingPriceAmountCents,
                                  )
                                : formatCurrencyFromCents(
                                    variant.monthlyPriceAmountCents,
                                  )}
                            </strong>
                            <div className="cell-secondary">
                              {variant.paymentMode === "upfront"
                                ? `${formatCurrencyFromCents(variant.monthlyPriceAmountCents)} / mois equivalent`
                                : `${formatCurrencyFromCents(variant.billingPriceAmountCents)} par echeance`}
                            </div>
                          </td>
                          <td>
                            {formatCurrencyFromCents(variant.setupFeeAmountCents)}
                          </td>
                          <td>
                            <code>{variant.externalReference}</code>
                          </td>
                          <td>
                            <Link
                              className="table-action"
                              href={`/admin/catalog/${encodeURIComponent(variant.offer.id)}`}
                            >
                              Modifier
                            </Link>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </section>
            ))}
          </div>
        </SectionCard>
      ) : null}

      {contentResult.error ? (
        <ErrorState
          description="Impossible de charger la configuration publique des packs pour le moment."
          reference={contentResult.correlationId}
          title="Vitrine indisponible"
        />
      ) : (
        <SectionCard ariaLabel="Configuration de la vitrine packs">
          <h2>Modifier la vitrine publique</h2>
          <p className="field-hint">
            Cette zone pilote uniquement la presentation client et le tableau
            comparatif visible sur le site public. Les variantes facturees
            restent gerees dans le catalogue commercial juste au-dessus.
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
