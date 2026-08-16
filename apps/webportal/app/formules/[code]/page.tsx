import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";

import { BillingV2FormuleConfigurator } from "@/components/BillingV2FormuleConfigurator";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { getBillingV2FormulesCatalog } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";
import { resolvePresetTagline } from "@/lib/billing-v2-formules";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ code: string }>;
};

export async function generateMetadata({
  params,
}: PageProps): Promise<Metadata> {
  const { code } = await params;
  const { data: catalog } = await getBillingV2FormulesCatalog();
  const preset = catalog.presets.find((item) => item.code === code);

  return buildPublicMetadata({
    title: preset ? `Configurer la formule ${preset.name}` : "Configurer une formule",
    description:
      preset?.description
      ?? "Ajustez la capacité, la sauvegarde et les accès de votre formule.",
    path: `/formules/${code}`,
  });
}

export default async function FormuleConfigurationPage({ params }: PageProps) {
  const { code } = await params;
  const { data: catalog } = await getBillingV2FormulesCatalog();
  const preset = catalog.presets.find((item) => item.code === code);

  if (catalog.presets.length > 0 && !preset) {
    notFound();
  }

  return (
    <div className="formule-page">
      <nav className="formule-breadcrumb" aria-label="Fil d'Ariane">
        <Link href="/formules">Formules</Link>
        <span aria-hidden="true"> / </span>
        <span>{preset?.name ?? "Configuration"}</span>
      </nav>

      {!preset ? (
        <p className="formules-empty">
          Le catalogue des formules n&apos;est pas joignable pour le moment.
          Les prix sont servis par l&apos;API interne : le site public
          n&apos;en conserve aucun.
        </p>
      ) : (
        <>
          <header className="formule-header">
            <p className="eyebrow">Formule</p>
            <h1>{preset.name}</h1>
            <p className="formule-lead">{resolvePresetTagline(preset.code)}</p>
            <p className="formule-baseline">
              Configuration recommandée :{" "}
              <strong>
                {formatCurrencyFromCents(preset.baselineMonthlyAmountCents)}
              </strong>{" "}
              / mois sans engagement. Ajustez ci-dessous, le prix suit.
            </p>
          </header>

          <BillingV2FormuleConfigurator catalog={catalog} preset={preset} />

          <section className="formule-footnote">
            <h2>Ce que vous payez</h2>
            <p>
              Le prix affiché correspond exactement à la configuration retenue
              ci-dessus : chaque option ajoutée ou retirée met le total à jour
              immédiatement. Aucun frais de mise en service ne s&apos;ajoute au
              moment de la souscription, et la remise annoncée est celle qui
              sera réellement appliquée.
            </p>
            <p className="formule-footnote-secondary">
              Besoin d&apos;une configuration qui sort de ce cadre ?{" "}
              <Link className="text-link" href="/contact">
                Écrivez-nous
              </Link>
              , nous la mettons en place manuellement.
            </p>
          </section>
        </>
      )}
    </div>
  );
}
