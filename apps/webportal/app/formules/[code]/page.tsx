import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";

import { BillingV2FormuleConfigurator } from "@/components/BillingV2FormuleConfigurator";
import { SitePageFrame } from "@/components/SitePageFrame";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { getBillingV2FormulesCatalog, getPublicSitePageLayout } from "@/lib/internal-api";
import { buildPublicMetadata, CONTENT_UNAVAILABLE_ROBOTS } from "@/lib/public-metadata";
import { resolvePresetTagline } from "@/lib/billing-v2-formules";
import { readBillingV2SelectionSearchParams } from "@/lib/billing-v2-selection";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ code: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export async function generateMetadata({
  params,
}: PageProps): Promise<Metadata> {
  const { code } = await params;
  const { data: catalog } = await getBillingV2FormulesCatalog();
  const preset = catalog.presets.find((item) => item.code === code);

  return buildPublicMetadata({
    title: preset ? `Configurer l'offre ${preset.name}` : "Configurer une offre",
    description: preset
      ? resolvePresetTagline(preset)
      : "Ajustez la capacité, la sauvegarde et les accès de votre offre.",
    path: `/formules/${code}`,
    ...(preset ? {} : { robots: CONTENT_UNAVAILABLE_ROBOTS }),
  });
}

export default async function FormuleConfigurationPage({ params, searchParams }: PageProps) {
  const { code } = await params;
  const resumedSelection = readBillingV2SelectionSearchParams(await searchParams);
  const [{ data: catalog }, layoutResult] = await Promise.all([
    getBillingV2FormulesCatalog(),
    getPublicSitePageLayout("/formules/[code]"),
  ]);
  const preset = catalog.presets.find((item) => item.code === code);

  if (catalog.presets.length > 0 && !preset) {
    notFound();
  }

  if (!preset) return <div className="formule-page">
    <nav className="formule-breadcrumb" aria-label="Fil d'Ariane">
      <Link href="/formules">Offres</Link>
    </nav>
    <h1>Offres momentanément indisponibles</h1>
    <p className="formules-empty">Réessayez plus tard ou <Link href="/contact">contactez-nous</Link>.</p>
  </div>;

  const breadcrumb = <nav className="formule-breadcrumb" aria-label="Fil d'Ariane">
    <Link href="/formules">Offres</Link>
    <span aria-hidden="true"> / </span>
    <span>{preset.name}</span>
  </nav>;
  const introduction = <header className="formule-header">
    <p className="eyebrow">Offre</p>
    <h1>{preset.name}</h1>
    <p className="formule-lead">{resolvePresetTagline(preset)}</p>
    <p className="formule-baseline">
      Configuration recommandée :{" "}
      <strong>{formatCurrencyFromCents(preset.baselineMonthlyAmountCents)}</strong>{" "}
      / mois sans engagement. Ajustez ci-dessous, le prix suit.
    </p>
  </header>;
  const configurator = <BillingV2FormuleConfigurator
    catalog={catalog}
    preset={preset}
    initialSelection={resumedSelection?.presetCode === preset.code ? resumedSelection : null}
  />;
  const fallbackHelp = <section className="formule-footnote">
    <h2>Comment lire le prix ?</h2>
    <p>Le récapitulatif affiche le prix de votre sélection. Chaque option ajustée déclenche un nouveau calcul. Vous pourrez relire le montant et les conditions avant le paiement.</p>
    <p className="formule-footnote-secondary">
      Un besoin différent ? <Link className="text-link" href="/contact">Écrivez-nous</Link>.
    </p>
  </section>;

  return <div className="formule-page">
    <SitePageFrame area="public" pageKey="/formules/[code]"
      initialLayout={layoutResult.data}
      slots={{
        formule_detail_breadcrumb: breadcrumb,
        formule_detail_intro: introduction,
        formule_detail_configurator: configurator,
      }}>
      <>{breadcrumb}{introduction}{configurator}{fallbackHelp}</>
    </SitePageFrame>
  </div>;
}
