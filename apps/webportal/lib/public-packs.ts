import type {
  BillingV2PublicCatalog,
  BillingV2PublicPreset,
  PublicPackCatalogContent,
  PublicPackCode,
  PublicPackManifest,
  PublicPackPresentation,
} from "@kermaria/shared";
import {
  PUBLIC_PACKS,
  createDefaultPublicPackCatalogContent,
} from "@kermaria/shared";

/**
 * Vitrine des formules.
 *
 * Une formule publique est la rencontre de deux sources qui ne se recouvrent
 * pas : l'editorial (manifeste `PUBLIC_PACKS` + contenu administrable) et le
 * commercial (`billing_v2_offer_presets`). Le code d'un preset **est** la cle
 * du pack : `pack-dossier-securise` designe le meme objet des deux cotes, et
 * c'est ce qui permet a la vitrine de ne rien tarifer elle-meme.
 *
 * Aucun montant n'est calcule ici. Le seul chiffre affiche est le point de
 * depart mensuel que le serveur a deja calcule
 * (`baselineMonthlyAmountCents`) ; le prix reellement engage vient de
 * `/formules/{code}`, qui interroge le moteur tarifaire.
 */
export type PublicPackView = {
  key: PublicPackCode;
  slug: string;
  label: string;
  shortLabel: string;
  headline: string;
  audience: string;
  description: string;
  highlights: readonly string[];
  included: readonly string[];
  technicalServiceReferences: readonly string[];
  highlightLabel: string | null;
  order: number;
  /** Code du preset Billing V2 — identique a `key`. */
  presetCode: string;
  /** Point de depart mensuel calcule par le serveur, en centimes. */
  baselineMonthlyAmountCents: number;
  currency: string;
};

export function buildPackPresentationMap(
  content: PublicPackCatalogContent | null = null,
) {
  const source = content ?? createDefaultPublicPackCatalogContent();
  return new Map<PublicPackCode, PublicPackPresentation>(
    source.packs.map((pack) => [pack.packCode, pack]),
  );
}

export function findPackPresentation(
  packKey: PublicPackCode,
  content: PublicPackCatalogContent | null = null,
) {
  return buildPackPresentationMap(content).get(packKey) ?? null;
}

/**
 * Formules publiables : celles dont le preset existe et est publie.
 *
 * Un pack sans preset correspondant n'est pas affiche. Le montrer sans prix
 * laisserait croire a une offre commandable qu'aucun moteur ne sait tarifer.
 */
export function buildPublicPackViews(
  catalog: BillingV2PublicCatalog,
  content: PublicPackCatalogContent | null = null,
): PublicPackView[] {
  const presentationByCode = buildPackPresentationMap(content);
  const presetByCode = new Map<string, BillingV2PublicPreset>(
    catalog.presets.map((preset) => [preset.code, preset]),
  );

  return PUBLIC_PACKS.map((manifest) => {
    const preset = presetByCode.get(manifest.key);
    return preset ? toView(manifest, preset, presentationByCode, catalog) : null;
  })
    .filter((view): view is PublicPackView => view !== null)
    .sort((left, right) => left.order - right.order);
}

export function findPublicPackView(
  views: readonly PublicPackView[],
  packKey: PublicPackCode,
) {
  return views.find((view) => view.key === packKey) ?? null;
}

function toView(
  manifest: PublicPackManifest,
  preset: BillingV2PublicPreset,
  presentationByCode: Map<PublicPackCode, PublicPackPresentation>,
  catalog: BillingV2PublicCatalog,
): PublicPackView {
  const presentation = presentationByCode.get(manifest.key) ?? null;

  return {
    key: manifest.key,
    slug: manifest.slug,
    // L'editorial administrable prime sur le manifeste fige : c'est lui que
    // l'exploitant peut corriger sans redeployer.
    label: presentation?.label ?? manifest.label,
    shortLabel: presentation?.shortLabel ?? manifest.shortLabel,
    headline: presentation?.headline ?? manifest.headline,
    audience: presentation?.audience ?? manifest.audience,
    description: presentation?.description ?? manifest.description,
    highlights: presentation?.highlights ?? manifest.highlights,
    included: presentation?.included ?? manifest.included,
    technicalServiceReferences: manifest.technicalServiceReferences,
    highlightLabel: presentation?.highlightLabel ?? null,
    order: presentation?.displayOrder ?? manifest.order,
    presetCode: preset.code,
    baselineMonthlyAmountCents: preset.baselineMonthlyAmountCents,
    currency: catalog.currency,
  };
}
